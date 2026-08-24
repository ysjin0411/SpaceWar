using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보스 체력을 표시하고,
/// 스테이지 진행도 바와 보스 체력 바의 교체 연출을 관리한다.
/// </summary>
public class BossHpUI : MonoBehaviour
{
    [Header("보스 연결")]

    // 보스가 생성된 순간 생성된 보스를 전달받는다.
    [SerializeField] BossEncounterManager _bossEncounterManager;

    // 중간보스 처치 후 실제 맵이 교체된 순간을 전달받는다.
    [SerializeField] StageTransitionManager _stageTransitionManager;

    [Header("스테이지 진행도 UI")]

    // 가운데로 접거나 양옆으로 펼칠 스테이지 진행도 최상위 오브젝트다.
    [SerializeField] RectTransform _stageProgressRoot;

    [Header("보스 체력 UI")]

    // 가운데로 접거나 양옆으로 펼칠 보스 체력 UI 최상위 오브젝트다.
    [SerializeField] RectTransform _bossHpRoot;

    // fillAmount로 보스의 현재 체력을 표시하는 이미지다.
    [SerializeField] Image _bossHpFillImage;

    // 중간보스 또는 파이널 보스 이름을 표시한다.
    [SerializeField] TMP_Text _bossNameText;

    [Header("연출 시간")]

    // 스테이지 진행도 바가 가운데로 접히는 시간이다.
    [Min(0.01f)]
    [SerializeField] float _stageCloseDuration = 0.25f;

    // 보스 체력 바가 가운데에서 펼쳐지는 시간이다.
    [Min(0.01f)]
    [SerializeField] float _bossOpenDuration = 0.3f;

    // 보스 체력 바가 가운데로 접히는 시간이다.
    [Min(0.01f)]
    [SerializeField] float _bossCloseDuration = 0.25f;

    // 맵 교체 후 스테이지 진행도 바가 다시 펼쳐지는 시간이다.
    [Min(0.01f)]
    [SerializeField] float _stageOpenDuration = 0.3f;

    // 보스 체력이 목표 fillAmount까지 감소하는 시간이다.
    [Min(0.01f)]
    [SerializeField] float _hpFillDuration = 0.25f;

    // 현재 UI에 연결되어 있는 보스다.
    BossEnemy _boundBoss;

    // 보스 체력 감소 연출을 관리한다.
    Tween _hpTween;

    // 스테이지 바와 보스 바의 교체 연출을 관리한다.
    Sequence _uiSequence;

    // 최초 체력 이벤트인지 확인한다.
    bool _hasReceivedInitialHealth;

    private void Awake()
    {
        if (_bossEncounterManager == null)
        {
            throw new InvalidOperationException(
                $"{name}의 BossEncounterManager가 연결되지 않았습니다.");
        }

        if (_stageTransitionManager == null)
        {
            throw new InvalidOperationException(
                $"{name}의 StageTransitionManager가 연결되지 않았습니다.");
        }

        if (_stageProgressRoot == null)
        {
            throw new InvalidOperationException(
                $"{name}의 StageProgressRoot가 연결되지 않았습니다.");
        }

        if (_bossHpRoot == null)
        {
            throw new InvalidOperationException(
                $"{name}의 BossHpRoot가 연결되지 않았습니다.");
        }

        if (_bossHpFillImage == null)
        {
            throw new InvalidOperationException(
                $"{name}의 BossHpFillImage가 연결되지 않았습니다.");
        }

        if (_bossNameText == null)
        {
            throw new InvalidOperationException(
                $"{name}의 BossNameText가 연결되지 않았습니다.");
        }

        if (_bossHpFillImage.type != Image.Type.Filled)
        {
            throw new InvalidOperationException(
                "Boss_HpBar의 Image Type은 Filled여야 합니다.");
        }

        // 스테이지 시작 시 진행도 바는 펼쳐진 상태로 둔다.
        SetScaleX(_stageProgressRoot, 1f);

        // 보스 체력 UI는 보스가 생성될 때까지 숨긴다.
        SetScaleX(_bossHpRoot, 0f);
        _bossHpRoot.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        // 보스가 생성된 순간을 전달받는다.
        _bossEncounterManager.BossSpawned += HandleBossSpawned;

        // 중간보스 이후 실제 맵이 교체된 순간을 전달받는다.
        _stageTransitionManager.SectionMapChanged +=
            HandleSectionMapChanged;
    }

    private void OnDisable()
    {
        _bossEncounterManager.BossSpawned -= HandleBossSpawned;

        _stageTransitionManager.SectionMapChanged -=
            HandleSectionMapChanged;

        UnbindBoss();

        // 비활성화된 UI에서 Tween이 계속 실행되지 않도록 정리한다.
        _hpTween?.Kill();
        _hpTween = null;

        _uiSequence?.Kill();
        _uiSequence = null;
    }

    /// <summary>
    /// 중간보스 또는 파이널 보스가 생성되었을 때 호출된다.
    /// </summary>
    private void HandleBossSpawned(BossEnemy spawnedBoss)
    {
        if (spawnedBoss == null)
        {
            return;
        }

        UnbindBoss();

        // 새로 생성된 보스의 체력 이벤트를 구독한다.
        _boundBoss = spawnedBoss;
        _boundBoss.HealthChanged += HandleHealthChanged;
        _boundBoss.Defeated += HandleBossDefeated;

        _hasReceivedInitialHealth = false;

        // 생성된 보스 프리팹에 설정된 표시 이름을 UI에 출력한다.
        _bossNameText.text = spawnedBoss.BossName;

        PlayBossOpen();
    }

    /// <summary>
    /// 보스의 현재 체력을 fillAmount에 반영한다.
    /// </summary>
    private void HandleHealthChanged(
        int currentHp,
        int maxHp)
    {
        if (maxHp <= 0)
        {
            return;
        }

        float targetFillAmount = Mathf.Clamp01(
            (float)currentHp / maxHp);

        // 보스 생성 직후에는 최대 체력을 즉시 표시한다.
        if (!_hasReceivedInitialHealth)
        {
            _hasReceivedInitialHealth = true;
            _bossHpFillImage.fillAmount = targetFillAmount;
            return;
        }

        // 연속으로 피해를 받으면 이전 연출을 중지하고
        // 현재 표시 위치에서 새로운 체력까지 다시 감소시킨다.
        _hpTween?.Kill();

        _hpTween = _bossHpFillImage
            .DOFillAmount(
                targetFillAmount,
                _hpFillDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);
    }

    /// <summary>
    /// 보스 사망 애니메이션까지 끝났을 때 체력 바를 접는다.
    /// </summary>
    private void HandleBossDefeated(BossEnemy defeatedBoss)
    {
        if (defeatedBoss != _boundBoss)
        {
            return;
        }

        UnbindBoss();
        PlayBossClose();
    }

    /// <summary>
    /// 스테이지 진행도 바를 접고 보스 체력 바를 펼친다.
    /// </summary>
    private void PlayBossOpen()
    {
        _uiSequence?.Kill();

        _bossHpRoot.gameObject.SetActive(true);
        SetScaleX(_bossHpRoot, 0f);

        _uiSequence = DOTween.Sequence();

        // 기존 스테이지 진행도 바를 가운데로 접는다.
        _uiSequence.Append(
            _stageProgressRoot
                .DOScaleX(0f, _stageCloseDuration)
                .SetEase(Ease.InCubic));

        // 스테이지 바가 접힌 뒤 보스 체력 바를 양옆으로 펼친다.
        _uiSequence.Append(
            _bossHpRoot
                .DOScaleX(1f, _bossOpenDuration)
                .SetEase(Ease.OutBack));

        _uiSequence.SetUpdate(true);
    }

    /// <summary>
    /// 보스 체력 바를 가운데로 접고 숨긴다.
    /// </summary>
    private void PlayBossClose()
    {
        _uiSequence?.Kill();
        _hpTween?.Kill();

        _uiSequence = DOTween.Sequence();

        _uiSequence.Append(
            _bossHpRoot
                .DOScaleX(0f, _bossCloseDuration)
                .SetEase(Ease.InCubic));

        _uiSequence.OnComplete(() =>
        {
            _bossHpRoot.gameObject.SetActive(false);
            _uiSequence = null;
        });

        _uiSequence.SetUpdate(true);
    }

    /// <summary>
    /// 중간보스 이후 맵이 실제로 교체된 순간 호출된다.
    /// </summary>
    private void HandleSectionMapChanged()
    {
        _uiSequence?.Kill();

        // 혹시 남아 있는 보스 체력 UI를 확실히 숨긴다.
        SetScaleX(_bossHpRoot, 0f);
        _bossHpRoot.gameObject.SetActive(false);

        // 진행도 바가 화면 중앙에서 펼쳐지도록 0부터 시작한다.
        SetScaleX(_stageProgressRoot, 0f);

        _uiSequence = DOTween.Sequence();

        _uiSequence.Append(
            _stageProgressRoot
                .DOScaleX(1f, _stageOpenDuration)
                .SetEase(Ease.OutBack));

        _uiSequence.OnComplete(() =>
        {
            _uiSequence = null;
        });

        _uiSequence.SetUpdate(true);
    }

    /// <summary>
    /// 현재 연결된 보스의 이벤트 구독을 해제한다.
    /// </summary>
    private void UnbindBoss()
    {
        if (_boundBoss == null)
        {
            return;
        }

        _boundBoss.HealthChanged -= HandleHealthChanged;
        _boundBoss.Defeated -= HandleBossDefeated;
        _boundBoss = null;
    }

    /// <summary>
    /// Y축과 Z축 크기는 유지하고 X축 크기만 변경한다.
    /// </summary>
    private void SetScaleX(
        RectTransform target,
        float scaleX)
    {
        Vector3 scale = target.localScale;
        scale.x = scaleX;
        target.localScale = scale;
    }
}
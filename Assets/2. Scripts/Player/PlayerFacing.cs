using System;
using UnityEngine;

/// <summary>
/// 플레이어의 좌우 방향과 발사 기준점을 함께 변경한다.
/// 에일리언 2페이즈 전투에서만 X키 전환을 허용한다.
/// </summary>
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(PlayerController))]
public sealed class PlayerFacing : MonoBehaviour
{
    // 실제 캐릭터 외형을 표시하는 Renderer다.
    [SerializeField]
    SpriteRenderer _characterRenderer;

    // Weapon에서 사용하는 것과 동일한 발사 기준점이다.
    [SerializeField]
    Transform _firePoint;

    // 사망, 강제 이동, 조작 금지 상태를 확인한다.
    PlayerController _playerController;

    // 현재 전투 중인 보스와 스테이지 진행 상태를 확인한다.
    BossEncounterManager _bossEncounterManager;
    StageFlowManager _stageFlowManager;

    // 오른쪽을 바라보는 기본 상태의 위치와 회전이다.
    Vector3 _defaultRendererPosition;
    Vector3 _defaultFirePointPosition;
    Quaternion _defaultFirePointRotation;

    // 캐릭터 프리팹에 저장된 원래 반전 상태다.
    bool _defaultFlipX;

    // 스테이지 참조가 전달된 뒤에만 입력을 처리한다.
    bool _isInitialized;

    // 현재 왼쪽을 바라보는지 다른 공격 시스템에 제공한다.
    public bool IsFacingLeft { get; private set; }

    private void Awake()
    {
        _playerController = GetComponent<PlayerController>();

        if (_characterRenderer == null || _firePoint == null)
        {
            throw new InvalidOperationException(
                $"{name}: Character Renderer와 FirePoint를 연결하세요.");
        }

        // 현재 캐릭터 프리팹처럼 두 오브젝트가 플레이어 바로 아래에
        // 있어야 플레이어 중심을 기준으로 위치를 반전할 수 있다.
        if (_characterRenderer.transform.parent != transform ||
            _firePoint.parent != transform)
        {
            throw new InvalidOperationException(
                $"{name}: Renderer와 FirePoint는 " +
                "플레이어 루트의 직접 자식이어야 합니다.");
        }

        // 전환을 반복해도 위치 오차가 누적되지 않도록 원본을 저장한다.
        _defaultRendererPosition =
            _characterRenderer.transform.localPosition;

        _defaultFirePointPosition = _firePoint.localPosition;
        _defaultFirePointRotation = _firePoint.localRotation;
        _defaultFlipX = _characterRenderer.flipX;
    }

    /// <summary>
    /// 플레이어 생성 후 StageManager가 씬의 관리자들을 전달한다.
    /// </summary>
    public void Initialize(
        BossEncounterManager bossEncounterManager,
        StageFlowManager stageFlowManager)
    {
        if (bossEncounterManager == null)
        {
            throw new ArgumentNullException(
                nameof(bossEncounterManager));
        }

        if (stageFlowManager == null)
        {
            throw new ArgumentNullException(
                nameof(stageFlowManager));
        }

        _bossEncounterManager = bossEncounterManager;
        _stageFlowManager = stageFlowManager;
        _isInitialized = true;

        SetFacing(false);
    }

    private void Update()
    {
        // 보스 처치, 스테이지 종료, 관리자 비활성화 시 기본 방향으로 복구한다.
        if (!CanUseFacingSwitch())
        {
            SetFacing(false);
            return;
        }

        // 일시정지, 사망, 강제 이동, 조작 금지 중에는 전환하지 않는다.
        if (Time.timeScale <= 0f || !_playerController.CanControl)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.X))
        {
            SetFacing(!IsFacingLeft);
        }
    }

    // 에일리언 2페이즈가 실제로 전투 중인지 확인한다.
    private bool CanUseFacingSwitch()
    {
        if (!_isInitialized ||
            _bossEncounterManager == null ||
            !_bossEncounterManager.isActiveAndEnabled ||
            _stageFlowManager == null ||
            !_stageFlowManager.isActiveAndEnabled)
        {
            return false;
        }

        if (_stageFlowManager.CurrentPhase !=
            StagePhase.FinalBossBattle)
        {
            return false;
        }

        BossEnemy boss = _bossEncounterManager.CurrentBoss;

        return boss is Boss_AlienBody &&
               boss.isActiveAndEnabled &&
               boss.HasBattleStarted &&
               !boss.IsDead;
    }

    // 캐릭터 외형, 외형의 위치, 발사 위치와 방향을 함께 반영한다.
    private void SetFacing(bool faceLeft)
    {
        if (IsFacingLeft == faceLeft)
        {
            return;
        }

        IsFacingLeft = faceLeft;

        // 스프라이트 자체의 좌우를 반전한다.
        // 기존 근접 공격과 방패 판정도 이 flipX 값을 사용한다.
        _characterRenderer.flipX =
            faceLeft ? !_defaultFlipX : _defaultFlipX;

        // Renderer가 플레이어 중심에서 벗어나 있다면 위치도 대칭 이동한다.
        Vector3 rendererPosition = _defaultRendererPosition;

        if (faceLeft)
        {
            rendererPosition.x = -rendererPosition.x;
        }

        _characterRenderer.transform.localPosition = rendererPosition;

        // 총구가 캐릭터 반대쪽에 남지 않도록 발사 위치를 반전한다.
        Vector3 firePointPosition = _defaultFirePointPosition;

        if (faceLeft)
        {
            firePointPosition.x = -firePointPosition.x;
        }

        _firePoint.localPosition = firePointPosition;

        if (faceLeft)
        {
            // 기본 발사 벡터의 X만 반전하여 좌우 대칭 방향을 구한다.
            Vector3 direction =
                _defaultFirePointRotation * Vector3.right;

            direction.x = -direction.x;

            float angle =
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            _firePoint.localRotation =
                Quaternion.Euler(0f, 0f, angle);
        }
        else
        {
            // 오른쪽으로 복귀할 때 프리팹의 원래 회전을 복원한다.
            _firePoint.localRotation = _defaultFirePointRotation;
        }
    }

    private void OnDisable()
    {
        // 캐릭터가 비활성화될 때 반전 상태를 남기지 않는다.
        SetFacing(false);
    }
}
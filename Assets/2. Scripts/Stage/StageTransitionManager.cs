using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 플레이어의 스테이지 입장과 구간 전환 연출을 담당한다.
/// 현재 단계에서는 최초 입장 연출만 처리한다.
/// </summary>
public class StageTransitionManager : MonoBehaviour
{
    [Header("스테이지 흐름")]

    [SerializeField] StageFlowManager _stageFlowManager;
    // 입장 연출 완료 후 Section1을 시작할 StageFlowManager
    [SerializeField] Transform _exitPoint;
    // 미드보스 처치 후 플레이어가 퇴장할 화면 오른쪽 바깥 위치

    [Min(0f)]
    [SerializeField] float _exitStartDelay = 0.5f;
    // 사망 애니메이션 완료 후 플레이어가 움직이기 전까지 기다리는 시간

    [Min(0.1f)]
    [SerializeField] float _exitDuration = 2f;
    // 플레이어가 화면 오른쪽 밖으로 퇴장하는 데 걸리는 시간

    [SerializeField] BackgroundScroller _backgroundScroller;
    // Section1과 Section2의 배경 스프라이트를 관리하는 스크롤러

    [SerializeField] Sprite _section2BackgroundSprite;
    // 플레이어가 화면 밖으로 나간 뒤 적용할 Section2 배경

    // 구간 전환 연출이 중복 실행되는 것을 방지한다.
    bool _isSectionTransitionPlaying;
    [Header("플레이어 입장 위치")]

    [SerializeField] Transform _entranceStartPoint;
    // 플레이어가 입장 연출을 시작할 화면 왼쪽 바깥 위치

    [SerializeField] Transform _battlePoint;
    // 입장 연출이 끝났을 때 플레이어가 도착할 전투 위치

    [Header("플레이어 입장 시간")]

    [Min(0.1f)]
    [SerializeField] float _entranceDuration = 2f;
    // 시작 위치에서 전투 위치까지 이동하는 데 걸리는 시간

    // StageManager가 생성한 현재 플레이어
    PlayerController _playerController;

    // 입장 연출에서 이동시킬 플레이어 Rigidbody2D
    Rigidbody2D _playerRigidbody;

    // 현재 실행 중인 플레이어 이동 연출
    Tween _playerMoveTween;

    // StageManager로부터 플레이어를 전달받았는지 확인한다.
    bool _isInitialized;

    // 동일한 입장 연출이 중복 실행되는 것을 방지한다.
    bool _isEntrancePlaying;

    // 중간보스 이후 실제 맵 배경이 교체된 순간을 알린다.
    public event Action SectionMapChanged;

    private void Awake()
    {
        if (_stageFlowManager == null)
        {
            throw new InvalidOperationException(
                $"{name}의 StageFlowManager가 연결되지 않았습니다.");
        }

        if (_entranceStartPoint == null)
        {
            throw new InvalidOperationException(
                $"{name}의 Entrance Start Point가 연결되지 않았습니다.");
        }

        if (_battlePoint == null)
        {
            throw new InvalidOperationException(
                $"{name}의 Battle Point가 연결되지 않았습니다.");
        }

        if (_exitPoint == null)
        {
            throw new InvalidOperationException(
                $"{name}의 Exit Point가 연결되지 않았습니다.");
        }

        if (_backgroundScroller == null)
        {
            throw new InvalidOperationException(
                $"{name}의 Background Scroller가 연결되지 않았습니다.");
        }

        if (_section2BackgroundSprite == null)
        {
            throw new InvalidOperationException(
                $"{name}의 Section2 Background Sprite가 연결되지 않았습니다.");
        }
    }

    private void OnEnable()
    {
        if (_stageFlowManager != null)
        {
            _stageFlowManager.PhaseChanged += HandlePhaseChanged;
        }
    }

    private void OnDisable()
    {
        if (_stageFlowManager != null)
        {
            _stageFlowManager.PhaseChanged -= HandlePhaseChanged;
        }

        // 오브젝트가 비활성화될 때 실행 중인 이동 연출을 정리한다.
        _playerMoveTween?.Kill();
        _playerMoveTween = null;
        _isEntrancePlaying = false;
        _isSectionTransitionPlaying = false;
    }

    /// <summary>
    /// StageManager가 생성한 플레이어를 전달받는다.
    /// </summary>
    public void Initialize(PlayerController playerController)
    {
        if (playerController == null)
        {
            throw new ArgumentNullException(nameof(playerController));
        }

        _playerController = playerController;
        _playerRigidbody =
            _playerController.GetComponent<Rigidbody2D>();

        if (_playerRigidbody == null)
        {
            throw new MissingComponentException(
                $"{_playerController.name}에 Rigidbody2D가 없습니다.");
        }

        _isInitialized = true;

        // StageIntro가 먼저 시작된 경우에도 입장 연출을 놓치지 않도록 확인한다.
        if (_stageFlowManager.CurrentPhase == StagePhase.StageIntro)
        {
            StartEntrance();
        }
    }

    /// <summary>
    /// 스테이지 페이즈에 맞는 플레이어 연출을 시작한다.
    /// </summary>
    private void HandlePhaseChanged(StagePhase phase)
    {
        switch (phase)
        {
            case StagePhase.StageIntro:

                // 최초 스테이지 입장 연출을 시작한다.
                StartEntrance();
                break;

            case StagePhase.SectionTransition:

                // 미드보스 처치 후 다음 구간으로 이동하는 연출을 시작한다.
                StartSectionTransition();
                break;
        }
    }

    /// <summary>
    /// 플레이어를 화면 왼쪽 밖에서 전투 위치까지 이동시킨다.
    /// </summary>
    private void StartEntrance()
    {
        // 아직 플레이어를 전달받지 않았다면 Initialize에서 다시 시작한다.
        if (!_isInitialized)
        {
            return;
        }

        // 이미 입장 연출이 진행 중이라면 중복 실행하지 않는다.
        if (_isEntrancePlaying)
        {
            return;
        }

        _isEntrancePlaying = true;

        // 입장 연출 중에는 플레이어 직접 조작을 막는다.
        _playerController.SetControlEnabled(false);

        // 플레이어를 화면 왼쪽 바깥 시작 위치로 옮긴다.
        _playerRigidbody.position =
            _entranceStartPoint.position;

        // 이전 이동 연출이 남아 있다면 제거한다.
        _playerMoveTween?.Kill();

        // Rigidbody2D를 전투 시작 위치까지 이동시킨다.
        _playerMoveTween = _playerRigidbody
            .DOMove(
                _battlePoint.position,
                _entranceDuration)
            .SetEase(Ease.Linear)
            .OnComplete(() =>
            {
                _playerMoveTween = null;
                _isEntrancePlaying = false;

                // 전투 위치에 도착했으므로 플레이어 조작을 허용한다.
                _playerController.SetControlEnabled(true);

                // StageFlowManager에 입장 연출 완료를 알린다.
                _stageFlowManager.NotifyStageIntroCompleted();
            });
    }

    /// <summary>
    /// 미드보스 처치 후 플레이어를 오른쪽으로 퇴장시키고,
    /// 배경을 교체한 뒤 왼쪽에서 다시 입장시킨다.
    /// </summary>
    private void StartSectionTransition()
    {
        // 아직 플레이어를 전달받지 않았다면 연출을 시작할 수 없다.
        if (!_isInitialized)
        {
            return;
        }

        // 이미 구간 전환 연출이 진행 중이라면 중복 실행하지 않는다.
        if (_isSectionTransitionPlaying)
        {
            return;
        }

        _isSectionTransitionPlaying = true;

        // 구간 전환 중에는 플레이어가 직접 이동하지 못하게 한다.
        _playerController.SetControlEnabled(false);

        // 이전 플레이어 이동 연출이 남아 있다면 제거한다.
        _playerMoveTween?.Kill();

        Sequence transitionSequence = DOTween.Sequence();

        // 보스 사망 연출이 끝난 뒤 잠시 기다린다.
        transitionSequence.AppendInterval(_exitStartDelay);

        // 현재 Y 위치를 유지하면서 화면 오른쪽 밖으로 이동한다.
        transitionSequence.Append(
            _playerRigidbody
                .DOMoveX(
                    _exitPoint.position.x,
                    _exitDuration)
                .SetEase(Ease.Linear));

        // 플레이어가 화면 밖에 있을 때 두 번째 구간 배경으로 교체한다.
        transitionSequence.AppendCallback(() =>
        {
            _backgroundScroller.StopScrolling();

            // 새 배경을 보여주기 전에 이전 구간의 적과 공격을 정리한다.
            ClearPreviousSectionEnemies();

            _backgroundScroller.ChangeBackground(
                _section2BackgroundSprite);

            // 맵이 교체된 바로 이 순간에
            // 스테이지 진행도 UI를 다시 펼치도록 알린다.
            SectionMapChanged?.Invoke();

            // 플레이어를 화면 왼쪽 바깥으로 옮긴다.
            _playerRigidbody.position =
                _entranceStartPoint.position;

            _backgroundScroller.ResumeScrolling();
        });

        // 새로운 배경에서 전투 위치까지 다시 입장한다.
        transitionSequence.Append(
            _playerRigidbody
                .DOMove(
                    _battlePoint.position,
                    _entranceDuration)
                .SetEase(Ease.Linear));

        transitionSequence.OnComplete(() =>
        {
            _playerMoveTween = null;
            _isSectionTransitionPlaying = false;

            // 재입장이 끝났으므로 플레이어 조작을 허용한다.
            _playerController.SetControlEnabled(true);

            // StageFlowManager에 구간 전환 완료를 알린다.
            _stageFlowManager.NotifySectionTransitionCompleted();
        });

        _playerMoveTween = transitionSequence;
    }

    /// <summary>
    /// 배경 교체 순간 이전 구간의 일반 적과 적 공격을 제거한다.
    /// 처치가 아니므로 피해·사망·보상 처리를 호출하지 않는다.
    /// </summary>
    private void ClearPreviousSectionEnemies()
    {
        // 먼저 적을 중단시켜 더 이상 공격하지 못하게 한다.
        RemoveSectionObjects<Enemy>();

        // 이미 발사된 일반 적·보스의 총알을 제거한다.
        RemoveSectionObjects<EnemyBullet>();

        // 남아 있는 보스 레이저와 레이저 경고도 정리한다.
        RemoveSectionObjects<BossLaserBeam>();
        RemoveSectionObjects<BossLaserWarning>();

        // 이전 맵의 블랙홀과 플레이어에게 적용한 흡인도 정리한다.
        RemoveSectionObjects<BossBlackHole>();
    }

    /// <summary>
    /// 현재 스테이지 씬에 속한 지정 종류의 오브젝트를 제거한다.
    /// 맵 교체 시 한 번만 검색하며 비활성 오브젝트도 포함한다.
    /// </summary>
    private void RemoveSectionObjects<T>() where T : Component
    {
        // 제거 순서는 중요하지 않으므로 정렬 비용을 생략한다.
        T[] targets = UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (T target in targets)
        {
            if (target == null)
            {
                continue;
            }

            // 다른 씬의 오브젝트는 정리 대상에서 제외한다.
            if (target.gameObject.scene != gameObject.scene)
            {
                continue;
            }

            // 풀링된 오브젝트는 파괴하지 않고 원래 풀에 반납한다.
            if (target.TryGetComponent(out PoolObject poolObject))
            {
                // 이미 풀에서 대기 중이면 내부 검사에 의해 아무 작업도 하지 않는다.
                poolObject.ReturnToPool();

                // 아래의 비활성화와 Destroy 처리를 건너뛴다.
                continue;
            }

            // Destroy는 프레임 끝에 처리되므로 먼저 비활성화한다.
            // 같은 프레임의 추가 공격·충돌·표시를 즉시 중단한다.
            target.gameObject.SetActive(false);

            // 처치 이벤트를 거치지 않고 오브젝트만 제거한다.
            Destroy(target.gameObject);
        }
    }
}

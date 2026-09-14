using UnityEngine;

// NPC의 고유 기능 창을 열고, 창을 사용하는 동안 조작을 잠근다.
public class TownNpcActionPresenter : MonoBehaviour
{
    // Town의 Canvas 아래에 배치한 업적 창 View.
    [SerializeField] private AchievementWindowView _achievementView;

    // 기존 Stage Service 필드에 연결된 컴포넌트 참조를 유지한다.
    [UnityEngine.Serialization.FormerlySerializedAs("_stageService")]

    // 스테이지와 상점 화면을 같은 NPC 서비스 창에서 관리한다.
    [SerializeField] private TownNpcServicePresenter _serviceWindow;

    // 현재 기능 창을 이용하는 플레이어.
    private TownPlayerController _player;

    // 기능 창의 사용 상태와 닫기 연출 진행 여부.
    private bool _isOpen;
    private bool _isClosing;

    // 업적 창이 완전히 닫히는 시점을 구독한다.
    private void OnEnable()
    {
        if (_achievementView != null)
        {
            _achievementView.Closed += HandleClosed;
        }
    }

    // NPC 종류에 해당하는 기능을 연다.
    // 성공한 경우 대화 Presenter에서 조작 잠금의 책임을 넘겨받는다.
    public bool TryOpen(
        NpcActionType actionType,
        TownPlayerController player)
    {
        if (!isActiveAndEnabled || _isOpen ||
            player == null || !player.isActiveAndEnabled)
        {
            return false;
        }

        // 스테이지와 상점은 공용 창 관리자가 화면 전환과 조작 잠금을 담당한다.
        if (actionType == NpcActionType.Stage ||
            actionType == NpcActionType.Shop)
        {
            if (_serviceWindow == null)
            {
                Debug.LogError(
                    "TownNpcActionPresenter의 Service Window를 연결해주세요.",
                    this);
                return false;
            }

            return _serviceWindow.TryOpen(actionType, player);
        }

        // 이번 단계에서는 업적 기능을 연결한다.
        // 연결되지 않은 기능은 대화창을 그대로 유지한다.
        if (actionType != NpcActionType.Achievement)
        {
            return false;
        }

        if (_achievementView == null ||
            !_achievementView.enabled ||
            SaveManager.Instance == null ||
            !SaveManager.Instance.IsLoggedIn ||
            AchievementManager.Instance == null)
        {
            Debug.LogError(
                "업적 View, 로그인 상태, AchievementManager를 확인해주세요.",
                this);

            return false;
        }

        // 부모 Canvas가 꺼져 있으면 창을 표시할 수 없다.
        Transform parent = _achievementView.transform.parent;

        if (parent != null && !parent.gameObject.activeInHierarchy)
        {
            return false;
        }

        _player = player;
        _isOpen = true;
        _isClosing = false;

        _player.SetControlEnabled(false);
        _achievementView.OpenWindow();

        // NPC 고유기능으로 업적창을 연 뒤 해당 퀘스트 목표를 검사한다.
        try
        {
            SaveManager.Instance.RecordAchievementWindowOpened();
        }
        catch (System.Exception exception)
        {
            // 저장에 실패해도 이미 열린 업적창의 조작 흐름은 유지한다.
            // 목표 달성 상태는 저장 코드에서 진행 중으로 복구된다.
            Debug.LogException(exception, this);
        }

        return true;
    }

    // Escape로도 업적 창의 기존 닫기 연출을 실행한다.
    private void Update()
    {
        if (!_isOpen || _isClosing)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            _isClosing = true;
            _achievementView.CloseWindow();
        }
    }

    // 창이 실제로 닫힌 뒤에만 플레이어 조작을 복구한다.
    private void HandleClosed()
    {
        if (!_isOpen)
        {
            return;
        }

        _isOpen = false;
        _isClosing = false;

        if (_player != null)
        {
            _player.SetControlEnabled(true);
        }

        _player = null;
    }

    // 관리자가 꺼질 때도 남은 창과 조작 잠금을 정리한다.
    private void OnDisable()
    {
        if (_achievementView != null)
        {
            _achievementView.Closed -= HandleClosed;

            // 이 Presenter가 열었던 창만 닫는다.
            if (_isOpen)
            {
                _achievementView.gameObject.SetActive(false);
            }
        }

        HandleClosed();
    }
}
using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Town 서비스 창에서 스테이지·캐릭터 선택과 출격 순서를 관리한다.
public class TownNpcServicePresenter : MonoBehaviour
{
    // 기존 NpcServiceRoot의 CanvasGroup.
    [SerializeField] private CanvasGroup _root;

    // 기존 ContentViewport 아래의 ScreenRoot.
    [SerializeField] private RectTransform _screenRoot;

    // 기존 창의 제목과 닫기 버튼.
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private Button _closeButton;

    // 서비스 이용 중 입력을 차단할 기존 Town Canvas의 CanvasGroup.
    [SerializeField] private CanvasGroup _townUiGroup;

    // 추가 씬에서도 유지해서 사용할 Town의 EventSystem.
    [SerializeField] private EventSystem _townEventSystem;

    // 출격 연출에서 위로 이동시킬 TownPlayer의 Visual.
    [SerializeField] private SpriteRenderer _playerVisual;

    // 출격 중 TownPlayer 본체가 움직이지 않게 할 Rigidbody2D.
    [SerializeField] private Rigidbody2D _playerBody;

    // 창이 나타나거나 사라지는 시간.
    [SerializeField, Min(0.01f)] private float _windowDuration = 0.2f;

    // 캐릭터가 위로 이동하면서 사라지는 시간과 거리.
    [SerializeField, Min(0.01f)] private float _departureDuration = 0.8f;
    [SerializeField, Min(0.1f)] private float _departureHeight = 10f;

    // 로딩 화면 전체를 켜고 끄는 패널.
    [SerializeField] private GameObject _loadingPanel;

    // 로딩 문구와 게이지 표시를 담당하는 컴포넌트.
    [SerializeField] private LoadingScreenUI _loadingUI;

    // 현재 조작을 잠근 Town 플레이어.
    private TownPlayerController _player;

    // 현재 창에 표시 중인 추가 씬과 연결 컴포넌트.
    private Scene _pageScene;
    private NpcServiceScenePage _page;

    // 로딩 중인 씬을 구분하고 연결 실패를 전달한다.
    private string _loadingSceneName = string.Empty;
    private Exception _mountError;

    // 중복 전환과 출격 중 닫기 입력을 막는다.
    private bool _busy;

    // 서비스 이용 전 Town UI의 입력 설정.
    private bool _previousInteractable;
    private bool _previousBlocksRaycasts;

    // 출격 실패 시 복구할 캐릭터 표시·물리 상태.
    private Vector3 _visualPosition;
    private Color _visualColor;
    private bool _previousSimulated;
    private bool _departing;

    // 외부에서 서비스 이용 여부를 확인한다.
    public bool IsOpen { get; private set; }

    // 필수 연결을 검사하고 창 입력과 씬 로딩 알림을 등록한다.
    private void Awake()
    {
        // 로딩 화면과 표시 담당 컴포넌트의 연결을 확인한다.
        if (_loadingPanel == null || _loadingUI == null)
        {
            throw new MissingReferenceException(
                "LoadingPanel과 LoadingUI를 연결해주세요.");
        }

        // Town에 처음 들어왔을 때 로딩 화면을 숨긴다.
        _loadingPanel.SetActive(false);

        if (_root == null || _screenRoot == null ||
            _titleText == null || _closeButton == null ||
            _townUiGroup == null || _townEventSystem == null ||
            _playerVisual == null || _playerBody == null)
        {
            throw new MissingReferenceException(
                "TownStageServicePresenter의 Inspector 연결을 확인해주세요.");
        }

        _closeButton.onClick.AddListener(RequestClose);
        SceneManager.sceneLoaded += HandleSceneLoaded;

        _root.alpha = 0f;
        _root.gameObject.SetActive(false);
    }

    // NPC 종류에 따라 첫 화면과 캐릭터 선택 목적을 결정한다.
    public bool TryOpen(
        NpcActionType actionType,
        TownPlayerController player)
    {
        if (!isActiveAndEnabled || IsOpen || _busy ||
            player == null || !player.isActiveAndEnabled ||
            SaveManager.Instance == null ||
            !SaveManager.Instance.IsLoggedIn ||
            GameSessionManager.Instance == null)
        {
            return false;
        }

        // 이 창은 스테이지 출격과 상점 이용을 지원한다.
        if (actionType != NpcActionType.Stage &&
            actionType != NpcActionType.Shop)
        {
            return false;
        }

        bool isShop = actionType == NpcActionType.Shop;

        // 두 서비스가 공통으로 쓰는 캐릭터 선택 씬과
        // 각 서비스의 전용 씬이 등록되어 있는지 확인한다.
        string serviceScene = isShop ? "EquipmentManage" : "StageSelect";

        if (!Application.CanStreamedLevelBeLoaded("CharacterSelect") ||
            !Application.CanStreamedLevelBeLoaded(serviceScene))
        {
            Debug.LogError(
                $"CharacterSelect와 {serviceScene}의 빌드 씬 등록을 확인해주세요.",
                this);
            return false;
        }

        GameSessionManager session = GameSessionManager.Instance;

        // 이전 서비스 이용에서 남은 선택 정보를 초기화한다.
        session.ResetSelection();

        if (isShop)
        {
            // 상점에서는 장비를 구매하고 장착할 캐릭터부터 선택한다.
            session.BeginCharacterSelection(
                CharacterSelectPurpose.LoadoutEdit);
        }

        // 창을 사용하는 동안 Town 캐릭터와 기본 UI 입력을 막는다.
        _player = player;
        _player.SetControlEnabled(false);

        _previousInteractable = _townUiGroup.interactable;
        _previousBlocksRaycasts = _townUiGroup.blocksRaycasts;
        _townUiGroup.interactable = false;
        _townUiGroup.blocksRaycasts = false;

        IsOpen = true;
        _root.gameObject.SetActive(true);

        // 같은 창 안에서 서비스별 첫 화면을 불러온다.
        string firstScene = isShop ? "CharacterSelect" : "StageSelect";
        string title = isShop ? "상점 - 캐릭터 선택" : "스테이지 선택";

        StartCoroutine(ChangePage(firstScene, title));
        return true;
    }

    // 로딩·출격 중이 아닐 때만 Esc로 닫는다.
    private void Update()
    {
        if (IsOpen && !_busy && Input.GetKeyDown(KeyCode.Escape))
        {
            RequestClose();
        }
    }

    // 창과 내부 화면의 클릭 가능 여부를 함께 바꾼다.
    private void SetBusy(bool busy)
    {
        _busy = busy;
        _root.interactable = !busy;
        _closeButton.interactable = !busy;
    }

    // 현재 선택 목적을 유지하면서 공통 캐릭터 선택 화면을 연다.
    private void RequestCharacterSelection()
    {
        if (!IsOpen || _busy)
        {
            return;
        }

        bool isShop =
            GameSessionManager.Instance.CurrentCharacterSelectPurpose ==
            CharacterSelectPurpose.LoadoutEdit;

        string title = isShop ? "상점 - 캐릭터 선택" : "캐릭터 선택";

        StartCoroutine(ChangePage("CharacterSelect", title));
    }

    // 상점에서 캐릭터를 선택하면 같은 창 안에 구매·장착 화면을 연다.
    private void RequestEquipment()
    {
        if (!IsOpen || _busy)
        {
            return;
        }

        StartCoroutine(ChangePage("EquipmentManage", "상점"));
    }

    // 캐릭터 선택 화면의 뒤로 가기는 창 내부의 스테이지 선택으로 연결한다.
    private void RequestStageSelection()
    {
        if (IsOpen && !_busy)
        {
            StartCoroutine(ChangePage("StageSelect", "스테이지 선택"));
        }
    }

    // 추가 씬을 교체한다. Town은 계속 유지한다.
    private IEnumerator ChangePage(string sceneName, string title)
    {
        
        SetBusy(true);
        _titleText.text = "불러오는 중…";

        // 로딩 중 키보드 선택이 이전 버튼에 남지 않게 한다.
        _townEventSystem.SetSelectedGameObject(null);

        yield return UnloadPage();


        _loadingSceneName = sceneName;
        _mountError = null;

        AsyncOperation operation = null;

        try
        {
            operation = SceneManager.LoadSceneAsync(
                sceneName, LoadSceneMode.Additive);
        }
        catch (Exception exception)
        {
            _mountError = exception;
        }

        if (operation != null)
        {
            yield return operation;

        }

        _loadingSceneName = string.Empty;

        if (_mountError != null || _page == null)
        {
            if (_mountError != null)
            {
                Debug.LogException(_mountError, this);
            }
            else
            {
                Debug.LogError(
                    $"{sceneName}의 NpcServiceScenePage 연결을 확인해주세요.",
                    this);
            }

            // 실패한 추가 씬과 입력 잠금을 정리한다.
            yield return CloseRoutine();


            yield break;
        }

        _titleText.text = title;

        yield return _root
            .DOFade(1f, _windowDuration)
            .SetUpdate(true)
            .SetLink(gameObject)
            .WaitForCompletion();

        SetBusy(false);
    }

    // 추가 씬의 Start가 실행되기 전에 화면과 전환 이벤트를 연결한다.
    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsOpen ||
            mode != LoadSceneMode.Additive ||
            scene.name != _loadingSceneName)
        {
            return;
        }

        _pageScene = scene;

        try
        {
            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            {
                NpcServiceScenePage candidate =
                    sceneRoot.GetComponent<NpcServiceScenePage>();

                if (candidate != null)
                {
                    _page = candidate;
                    break;
                }
            }

            if (_page == null)
            {
                throw new InvalidOperationException(
                    $"{scene.name}의 Manager에 NpcServiceScenePage가 없습니다.");
            }

            _page.Attach(_screenRoot, _townEventSystem);

            if (scene.name == "StageSelect")
            {
                StageSelectManager stage =
                    _page.GetComponent<StageSelectManager>();

                if (stage == null)
                {
                    throw new MissingComponentException(
                        "NpcServiceScenePage를 StageSelectManager와 같은 오브젝트에 붙여주세요.");
                }

                stage.CharacterSelectionRequested +=
                    RequestCharacterSelection;
            }
            else if (scene.name == "CharacterSelect")
            {
                // 공통 캐릭터 선택 화면의 전투·상점 요청을 연결한다.
                CharacterSelectScreen character =
                    _page.GetComponent<CharacterSelectScreen>();

                if (character == null)
                {
                    throw new MissingComponentException(
                        "NpcServiceScenePage와 CharacterSelectScreen을 같은 오브젝트에 붙여주세요.");
                }

                character.StageSelectionRequested += RequestStageSelection;
                character.DepartureRequested += RequestDeparture;
                character.EquipmentRequested += RequestEquipment;
                character.CloseRequested += RequestClose;
            }
            else if (scene.name == "EquipmentManage")
            {
                // 구매·장착 화면에서 다른 캐릭터를 선택할 수 있도록 연결한다.
                EquipmentManager equipment =
                    _page.GetComponent<EquipmentManager>();

                if (equipment == null)
                {
                    throw new MissingComponentException(
                        "NpcServiceScenePage와 EquipmentManager를 같은 오브젝트에 붙여주세요.");
                }

                equipment.CharacterSelectionRequested += RequestCharacterSelection;
            }
            else
            {
                // 지원하지 않는 씬을 잘못 연결했을 때 원인을 명확하게 알린다.
                throw new InvalidOperationException(
                    $"지원하지 않는 NPC 서비스 씬입니다: {scene.name}");
            }
        }
        catch (Exception exception)
        {
            _mountError = exception;
        }
    }

    // 화면을 제거하고, 소유한 추가 씬만 내린다.
    private IEnumerator UnloadPage()
    {
        if (_page != null)
        {
            StageSelectManager stage =
                _page.GetComponent<StageSelectManager>();

            if (stage != null)
            {
                stage.CharacterSelectionRequested -=
                    RequestCharacterSelection;
            }

            CharacterSelectScreen character =
                _page.GetComponent<CharacterSelectScreen>();

            if (character != null)
            {
                // 캐릭터 선택 화면이 사라지기 전에 모든 화면 전환 연결을 해제한다.
                character.StageSelectionRequested -= RequestStageSelection;
                character.DepartureRequested -= RequestDeparture;
                character.EquipmentRequested -= RequestEquipment;
                character.CloseRequested -= RequestClose;
            }

            // 구매·장착 화면의 복귀 이벤트도 화면 제거 전에 해제한다.
            EquipmentManager equipment =
                _page.GetComponent<EquipmentManager>();

            if (equipment != null)
            {
                equipment.CharacterSelectionRequested -= RequestCharacterSelection;
            }

            _page.Release();
            _page = null;
        }

        if (_pageScene.IsValid() && _pageScene.isLoaded)
        {
            AsyncOperation unload =
                SceneManager.UnloadSceneAsync(_pageScene);

            if (unload != null)
            {
                yield return unload;
            }
        }

        _pageScene = default;
    }

    // 닫기 버튼과 스테이지 화면의 뒤로 가기를 처리한다.
    public void RequestClose()
    {
        if (!IsOpen || _busy)
        {
            return;
        }

        StartCoroutine(CloseRoutine());
    }

    // 취소하면 선택 화면을 정리하고 Town 조작을 복구한다.
    private IEnumerator CloseRoutine()
    {
        SetBusy(true);

        yield return _root
            .DOFade(0f, _windowDuration)
            .SetUpdate(true)
            .SetLink(gameObject)
            .WaitForCompletion();

        _root.gameObject.SetActive(false);

        yield return UnloadPage();

        if (GameSessionManager.Instance != null)
        {
            GameSessionManager.Instance.ResetSelection();
        }

        RestoreTown();
    }

    // 캐릭터 선택 후 목적지를 확인하고 출격 순서를 시작한다.
    private void RequestDeparture(string destination)
    {
        if (!IsOpen || _busy)
        {
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(destination))
        {
            Debug.LogError(
                $"빌드 씬 목록에 {destination}이 없습니다.", this);
            RequestClose();
            return;
        }

        StartCoroutine(Depart(destination));
    }

    // 창 닫기 → 선택 씬 정리 → 캐릭터 상승·소멸 → 전투 씬 진입.
    private IEnumerator Depart(string destination)
    {
        SetBusy(true);

        float startTime = Time.realtimeSinceStartup;
        Debug.Log($"스테이지 로드 시작: {startTime}");

        yield return _root
            .DOFade(0f, _windowDuration)
            .SetUpdate(true)
            .SetLink(gameObject)
            .WaitForCompletion();

        Debug.Log($"연출 완료: {Time.realtimeSinceStartup - startTime}");
        startTime = Time.realtimeSinceStartup;

        _root.gameObject.SetActive(false);
        yield return UnloadPage();

        Debug.Log($"UI 씬 언로드: {Time.realtimeSinceStartup - startTime}");
        startTime = Time.realtimeSinceStartup;

        // 본체 위치는 유지하여 Town 카메라가 위로 따라가지 않게 한다.
        _visualPosition = _playerVisual.transform.localPosition;
        _visualColor = _playerVisual.color;
        _previousSimulated = _playerBody.simulated;
        _departing = true;
        _playerBody.simulated = false;

        Sequence departure = DOTween.Sequence();

        departure.Join(
            _playerVisual.transform.DOLocalMoveY(
                _visualPosition.y + _departureHeight,
                _departureDuration)
            .SetEase(Ease.InQuad));

        departure.Join(
            _playerVisual.DOFade(0f, _departureDuration));

        departure.SetUpdate(true);
        departure.SetLink(gameObject);

        // 캐릭터의 출발 연출이 끝날 때까지 기다린다.
        yield return departure.WaitForCompletion();

        Debug.Log($"캐릭터 연출 완료: {Time.realtimeSinceStartup - startTime}");
        startTime = Time.realtimeSinceStartup;

        // 로딩 화면을 처음 상태로 표시한다.
        _loadingPanel.SetActive(true);

        // 로드 시작 전에 로딩 화면이 표시될 프레임을 준다.
        yield return null;

        AsyncOperation operation = null;

        try
        {

            // 목적지 스테이지를 비동기로 불러온다.
            operation = SceneManager.LoadSceneAsync(
                destination, LoadSceneMode.Single);

            if (operation != null)
            {
                // 추가: 로딩이 완료되면 실행할 로그를 미리 등록한다.
                operation.completed += completedOperation =>
                {
                    Debug.Log($"씬 로드 완료: {Time.realtimeSinceStartup - startTime}");
                };

                // 게이지가 가득 찬 모습을 표시한 뒤 씬을 전환한다.
                operation.allowSceneActivation = false;
            }
        }
        catch (Exception exception)
        {
            // 씬 로드를 시작하지 못한 원인을 기록한다.
            Debug.LogException(exception, this);
        }

        if (operation == null)
        {
            // 로드를 시작하지 못했다면 로딩 화면을 숨긴다.
            _loadingPanel.SetActive(false);

            // 출발 연출로 바뀐 캐릭터 상태를 복구한다.
            RestoreDeparture();

            if (GameSessionManager.Instance != null)
            {
                // 실패한 출발의 선택 정보를 초기화한다.
                GameSessionManager.Instance.ResetSelection();
            }

            // Town의 UI와 플레이어 조작을 복구한다.
            RestoreTown();
            yield break;
        }

        // 활성화 준비가 끝날 때까지 실제 로딩 진행률을 전달한다.
        // 문구 반복과 게이지 보간은 LoadingScreenUI.Update에서 처리한다.
        while (operation.progress < 0.9f)
        {
            _loadingUI.SetProgress(operation.progress / 0.9f);
            yield return null;
        }

        // 기존 시작 시각을 기준으로 활성화 준비까지 걸린 시간을 기록한다.
        Debug.Log(
            $"씬 활성화 준비 완료: " +
            $"{Time.realtimeSinceStartup - startTime}초");

        // 씬 활성화 준비가 끝났으므로 표시 목표를 100%로 설정한다.
        _loadingUI.SetProgress(1f);

        // 게이지가 목표값까지 부드럽게 채워질 때까지 기다린다.
        // 이 동안 문구와 캐릭터 애니메이션도 계속 갱신될 수 있다.
        while (!_loadingUI.IsFillComplete)
        {
            yield return null;
        }

        // 가득 찬 게이지를 표시할 프레임을 준다.
        yield return null;

        // 실제 씬 전환을 허용한다.
        operation.allowSceneActivation = true;

        // Town이 제거될 때까지 로딩 UI는 별도의 Update로 갱신된다.
        yield return operation;
    }


    // 실패하거나 담당 오브젝트가 제거되면 출격 연출 상태를 복구한다.
    private void RestoreDeparture()
    {
        if (!_departing)
        {
            return;
        }

        _departing = false;

        if (_playerVisual != null)
        {
            _playerVisual.transform.localPosition = _visualPosition;
            _playerVisual.color = _visualColor;
        }

        if (_playerBody != null)
        {
            _playerBody.simulated = _previousSimulated;
        }
    }

    // 창이 닫힌 뒤에만 기존 UI와 플레이어 조작을 복구한다.
    private void RestoreTown()
    {
        if (_townUiGroup != null)
        {
            _townUiGroup.interactable = _previousInteractable;
            _townUiGroup.blocksRaycasts = _previousBlocksRaycasts;
        }

        if (_player != null)
        {
            _player.SetControlEnabled(true);
        }

        _player = null;
        IsOpen = false;
        _busy = false;
    }

    // Town이 전투 씬으로 교체될 때 전역 이벤트 연결을 해제한다.
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(RequestClose);
        }

        RestoreDeparture();

        if (IsOpen)
        {
            RestoreTown();
        }
    }
}
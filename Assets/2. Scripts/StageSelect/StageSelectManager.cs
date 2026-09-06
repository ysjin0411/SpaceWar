using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;
using DG.Tweening;

public class StageSelectManager : MonoBehaviour
{
    // 지도에 배치된 버튼 하나와 그 버튼의 데이터를 묶는다.
    [Serializable]
    private class StageNode
    {
        // 비워두면 아직 준비되지 않은 스테이지로 처리한다.
        public StageData Data;

        // 지도에서 선택할 스테이지 버튼.
        public Button Button;

        // 선택된 스테이지를 감싸는 링 이미지.
        public Image SelectionRing;
    }

    [Header("스테이지 목록")]
    // 지도에 배치한 순서대로 연결한다.
    [SerializeField] StageNode[] _stageNodes;

    [Header("지도 이동")]
    // 배경, 경로, 스테이지 아이콘을 포함하는 이동 대상.
    [SerializeField] RectTransform _mapContent;

    // 선택된 아이콘이 도착할 화면상의 기준 위치.
    [SerializeField] RectTransform _focusAnchor;

    // 지도 이동에 걸리는 시간.
    [SerializeField, Min(0.01f)] float _moveDuration = 0.45f;

    [Header("정보 패널")]
    // StageInfoPanel에 붙어 있는 CanvasGroup.
    [SerializeField] CanvasGroup _infoPanel;

    // 선택한 스테이지 이름과 최단 클리어 기록.
    [SerializeField] TMP_Text _stageNameText;
    [SerializeField] TMP_Text _clearTimeText;

    // 중간 보스의 이름과 아이콘.
    [SerializeField] TMP_Text _midBossNameText;
    [SerializeField] Image _midBossIcon;

    // 최종 보스의 이름과 아이콘.
    [SerializeField] TMP_Text _finalBossNameText;
    [SerializeField] Image _finalBossIcon;

    // 선택한 스테이지로 출격하는 버튼.
    [SerializeField] Button _enterButton;

    // 정보 패널이 나타나는 시간.
    [SerializeField, Min(0.01f)] float _panelFadeDuration = 0.2f;

    [Header("선택 링")]
    // 링이 한 바퀴 회전하는 시간.
    [SerializeField, Min(0.01f)] float _ringRotationDuration = 8f;

    // 링이 밝아졌다 흐려지는 편도 시간.
    [SerializeField, Min(0.01f)] float _ringPulseDuration = 0.65f;

    // 링이 가장 흐려졌을 때의 투명도.
    [SerializeField, Range(0f, 1f)] float _ringMinimumAlpha = 0.45f;

    // 배열에서 선택된 항목의 번호. 저장용 StageIndex와는 다르다.
    private int _selectedNodeIndex = -1;

    // 출격 버튼 등의 중복 실행을 막는다.
    private bool _isLeaving;

    // 이 화면에서 생성한 트윈만 개별적으로 관리한다.
    private Tween _mapTween;
    private Tween _panelTween;
    private Tween _ringRotationTween;
    private Tween _ringPulseTween;

    private void Start()
    {
        // 필요한 연결이 빠졌다면 시작 시점에 알려준다.
        ValidateReferences();

        // 첫 화면에서는 정보 패널과 선택 표시를 숨긴다.
        _infoPanel.alpha = 0f;
        _infoPanel.interactable = false;
        _infoPanel.blocksRaycasts = false;
        _enterButton.interactable = false;

        for (int i = 0; i < _stageNodes.Length; i++)
        {
            StageNode node = _stageNodes[i];

            node.SelectionRing.gameObject.SetActive(false);

            // 데이터가 없는 스테이지는 선택하지 못하게 한다.
            node.Button.interactable = node.Data != null;

            if (node.Data != null)
            {
                // 스테이지 아이콘도 StageData에서 가져온다.
                node.Button.image.sprite = node.Data.StageIcon;
            }
        }

        // EventSystem의 초기 선택 때문에 자동 선택되지 않게 한다.
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private void LateUpdate()
    {
        // 이미 씬을 이동 중이거나 UI 입력 시스템이 없으면 처리하지 않는다.
        if (_isLeaving || EventSystem.current == null)
        {
            return;
        }

        GameObject selected =
            EventSystem.current.currentSelectedGameObject;

        if (selected == null)
        {
            // 기존처럼 첫 방향키 입력으로 사용 가능한 스테이지를 선택한다.
            if (IsAnyArrowKeyDown())
            {
                SelectFirstAvailableStage();
            }
        }
        else
        {
            // 방향키로 이동한 스테이지 정보를 먼저 반영한다.
            // 방향키와 Enter를 함께 눌러도 현재 선택한 스테이지로 진입한다.
            for (int i = 0; i < _stageNodes.Length; i++)
            {
                if (_stageNodes[i].Button.gameObject == selected)
                {
                    SelectingStage(i);
                    break;
                }
            }
        }

        // 일반 Enter와 숫자패드 Enter를 모두 허용한다.
        bool enterPressed =
            Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.KeypadEnter);

        if (!enterPressed)
        {
            return;
        }

        // 스테이지를 선택하고 정보창이 열린 상태에서만 진입한다.
        // 버튼이 비활성 상태라면 키보드로도 실행하지 않는다.
        if (_selectedNodeIndex < 0 ||
            !_infoPanel.gameObject.activeInHierarchy ||
            !_infoPanel.interactable ||
            _infoPanel.alpha <= 0f ||
            !_enterButton.IsActive() ||
            !_enterButton.IsInteractable())
        {
            return;
        }

        // 마우스로 선택하기 버튼을 클릭했을 때와 같은 동작을 실행한다.
        _enterButton.onClick.Invoke();
    }

    public void SelectingStage(int nodeIndex)
    {
        // 잘못된 번호나 이미 선택된 항목은 처리하지 않는다.
        if (_isLeaving ||
            nodeIndex < 0 ||
            nodeIndex >= _stageNodes.Length ||
            _selectedNodeIndex == nodeIndex)
        {
            return;
        }

        StageNode node = _stageNodes[nodeIndex];

        if (node.Data == null || !node.Button.IsInteractable())
        {
            return;
        }

        // 이전 링 연출을 정리한 뒤 새로운 선택을 표시한다.
        HideSelectedRing();
        _selectedNodeIndex = nodeIndex;

        RefreshStageInformation(node.Data);
        MoveMapToNode(node);
        ShowSelectedRing(node.SelectionRing);
        ShowInformationPanel();

        _enterButton.interactable = true;
    }

    private void MoveMapToNode(StageNode node)
    {
        if (_mapTween != null)
        {
            _mapTween.Kill();
        }

        RectTransform parent =
            (RectTransform)_mapContent.parent;

        RectTransform nodeRect =
            (RectTransform)node.Button.transform;

        // 두 위치를 같은 부모 좌표계로 변환한다.
        Vector3 targetPosition =
            parent.InverseTransformPoint(_focusAnchor.position);

        Vector3 nodePosition =
            parent.InverseTransformPoint(
                nodeRect.TransformPoint(nodeRect.rect.center));

        // 세로 배치는 유지하고 가로 위치만 기준점에 맞춘다.
        float destinationX =
            _mapContent.anchoredPosition.x
            + targetPosition.x
            - nodePosition.x;

        _mapTween = _mapContent
            .DOAnchorPosX(destinationX, _moveDuration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true);
    }

    private void RefreshStageInformation(StageData data)
    {
        // 보스는 Stage 씬에서 자동 추출된 프리팹의 정보를 사용한다.
        _stageNameText.text = data.StageName;

        _midBossNameText.text = data.MidBossPrefab.BossName;
        _midBossIcon.sprite = data.MidBossPrefab.BossIcon;

        _finalBossNameText.text = data.FinalBossPrefab.BossName;
        _finalBossIcon.sprite = data.FinalBossPrefab.BossIcon;

        // 배열 순서가 아니라 저장 데이터의 StageIndex로 조회한다.
        float bestClearTime =
            SaveManager.Instance.GetBestClearTime(data.StageIndex);

        _clearTimeText.text = FormatBestClearTime(bestClearTime);
    }

    private void ShowInformationPanel()
    {
        if (_panelTween != null)
        {
            _panelTween.Kill();
        }

        // 첫 선택뿐 아니라 다른 스테이지 선택 시에도 부드럽게 표시한다.
        _infoPanel.alpha = 0f;
        _infoPanel.interactable = true;
        _infoPanel.blocksRaycasts = true;

        _panelTween = _infoPanel
            .DOFade(1f, _panelFadeDuration)
            .SetUpdate(true);
    }

    private void ShowSelectedRing(Image ring)
    {
        // 매번 같은 회전각과 밝기에서 연출을 시작한다.
        ring.gameObject.SetActive(true);
        ring.rectTransform.localRotation = Quaternion.identity;

        Color color = ring.color;
        color.a = 1f;
        ring.color = color;

        // 선택 링을 일정한 속도로 회전시킨다.
        _ringRotationTween = ring.rectTransform
            .DOLocalRotate(
                new Vector3(0f, 0f, -360f),
                _ringRotationDuration,
                RotateMode.FastBeyond360)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Restart)
            .SetUpdate(true);

        // 밝기가 부드럽게 반복해서 변하도록 한다.
        _ringPulseTween = ring
            .DOFade(_ringMinimumAlpha, _ringPulseDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true);
    }

    private void HideSelectedRing()
    {
        if (_ringRotationTween != null)
        {
            _ringRotationTween.Kill();
        }

        if (_ringPulseTween != null)
        {
            _ringPulseTween.Kill();
        }

        if (_selectedNodeIndex >= 0)
        {
            _stageNodes[_selectedNodeIndex]
                .SelectionRing.gameObject.SetActive(false);
        }
    }

    private void SelectFirstAvailableStage()
    {
        // 데이터가 준비된 첫 번째 버튼으로 키보드 포커스를 옮긴다.
        for (int i = 0; i < _stageNodes.Length; i++)
        {
            StageNode node = _stageNodes[i];

            if (node.Data == null || !node.Button.IsInteractable())
            {
                continue;
            }

            EventSystem.current.SetSelectedGameObject(
                node.Button.gameObject);

            SelectingStage(i);
            return;
        }
    }

    private bool IsAnyArrowKeyDown()
    {
        // 기존 방향키 입력 방식을 유지한다.
        return Input.GetKeyDown(KeyCode.UpArrow)
            || Input.GetKeyDown(KeyCode.DownArrow)
            || Input.GetKeyDown(KeyCode.LeftArrow)
            || Input.GetKeyDown(KeyCode.RightArrow);
    }

    public void LoadPlayCharacterSelect()
    {
        if (_isLeaving || _selectedNodeIndex < 0)
        {
            return;
        }

        StageData data = _stageNodes[_selectedNodeIndex].Data;

        // 기존 캐릭터 선택 진입 흐름을 유지한다.
        _isLeaving = true;
        GameSessionManager.Instance.SelectStage(data.StageIndex);
        GameSessionManager.Instance.BeginCharacterSelection(
            CharacterSelectPurpose.Battle);

        SceneManager.LoadScene("CharacterSelect_play");
    }

    public void BackToMainMenu()
    {
        if (_isLeaving)
        {
            return;
        }

        // 선택 정보를 초기화하고 기존 메인 메뉴로 돌아간다.
        _isLeaving = true;
        GameSessionManager.Instance.ResetSelection();

        SceneManager.LoadScene("1.MainMenu");
    }

    private string FormatBestClearTime(float bestClearTime)
    {
        // 기록이 없으면 시간 대신 빈 기록 표시를 사용한다.
        if (bestClearTime <= 0f)
        {
            return "ClearTime: -- : --";
        }

        int totalSeconds = Mathf.FloorToInt(bestClearTime);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        return $"ClearTime: {minutes:00} : {seconds:00}";
    }

    private void ValidateReferences()
    {
        // 저장과 다음 씬 진입에 필요한 매니저를 확인한다.
        if (SaveManager.Instance == null ||
            GameSessionManager.Instance == null)
        {
            throw new InvalidOperationException(
                "SaveManager 또는 GameSessionManager가 없습니다.");
        }

        // 공통 UI 연결을 확인한다.
        if (_mapContent == null ||
            _focusAnchor == null ||
            _infoPanel == null ||
            _stageNameText == null ||
            _clearTimeText == null ||
            _midBossNameText == null ||
            _midBossIcon == null ||
            _finalBossNameText == null ||
            _finalBossIcon == null ||
            _enterButton == null)
        {
            throw new InvalidOperationException(
                "StageSelectManager의 지도 또는 정보 패널 연결이 빠졌습니다.");
        }

        if (_stageNodes == null || _stageNodes.Length == 0)
        {
            throw new InvalidOperationException(
                "Stage Nodes에 스테이지 버튼을 연결해주세요.");
        }

        // 각 항목의 버튼과 준비된 데이터 내용을 확인한다.
        for (int i = 0; i < _stageNodes.Length; i++)
        {
            StageNode node = _stageNodes[i];

            if (node == null ||
                node.Button == null ||
                node.Button.image == null ||
                node.SelectionRing == null)
            {
                throw new InvalidOperationException(
                    $"Stage Nodes의 Element {i} 연결이 빠졌습니다.");
            }

            // 데이터가 없는 항목은 잠긴 스테이지이므로 허용한다.
            if (node.Data == null)
            {
                continue;
            }

            StageData data = node.Data;

            if (data.StageIcon == null ||
                data.MidBossPrefab == null ||
                data.FinalBossPrefab == null ||
                data.MidBossPrefab.BossIcon == null ||
                data.FinalBossPrefab.BossIcon == null)
            {
                throw new InvalidOperationException(
                    $"{data.name}의 스테이지 아이콘 또는 보스 정보를 확인해주세요.");
            }
        }
    }

    private void OnDestroy()
    {
        // 씬을 나갈 때 이 화면에서 생성한 연출만 종료한다.
        if (_mapTween != null)
        {
            _mapTween.Kill();
        }

        if (_panelTween != null)
        {
            _panelTween.Kill();
        }

        if (_ringRotationTween != null)
        {
            _ringRotationTween.Kill();
        }

        if (_ringPulseTween != null)
        {
            _ringPulseTween.Kill();
        }
    }
}
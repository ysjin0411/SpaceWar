using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// AchievementData 한 개를 업적 슬롯 UI에 표시한다.
/// 선택 표시, 상태 표시, 보상 수령 요청을 담당한다.
/// </summary>
public class AchievementSlotView :
    MonoBehaviour,
    ISelectHandler,
    IDeselectHandler,
    IPointerEnterHandler
{
    [Header("슬롯 선택")]

    // 키보드와 컨트롤러로 이 슬롯을 선택하기 위한 Button이다.
    [SerializeField] Button _slotButton;

    // 슬롯이 선택되지 않았을 때 표시할 기본 프레임이다.
    [SerializeField] GameObject _unselectedVisual;

    // 슬롯이 선택됐을 때 표시할 강조 프레임이다.
    [SerializeField] GameObject _highlightedVisual;

    // 보상 수령 완료 상태에서 슬롯 전체를 어둡게 만든다.
    [SerializeField] CanvasGroup _slotCanvasGroup;

    [Header("업적 정보")]

    // AchievementData에 설정된 업적 아이콘을 표시한다.
    [SerializeField] Image _iconImage;

    // 업적 이름을 표시한다.
    [SerializeField] TMP_Text _achievementNameText;

    // 업적 조건 설명을 표시한다.
    [SerializeField] TMP_Text _descriptionText;

    // 보상 종류에 맞는 아이콘을 표시한다.
    [SerializeField] Image _rewardIconImage;

    // 보상 수량과 종류를 표시한다.
    [SerializeField] TMP_Text _rewardValueText;

    // 진행 중, 달성 완료, 보상 수령 완료 상태를 표시한다.
    [SerializeField] TMP_Text _stateText;

    [Header("보상 버튼")]

    // 달성 후 보상을 수령할 때 누르는 버튼이다.
    [SerializeField] Button _claimRewardButton;

    // 보상을 받을 수 있을 때 보상받기 버튼 주변에서 점멸할 효과다.
    [SerializeField] UIAttentionPulse _claimRewardAttentionPulse;

    // 보상 버튼의 현재 상태 문구를 표시한다.
    [SerializeField] TMP_Text _claimRewardButtonText;

    // 현재 이 슬롯에 연결된 업적 Config다.
    AchievementData _achievementData;

    // 현재 슬롯이 선택됐는지 저장한다.
    bool _isSelected;

    // 보상 수령을 완료한 슬롯에 적용할 투명도다.
    const float CompletedAlpha = 0.45f;

    // 동적 생성 후 최초 선택에 사용할 Button이다.
    public Button SlotButton => _slotButton;

    // 현재 슬롯에 연결된 업적 Config다.
    public AchievementData AchievementData =>
        _achievementData;

    private void Awake()
    {
        ValidateReferences();

        // 보상 버튼을 누르면 현재 업적의 보상 수령을 시도한다.
        _claimRewardButton.onClick.AddListener(
            HandleClaimRewardButtonClicked);

        // 슬롯이 처음 생성됐을 때는 선택되지 않은 상태다.
        SetSelected(false);
    }

    /// <summary>
    /// 슬롯에 표시할 업적 Config를 연결한다.
    /// </summary>
    /// <param name="achievementData">표시할 업적 원본 데이터</param>
    public void Bind(
        AchievementData achievementData)
    {
        // Config가 없으면 슬롯 내용을 구성할 수 없다.
        if (achievementData == null)
        {
            throw new ArgumentNullException(
                nameof(achievementData));
        }

        // 이후 상태 갱신과 보상 수령에 사용할 Config를 저장한다.
        _achievementData = achievementData;

        // 업적의 고정 표시 정보를 Config에서 가져온다.
        _achievementNameText.text =
            achievementData.AchievementName;

        _descriptionText.text =
            achievementData.Description;

        // Config에 아이콘이 있을 때만 Image를 표시한다.
        _iconImage.sprite =
            achievementData.Icon;

        _iconImage.preserveAspect = true;
        _iconImage.enabled =
            achievementData.Icon != null;

        // 보상 종류와 수량을 텍스트로 구성한다.
        _rewardValueText.text =
            GetRewardText(achievementData);

        // 실제 저장 데이터를 기준으로 현재 상태를 표시한다.
        RefreshState();
    }

    /// <summary>
    /// 저장된 달성 및 보상 수령 기록을 다시 읽어
    /// 슬롯의 표시 상태를 갱신한다.
    /// </summary>
    public void RefreshState()
    {
        // 아직 Config가 연결되지 않았다면 갱신할 수 없다.
        if (_achievementData == null)
        {
            return;
        }

        if (AchievementManager.Instance == null)
        {
            throw new InvalidOperationException(
                "AchievementManager가 존재하지 않습니다.");
        }

        AchievementState achievementState =
            AchievementManager.Instance.GetAchievementState(
                _achievementData);

        // 보상 수령 가능 상태에서만 버튼 테두리를 점멸한다.
        bool isRewardAvailable =
            achievementState == AchievementState.RewardAvailable;

        switch (achievementState)
        {
            case AchievementState.InProgress:

                // 미달성 상태는 밝게 표시하지만
                // 보상 수령 버튼은 사용할 수 없다.
                _slotCanvasGroup.alpha = 1f;
                _stateText.text = "진행 중";
                _claimRewardButtonText.text = "미달성";
                _claimRewardButton.interactable = false;
                break;

            case AchievementState.RewardAvailable:

                // 조건을 달성했으므로 밝게 강조하고
                // 보상 수령 버튼을 활성화한다.
                _slotCanvasGroup.alpha = 1f;
                _stateText.text = "달성 완료";
                _claimRewardButtonText.text = "보상받기";
                _claimRewardButton.interactable = true;
                break;

            case AchievementState.Completed:

                // 보상을 받은 업적은 어둡게 표시하고
                // 버튼을 완료 상태로 비활성화한다.
                _slotCanvasGroup.alpha =
                    CompletedAlpha;

                _stateText.text = "보상 수령 완료";
                _claimRewardButtonText.text = "완료";
                _claimRewardButton.interactable = false;
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(achievementState),
                    achievementState,
                    "지원하지 않는 업적 상태입니다.");
        }

        // 미달성 또는 보상 수령 완료 상태에서는 꺼지고,
        // 보상을 받을 수 있을 때만 점멸한다.
        _claimRewardAttentionPulse.SetAttention(
            isRewardAvailable);
    }

    /// <summary>
    /// 보상 종류에 맞는 표시 문자열을 생성한다.
    /// </summary>
    private string GetRewardText(
        AchievementData achievementData)
    {
        switch (achievementData.RewardType)
        {
            case AchievementRewardType.Gold:
                return
                    $"{achievementData.RewardValue:N0} Gold";

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(achievementData.RewardType),
                    achievementData.RewardType,
                    "지원하지 않는 업적 보상 종류입니다.");
        }
    }

    /// <summary>
    /// 보상받기 버튼을 눌렀을 때 현재 업적의 보상을 요청한다.
    /// </summary>
    private void HandleClaimRewardButtonClicked()
    {
        // Bind가 완료되지 않은 슬롯에서는 처리하지 않는다.
        if (_achievementData == null)
        {
            return;
        }

        if (AchievementManager.Instance == null)
        {
            throw new InvalidOperationException(
                "AchievementManager가 존재하지 않습니다.");
        }

        // 실제 지급, 중복 방지, 저장은 AchievementManager와
        // SaveManager가 처리한다.
        AchievementManager.Instance.TryClaimReward(
            _achievementData);
    }

    /// <summary>
    /// 키보드 또는 컨트롤러로 슬롯이 선택됐을 때 호출된다.
    /// </summary>
    public void OnSelect(
        BaseEventData eventData)
    {
        SetSelected(true);
    }

    /// <summary>
    /// 다른 UI로 선택이 이동했을 때 호출된다.
    /// </summary>
    public void OnDeselect(
        BaseEventData eventData)
    {
        SetSelected(false);
    }

    /// <summary>
    /// 마우스 포인터가 슬롯 위에 올라오면
    /// 키보드 선택과 동일한 선택 상태로 만든다.
    /// </summary>
    public void OnPointerEnter(
        PointerEventData eventData)
    {
        _slotButton.Select();
    }

    /// <summary>
    /// 슬롯의 기본 프레임과 강조 프레임을 전환한다.
    /// </summary>
    private void SetSelected(bool isSelected)
    {
        _isSelected = isSelected;

        _unselectedVisual.SetActive(
            !_isSelected);

        _highlightedVisual.SetActive(
            _isSelected);
    }

    /// <summary>
    /// Inspector에서 필요한 UI 연결이 빠졌는지 확인한다.
    /// </summary>
    private void ValidateReferences()
    {
        if (_slotButton == null)
        {
            throw new InvalidOperationException(
                $"{name}의 Slot Button이 연결되지 않았습니다.");
        }

        if (_unselectedVisual == null ||
            _highlightedVisual == null)
        {
            throw new InvalidOperationException(
                $"{name}의 슬롯 프레임이 모두 연결되지 않았습니다.");
        }

        if (_slotCanvasGroup == null)
        {
            throw new InvalidOperationException(
                $"{name}의 Slot CanvasGroup이 연결되지 않았습니다.");
        }

        if (_iconImage == null ||
            _rewardIconImage == null)
        {
            throw new InvalidOperationException(
                $"{name}의 업적 아이콘 또는 보상 아이콘이 연결되지 않았습니다.");
        }

        if (_achievementNameText == null ||
            _descriptionText == null ||
            _rewardValueText == null ||
            _stateText == null)
        {
            throw new InvalidOperationException(
                $"{name}의 업적 텍스트가 모두 연결되지 않았습니다.");
        }

        if (_claimRewardButton == null ||
            _claimRewardButtonText == null)
        {
            throw new InvalidOperationException(
                $"{name}의 보상 버튼이 연결되지 않았습니다.");
        }

        if (_claimRewardAttentionPulse == null)
        {
            throw new InvalidOperationException(
                $"{name}에 보상 버튼 알림 효과가 연결되지 않았습니다.");
        }
    }

    private void OnDestroy()
    {
        // 파괴된 슬롯 함수가 버튼에 남아 있지 않도록 해제한다.
        if (_claimRewardButton != null)
        {
            _claimRewardButton.onClick.RemoveListener(
                HandleClaimRewardButtonClicked);
        }
    }

    private void OnEnable()
    {
        if (AchievementManager.Instance == null)
        {
            return;
        }

        // 다른 슬롯에서 보상을 받더라도 현재 슬롯의 상태를 다시 확인한다.
        AchievementManager.Instance.AchievementStateChanged +=
            RefreshState;

        // 업적창을 다시 열었을 때 점멸 효과를 포함한 상태를 복구한다.
        RefreshState();
    }

    private void OnDisable()
    {
        // 비활성화된 슬롯이 계속 이벤트를 받지 않도록 해제한다.
        if (AchievementManager.Instance != null)
        {
            AchievementManager.Instance.AchievementStateChanged -=
                RefreshState;
        }
    }
}
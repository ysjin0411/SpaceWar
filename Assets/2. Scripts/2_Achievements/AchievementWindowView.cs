using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메인메뉴의 업적창을 DOTween 연출과 함께 열고 닫는다.
/// </summary>
public class AchievementWindowView : MonoBehaviour
{
    [Header("업적창 UI")]

    // 크기 연출을 적용할 1200 x 800 크기의 업적창이다.
    [SerializeField] RectTransform _achievementWindow;

    // 업적창의 투명도와 입력 가능 상태를 관리한다.
    [SerializeField] CanvasGroup _achievementWindowCanvasGroup;

    // 업적창 뒤에 표시되는 검은색 반투명 배경이다.
    [SerializeField] Image _dimmedBackgroundImage;

    // 업적창 우측 상단에 현재 보유 골드를 표시하는 텍스트다.
    [SerializeField] TMP_Text _currentGoldText;

    [Header("업적 슬롯 생성")]

    // 동적으로 생성할 업적 슬롯 프리팹이다.
    [SerializeField] AchievementSlotView _achievementSlotPrefab;

    // 생성된 업적 슬롯이 들어갈 ScrollView의 Content다.
    [SerializeField] Transform _slotContainer;

    // 생성된 모든 업적 슬롯을 보관한다.
    readonly List<AchievementSlotView> _achievementSlots = new();

    [Header("열기/닫기 연출")]

    // 업적창이 열리기 시작할 때의 크기다.
    [Range(0.1f, 1f)]
    [SerializeField] float _startScale = 0.8f;

    // 업적창이 열리는 시간이다.
    [Min(0.01f)]
    [SerializeField] float _openDuration = 0.3f;

    // 업적창이 닫히는 시간이다.
    [Min(0.01f)]
    [SerializeField] float _closeDuration = 0.2f;

    // 원래 설정된 어두운 배경의 알파값이다.
    float _dimmedTargetAlpha;

    // 현재 실행 중인 업적창 연출이다.
    Sequence _windowTween;

    // 업적 창이 완전히 닫히거나 비활성화되었음을 알린다.
    public event Action Closed;

    private void Awake()
    {
        // 업적창의 필수 UI 연결을 확인한다.
        if (_achievementWindow == null ||
            _achievementWindowCanvasGroup == null ||
            _dimmedBackgroundImage == null)
        {
            throw new InvalidOperationException(
                $"{name}의 업적창 UI 연결이 빠졌습니다.");
        }

        // 처음 활성화될 때 Inspector의 배경 투명도를 저장한다.
        // 여기서는 오브젝트를 켜거나 끄지 않는다.
        _dimmedTargetAlpha =
            _dimmedBackgroundImage.color.a;
    }

    /// <summary>
    /// 업적창이 활성화될 때 현재 골드를 표시하고
    /// 이후 발생하는 골드 변경 이벤트를 구독한다.
    /// </summary>
    private void OnEnable()
    {
        if (_currentGoldText == null)
        {
            throw new InvalidOperationException(
                $"{name}의 AchievementWindowView에 Current Gold Text가 연결되지 않았습니다.");
        }

        if (SaveManager.Instance == null)
        {
            throw new InvalidOperationException(
                "현재 골드를 표시할 SaveManager가 존재하지 않습니다.");
        }

        // 업적 보상 수령 등으로 골드가 변경되면
        // 열린 업적창의 숫자도 즉시 갱신한다.
        SaveManager.Instance.GoldChanged +=
            UpdateCurrentGoldText;

        // 창을 다시 열었을 때 저장된 최신 골드를 바로 표시한다.
        UpdateCurrentGoldText(
            SaveManager.Instance.CurrentGold);
    }

    /// <summary>
    /// SaveManager가 전달한 현재 보유 골드를 화면에 표시한다.
    /// </summary>
    private void UpdateCurrentGoldText(int currentGold)
    {
        // 1000 이상의 숫자는 1,000 형식으로 표시한다.
        _currentGoldText.text =
            currentGold.ToString("N0");
    }

    /// <summary>
    /// 업적 버튼을 누르면 업적창을 확대하며 표시한다.
    /// </summary>
    public void OpenWindow()
    {
        // 닫힌 오브젝트를 먼저 활성화해야 Tween을 재생할 수 있다.
        gameObject.SetActive(true);

        // 처음 열었을 때 Database 개수만큼 업적 슬롯을 생성한다.
        CreateAchievementSlotsIfNeeded();

        // 이전 열기 또는 닫기 연출이 남아 있다면 중지한다.
        _windowTween?.Kill();

        // 작은 크기와 투명한 상태에서 연출을 시작한다.
        _achievementWindow.localScale =
            Vector3.one * _startScale;

        _achievementWindowCanvasGroup.alpha = 0f;
        _achievementWindowCanvasGroup.interactable = false;
        _achievementWindowCanvasGroup.blocksRaycasts = false;

        // 어두운 배경도 투명한 상태에서 시작한다.
        _dimmedBackgroundImage.color =
            new Color(
                _dimmedBackgroundImage.color.r,
                _dimmedBackgroundImage.color.g,
                _dimmedBackgroundImage.color.b,
                0f);

        _windowTween = DOTween.Sequence();

        // 업적창을 원래 크기로 확대한다.
        _windowTween.Append(
            _achievementWindow
                .DOScale(1f, _openDuration)
                .SetEase(Ease.OutBack));

        // 확대 연출과 동시에 업적창을 선명하게 만든다.
        _windowTween.Join(
            _achievementWindowCanvasGroup
                .DOFade(1f, _openDuration)
                .SetEase(Ease.OutQuad));

        // 확대 연출과 동시에 뒤쪽 배경을 어둡게 만든다.
        _windowTween.Join(
            _dimmedBackgroundImage
                .DOFade(
                    _dimmedTargetAlpha,
                    _openDuration)
                .SetEase(Ease.OutQuad));

        _windowTween.OnComplete(() =>
        {
            // 열기 연출이 끝난 뒤 업적창 입력을 허용한다.
            _achievementWindowCanvasGroup.interactable = true;
            _achievementWindowCanvasGroup.blocksRaycasts = true;
            _windowTween = null;
        });
    }

    /// <summary>
    /// Back 버튼을 누르면 업적창을 축소하며 숨긴다.
    /// </summary>
    public void CloseWindow()
    {
        // 닫히는 동안 업적 슬롯을 누를 수 없게 한다.
        _achievementWindowCanvasGroup.interactable = false;
        _achievementWindowCanvasGroup.blocksRaycasts = false;

        // 기존 연출이 실행 중이라면 현재 위치에서 중지한다.
        _windowTween?.Kill();

        _windowTween = DOTween.Sequence();

        // 업적창을 가운데 방향으로 축소한다.
        _windowTween.Append(
            _achievementWindow
                .DOScale(
                    _startScale,
                    _closeDuration)
                .SetEase(Ease.InBack));

        // 축소와 동시에 업적창을 투명하게 만든다.
        _windowTween.Join(
            _achievementWindowCanvasGroup
                .DOFade(0f, _closeDuration)
                .SetEase(Ease.InQuad));

        // 축소와 동시에 어두운 배경을 제거한다.
        _windowTween.Join(
            _dimmedBackgroundImage
                .DOFade(0f, _closeDuration)
                .SetEase(Ease.InQuad));

        _windowTween.OnComplete(() =>
        {
            _windowTween = null;

            // 닫기 연출이 끝난 다음 업적창 전체를 비활성화한다.
            gameObject.SetActive(false);
        });
    }

    /// <summary>
    /// 업적창을 처음 열었을 때 Database 개수만큼 슬롯을 생성한다.
    /// 이미 생성했다면 다시 생성하지 않는다.
    /// </summary>
    private void CreateAchievementSlotsIfNeeded()
    {
        // 슬롯이 이미 생성되어 있다면 중복 생성하지 않는다.
        if (_achievementSlots.Count > 0)
        {
            return;
        }

        if (AchievementManager.Instance == null)
        {
            throw new InvalidOperationException(
                "AchievementManager가 존재하지 않습니다.");
        }

        if (_achievementSlotPrefab == null)
        {
            throw new InvalidOperationException(
                "Achievement Slot Prefab이 연결되지 않았습니다.");
        }

        if (_slotContainer == null)
        {
            throw new InvalidOperationException(
                "Achievement Slot Container가 연결되지 않았습니다.");
        }

        // AchievementManager에 연결된 업적 Database를 가져온다.
        AchievementDatabase database =
            AchievementManager.Instance.Database;

        for (int index = 0;
             index < database.Count;
             index++)
        {
            // Database의 현재 순서에 해당하는 업적 데이터를 가져온다.
            AchievementData achievementData =
                database.GetAchievementDataAt(index);

            // Content 아래에 새로운 업적 슬롯을 생성한다.
            AchievementSlotView achievementSlot =
                Instantiate(
                    _achievementSlotPrefab,
                    _slotContainer);

            // Hierarchy에서 슬롯 순서를 쉽게 확인할 수 있게 이름을 지정한다.
            achievementSlot.name =
                $"AchievementSlot_{index + 1}";

            // 생성된 슬롯에 업적 Config 데이터를 전달한다.
            achievementSlot.Bind(achievementData);

            // 나중에 전체 슬롯을 갱신할 수 있도록 목록에 저장한다.
            _achievementSlots.Add(achievementSlot);
        }
    }

    // 업적 창이 꺼지면 구독과 연출을 정리하고 닫힘을 알린다.
    private void OnDisable()
    {
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.GoldChanged -= UpdateCurrentGoldText;
        }

        // 진행 중인 연출이 비활성화 이후에 실행되지 않게 한다.
        if (_windowTween != null)
        {
            _windowTween.Kill();
            _windowTween = null;
        }

        // 닫기 연출이 끝나 실제 창이 꺼진 시점에 전달된다.
        if (Closed != null)
        {
            Closed.Invoke();
        }
    }
}
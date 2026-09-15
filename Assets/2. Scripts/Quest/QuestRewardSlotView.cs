using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 보상 한 항목의 아이콘, 이름, 수량을 표시한다.
public class QuestRewardSlotView : MonoBehaviour
{
    // 보상을 나타내는 이미지.
    [SerializeField] private Image _iconImage;

    // 보상 이름을 표시하는 텍스트.
    [SerializeField] private TMP_Text _nameText;

    // 지급 수량을 표시하는 텍스트.
    [SerializeField] private TMP_Text _amountText;

    // 프리팹이 비활성 상태여도 연결 상태를 검사할 수 있다.
    public bool IsConfigured =>
        _iconImage != null &&
        _nameText != null &&
        _amountText != null;

    // 전달받은 보상 데이터로 한 줄을 채운다.
    public void Bind(QuestRewardData reward)
    {
        // 아이콘이 없으면 흰 사각형이 표시되지 않도록 숨긴다.
        _iconImage.sprite = reward.Icon;
        _iconImage.enabled = reward.Icon != null;
        _iconImage.preserveAspect = true;

        // 보상 표시가 대화 진행 등의 클릭을 가로채지 않게 한다.
        _iconImage.raycastTarget = false;
        _nameText.raycastTarget = false;
        _amountText.raycastTarget = false;

        _nameText.text = reward.DisplayName;
        _amountText.text = $"× {reward.Amount:N0}";
    }
}
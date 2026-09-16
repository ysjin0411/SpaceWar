using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 슬롯 위에 올린 아이템의 상세 정보를 표시한다.
public class InventoryTooltipView : MonoBehaviour
{
    // TooltipTitleImage 아래 WeaponIcon의 Image다.
    [SerializeField] private Image _icon;

    // TooltipTitleText에 연결할 아이템 이름이다.
    [SerializeField] private TMP_Text _nameText;

    // ItemInfoText에 연결할 아이템 설명이다.
    [SerializeField] private TMP_Text _descriptionText;

    // ItemTypeText에 연결할 아이템 분류다.
    [SerializeField] private TMP_Text _typeText;

    // 초기 연결과 툴팁의 기준점을 설정한다.
    public void Initialize()
    {
        if (_icon == null || _nameText == null ||
            _descriptionText == null || _typeText == null)
        {
            throw new InvalidOperationException(
                "툴팁의 아이콘과 텍스트를 모두 연결해주세요.");
        }

        // 툴팁의 왼쪽 위가 배치 기준점이 된다.
        ((RectTransform)transform).pivot = new Vector2(0f, 1f);

        // 툴팁이 슬롯의 마우스 진입·이탈 판정을 가리지 않게 한다.
        foreach (Graphic graphic in GetComponentsInChildren<Graphic>(true))
        {
            graphic.raycastTarget = false;
        }

        Hide();
    }

    // 슬롯 중심을 툴팁의 왼쪽 위로 삼아 정보를 표시한다.
    public void Show(Item item, RectTransform slot)
    {
        _icon.sprite = item.Icon;
        _icon.enabled = item.Icon != null;
        _icon.preserveAspect = true;

        _nameText.text = item.DisplayName;
        _descriptionText.text = item.Description;

        switch (item.Type)
        {
            case ItemType.Material:
                _typeText.text = "재료";
                break;

            case ItemType.Quest:
                _typeText.text = "퀘스트 아이템";
                break;

            case ItemType.Consumable:
                _typeText.text = "소비 아이템";
                break;

            default:
                _typeText.text = item.Type.ToString();
                break;
        }

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        // 같은 Canvas 안에서 슬롯의 실제 중심 월드 좌표를 사용한다.
        transform.position = slot.TransformPoint(slot.rect.center);
    }

    // 빈 슬롯, 마우스 이탈, 드래그 시작 시 툴팁을 숨긴다.
    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
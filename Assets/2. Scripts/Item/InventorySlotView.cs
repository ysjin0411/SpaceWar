using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 슬롯의 표시와 마우스 이벤트를 담당한다.
[RequireComponent(typeof(Image))]
public class InventorySlotView : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IDropHandler
{
    // 슬롯 자식 Icon의 Image다.
    [SerializeField] private Image _icon;

    // 슬롯 자식 Count의 수량 텍스트다.
    [SerializeField] private TMP_Text _countText;

    // 슬롯의 실제 처리 담당자와 저장 목록의 인덱스다.
    private InventoryPresenter _owner;
    private int _index;

    // 툴팁을 슬롯 중심에 배치할 때 사용하는 영역이다.
    public RectTransform Rect => (RectTransform)transform;

    // 비활성 Canvas 아래에서도 외부에서 명시적으로 초기화한다.
    public void Initialize(InventoryPresenter owner, int index)
    {
        if (_icon == null || _countText == null)
        {
            throw new InvalidOperationException(
                $"{name}: Icon과 Count를 연결해주세요.");
        }

        _owner = owner;
        _index = index;

        // 빈 슬롯도 드롭을 받을 수 있어야 한다.
        GetComponent<Image>().raycastTarget = true;

        // 자식들이 슬롯의 마우스 판정을 가로채지 않게 한다.
        _icon.raycastTarget = false;
        _countText.raycastTarget = false;
        _icon.preserveAspect = true;
    }

    // 보유 아이템과 수량을 표시하고, 빈 슬롯은 내용만 숨긴다.
    public void Bind(Item item, int count)
    {
        bool occupied = item != null && count > 0;

        _icon.sprite = occupied ? item.Icon : null;
        _icon.gameObject.SetActive(occupied);
        _countText.gameObject.SetActive(occupied);
        _countText.text = occupied ? count.ToString("N0") : string.Empty;
    }

    // 마우스 진입 시 아이템이 있으면 툴팁을 요청한다.
    public void OnPointerEnter(PointerEventData eventData)
    {
        _owner.Hover(_index);
    }

    // 마우스가 슬롯을 벗어나면 해당 툴팁을 숨긴다.
    public void OnPointerExit(PointerEventData eventData)
    {
        _owner.Leave(_index);
    }

    // 왼쪽 버튼으로 아이템을 끌기 시작한다.
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            _owner.BeginDrag(_index, eventData.position);
        }
    }

    // 드래그 이미지를 현재 커서 위치로 이동한다.
    public void OnDrag(PointerEventData eventData)
    {
        _owner.MoveDrag(eventData.position);
    }

    // 드롭 성공 여부와 관계없이 드래그 표시를 정리한다.
    public void OnEndDrag(PointerEventData eventData)
    {
        _owner.EndDrag();
    }

    // 마우스를 놓은 대상이 이 슬롯일 때만 교환을 요청한다.
    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            _owner.Drop(_index);
        }
    }
}
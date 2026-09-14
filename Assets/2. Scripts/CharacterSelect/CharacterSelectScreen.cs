using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.U2D.Animation;
using DG.Tweening;

public class CharacterSelectScreen : MonoBehaviour
{
    [Serializable]
    private class CharacterSlot
    {
        // 슬롯 루트에 있는 선택 버튼.
        public Button button;

        // 프레임, 캐릭터, 텍스트, 무기 아이콘이 들어 있는 부모.
        // 선택 여부에 따른 밝기를 이 안의 Graphic에 적용한다.
        public RectTransform cardVisual;

        // 선택한 슬롯 뒤에서 회전하는 링.
        public Image selectionRing;

        // 첫 번째 장착 무기의 Idle을 표시할 이미지.
        public Image characterPreview;

        // 캐릭터 이름과 현재 레벨.
        public TMP_Text nameText;
        public TMP_Text levelText;

        // 장착 순서대로 연결하는 무기 아이콘 3개.
        public Image[] weaponIcons = new Image[3];

        // 아래 값은 실행 중 사용하는 상태이며 Inspector에는 저장하지 않는다.
        [NonSerialized] public Sprite[] idleFrames;
        [NonSerialized] public int frameIndex;
        [NonSerialized] public float frameTimer;
        [NonSerialized] public Vector3 originalScale;
        [NonSerialized] public Graphic[] graphics;
        [NonSerialized] public Color[] originalColors;
        [NonSerialized] public float brightness;
        [NonSerialized] public Tween scaleTween;
        [NonSerialized] public Tween brightnessTween;
    }

    [Header("데이터")]
    // 캐릭터 배열 순서와 슬롯 배열 순서를 맞춘다.
    [SerializeField] CharacterDatabase _characterDatabase;

    // 저장된 무기 ID를 실제 WeaponData로 변환한다.
    [SerializeField] WeaponDatabase _weaponDatabase;

    [Header("캐릭터 슬롯")]
    // 0번 솔져, 1번 워리어. 준비되지 않은 잠금 슬롯은 넣지 않는다.
    [SerializeField] CharacterSlot[] _slots;

    [Header("Idle 재생")]
    // 1초 동안 재생할 스프라이트 수. 선택 여부와 관계없이 적용된다.
    [SerializeField, Min(0.1f)] float _idleFps = 10f;

    [Header("선택 연출")]
    // Inspector에 설정한 원래 Scale에 곱하는 선택 배율.
    [SerializeField, Min(1f)] float _selectedScale = 1.06f;

    // 확대와 밝기 변화에 걸리는 시간.
    [SerializeField, Min(0.01f)] float _transitionDuration = 0.18f;

    // 비선택 상태에서 원래 색상에 곱하는 밝기.
    [SerializeField, Range(0f, 1f)] float _unselectedBrightness = 0.7f;

    // 선택 상태에서 원래 색상에 곱하는 밝기.
    [SerializeField, Range(0f, 1f)] float _selectedBrightness = 1f;

    // 선택 링이 한 바퀴 회전하는 시간.
    [SerializeField, Min(0.01f)] float _ringRotationDuration = 8f;

    // 현재 선택된 슬롯 번호와 씬 이동 상태.
    private int _selectedIndex = -1;
    private bool _isLeaving;
    private bool _initialized;

    // 현재 선택된 슬롯의 링 회전만 관리한다.
    private Tween _ringTween;

    // 선택 결과 저장 후 Town에서 출격 연출을 시작하도록 요청한다.
    public event Action<string> DepartureRequested;
    // 창 내부에서 스테이지 선택 화면으로 돌아가도록 요청한다.
    public event Action StageSelectionRequested;
    // 상점용 캐릭터 선택이 끝나면 구매·장착 화면 열기를 요청한다.
    public event Action EquipmentRequested;

    // 상점의 첫 화면에서 뒤로 가기를 누르면 서비스 창 닫기를 요청한다.
    public event Action CloseRequested;

    private void Start()
    {
        // 데이터와 슬롯의 기본 연결을 확인한다.
        if (_characterDatabase == null ||
            _weaponDatabase == null ||
            SaveManager.Instance == null ||
            GameSessionManager.Instance == null)
        {
            throw new InvalidOperationException(
                "캐릭터 선택 화면의 데이터베이스 또는 매니저가 없습니다.");
        }

        if (_slots == null ||
            _slots.Length == 0 ||
            _slots.Length != _characterDatabase.Count)
        {
            throw new InvalidOperationException(
                "Slots 개수를 CharacterDatabase의 캐릭터 개수와 맞춰주세요.");
        }

        // 각 슬롯에 저장 데이터와 Idle 스프라이트를 준비한다.
        for (int i = 0; i < _slots.Length; i++)
        {
            InitializeSlot(i);
        }

        _initialized = true;

        // 화면에 들어오자마자 특정 캐릭터가 자동 선택되지 않게 한다.
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    // 카드에 실제 장착 상태를 표시하고, 재생할 Idle 이미지를 준비한다.
    private void InitializeSlot(int index)
    {
        CharacterSlot slot = _slots[index];

        // 카드 표시에 필요한 UI 연결을 확인한다.
        if (slot == null ||
            slot.button == null ||
            slot.cardVisual == null ||
            slot.selectionRing == null ||
            slot.characterPreview == null ||
            slot.nameText == null ||
            slot.levelText == null ||
            slot.weaponIcons == null ||
            slot.weaponIcons.Length != 3)
        {
            throw new InvalidOperationException(
                $"Slots Element {index}의 UI 연결을 확인해주세요.");
        }

        CharacterData character =
            _characterDatabase.GetCharacterDataAt(index);

        CharacterLoadoutSaveData loadout =
            SaveManager.Instance.GetOrCreateCharacterLoadout(character.Id);

        slot.nameText.text = character.CharacterName;
        slot.levelText.text = loadout.level.ToString();

        // 캐릭터 외형은 첫 번째 슬롯에 실제로 장착한 무기를 기준으로 한다.
        WeaponData firstWeapon = null;

        for (int i = 0; i < slot.weaponIcons.Length; i++)
        {
            Image icon = slot.weaponIcons[i];

            if (icon == null)
            {
                throw new InvalidOperationException(
                    $"{character.name}의 Weapon Icons {i}번이 비어 있습니다.");
            }

            string weaponId = string.Empty;

            if (loadout.equippedWeaponIds != null &&
                i < loadout.equippedWeaponIds.Length)
            {
                weaponId = loadout.equippedWeaponIds[i];
            }

            // 빈 장착 슬롯에는 기본 무기를 대신 표시하지 않는다.
            if (string.IsNullOrWhiteSpace(weaponId))
            {
                icon.sprite = null;
                icon.enabled = false;
                continue;
            }

            WeaponData weapon = _weaponDatabase.GetWeaponData(weaponId);

            icon.sprite = weapon.Icon;
            icon.preserveAspect = true;
            icon.enabled = weapon.Icon != null;

            if (i == 0)
            {
                firstWeapon = weapon;
            }
        }

        // 처음부터 애니메이션을 재생하도록 시간을 초기화한다.
        slot.frameIndex = 0;
        slot.frameTimer = 0f;

        if (firstWeapon == null)
        {
            // 첫 슬롯이 비어 있으면 기본 캐릭터 이미지를 표시한다.
            // 한 장짜리 배열이므로 기존 Update에서도 그대로 처리할 수 있다.
            slot.idleFrames = new Sprite[] { character.Icon };
        }
        else
        {
            // 첫 번째 장착 무기에 해당하는 캐릭터 애니메이션을 가져온다.
            SpriteLibraryAsset library = firstWeapon.PlayerSpriteLibrary;
            string category = character.PreviewIdleCategory;

            if (library == null || string.IsNullOrWhiteSpace(category))
            {
                throw new InvalidOperationException(
                    $"{character.name}의 Sprite Library와 Idle 카테고리를 확인해주세요.");
            }

            // 기존과 같은 이름 순서로 애니메이션 프레임을 준비한다.
            string[] labels = library
                .GetCategoryLabelNames(category)
                .OrderBy(label => label, StringComparer.Ordinal)
                .ToArray();

            if (labels.Length == 0)
            {
                throw new InvalidOperationException(
                    $"{library.name}에 {category} 프레임이 없습니다.");
            }

            slot.idleFrames = new Sprite[labels.Length];

            for (int i = 0; i < labels.Length; i++)
            {
                slot.idleFrames[i] =
                    library.GetSprite(category, labels[i]);

                if (slot.idleFrames[i] == null)
                {
                    throw new InvalidOperationException(
                        $"{library.name}의 {category}/{labels[i]} 이미지가 비어 있습니다.");
                }
            }

            // 무기별로 설정했던 미리보기 크기와 위치를 유지한다.
            RectTransform previewRect = slot.characterPreview.rectTransform;

            previewRect.localScale =
                Vector3.one * firstWeapon.CharacterSelectPreviewScale;

            previewRect.anchoredPosition =
                firstWeapon.CharacterSelectPreviewOffset;
        }

        // 준비한 첫 이미지를 표시한다. 이후 재생은 기존 Update가 담당한다.
        slot.characterPreview.sprite = slot.idleFrames[0];
        slot.characterPreview.preserveAspect = true;
        slot.characterPreview.enabled = slot.idleFrames[0] != null;

        // 기존 카드 선택 확대와 밝기 효과에 필요한 원본 값을 저장한다.
        slot.originalScale = slot.button.transform.localScale;
        slot.graphics = slot.cardVisual.GetComponentsInChildren<Graphic>(true);
        slot.originalColors = new Color[slot.graphics.Length];

        for (int i = 0; i < slot.graphics.Length; i++)
        {
            slot.originalColors[i] = slot.graphics[i].color;
        }

        slot.selectionRing.gameObject.SetActive(false);
        SetBrightness(slot, _unselectedBrightness);
    }

    private void Update()
    {
        if (!_initialized || _isLeaving)
        {
            return;
        }

        // 선택되지 않은 캐릭터도 같은 속도로 Idle을 계속 재생한다.
        float interval = 1f / Mathf.Max(0.1f, _idleFps);

        foreach (CharacterSlot slot in _slots)
        {
            slot.frameTimer += Time.unscaledDeltaTime;

            int steps = Mathf.FloorToInt(slot.frameTimer / interval);

            if (steps == 0)
            {
                continue;
            }

            slot.frameTimer -= steps * interval;
            slot.frameIndex =
                (slot.frameIndex + steps) % slot.idleFrames.Length;

            slot.characterPreview.sprite =
                slot.idleFrames[slot.frameIndex];
        }

        // 방향키로 버튼 사이를 이동해도 선택 연출을 갱신한다.
        if (EventSystem.current == null)
        {
            return;
        }

        GameObject selected =
            EventSystem.current.currentSelectedGameObject;

        if (selected == null)
        {
            if (Input.GetKeyDown(KeyCode.UpArrow) ||
                Input.GetKeyDown(KeyCode.DownArrow) ||
                Input.GetKeyDown(KeyCode.LeftArrow) ||
                Input.GetKeyDown(KeyCode.RightArrow))
            {
                SelectingCharacter(0);
            }

            return;
        }

        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i].button.gameObject == selected)
            {
                SelectingCharacter(i);
                break;
            }
        }
    }

    public void SelectingCharacter(int index)
    {
        if (!_initialized || _isLeaving ||
            index < 0 || index >= _slots.Length ||
            index == _selectedIndex)
        {
            return;
        }

        if (!_slots[index].button.IsInteractable())
        {
            return;
        }

        // 이전 링 회전을 중단하고 이전 슬롯을 원래 상태로 돌린다.
        if (_ringTween != null)
        {
            _ringTween.Kill();
        }

        if (_selectedIndex >= 0)
        {
            ApplySelection(_slots[_selectedIndex], false);
        }

        _selectedIndex = index;
        CharacterSlot selectedSlot = _slots[index];

        ApplySelection(selectedSlot, true);

        // 마우스로 선택한 결과도 키보드 포커스에 반영한다.
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(
                selectedSlot.button.gameObject);
        }

        selectedSlot.selectionRing.rectTransform.localRotation =
            Quaternion.identity;

        _ringTween = selectedSlot.selectionRing.rectTransform
            .DOLocalRotate(
                new Vector3(0f, 0f, -360f),
                _ringRotationDuration,
                RotateMode.FastBeyond360)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Restart)
            .SetUpdate(true);
    }

    private void ApplySelection(CharacterSlot slot, bool selected)
    {
        // 빠르게 선택을 바꿔도 이전 트윈이 겹치지 않게 한다.
        if (slot.scaleTween != null)
        {
            slot.scaleTween.Kill();
        }

        if (slot.brightnessTween != null)
        {
            slot.brightnessTween.Kill();
        }

        slot.selectionRing.gameObject.SetActive(selected);

        float scale = selected ? _selectedScale : 1f;
        float brightness = selected
            ? _selectedBrightness
            : _unselectedBrightness;

        slot.scaleTween = slot.button.transform
            .DOScale(slot.originalScale * scale, _transitionDuration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true);

        slot.brightnessTween = DOTween.To(
                () => slot.brightness,
                value => SetBrightness(slot, value),
                brightness,
                _transitionDuration)
            .SetUpdate(true);
    }

    private void SetBrightness(CharacterSlot slot, float brightness)
    {
        slot.brightness = brightness;

        // 투명도는 유지하고 원래 RGB 색상에만 밝기를 곱한다.
        for (int i = 0; i < slot.graphics.Length; i++)
        {
            Color original = slot.originalColors[i];

            slot.graphics[i].color = new Color(
                original.r * brightness,
                original.g * brightness,
                original.b * brightness,
                original.a);
        }
    }

    // 선택한 캐릭터를 저장하고, 선택 목적에 맞는 화면 전환을 요청한다.
    public void LoadCharacter(int index)
    {
        if (!_initialized || _isLeaving ||
            index < 0 || index >= _slots.Length ||
            !_slots[index].button.IsInteractable())
        {
            return;
        }

        GameSessionManager session = GameSessionManager.Instance;
        CharacterSelectPurpose purpose =
            session.CurrentCharacterSelectPurpose;

        // 화면 전환을 받을 관리자가 연결되어 있는지 먼저 확인한다.
        // 연결 오류가 있을 때 선택 화면을 떠나는 상태로 잠기지 않게 한다.
        switch (purpose)
        {
            case CharacterSelectPurpose.Battle:
                if (session.SelectedStageIndex < 0 ||
                    DepartureRequested == null)
                {
                    Debug.LogError(
                        "출격할 스테이지와 출격 이벤트 연결을 확인해주세요.",
                        this);
                    return;
                }
                break;

            case CharacterSelectPurpose.LoadoutEdit:
                if (EquipmentRequested == null)
                {
                    Debug.LogError(
                        "상점 화면 전환 이벤트가 연결되지 않았습니다.",
                        this);
                    return;
                }
                break;

            default:
                Debug.LogError(
                    "캐릭터 선택 목적이 설정되지 않았습니다.",
                    this);
                return;
        }

        SelectingCharacter(index);

        // 구매·장착 화면과 전투에서 사용할 캐릭터 정보를 저장한다.
        CharacterData character =
            _characterDatabase.GetCharacterDataAt(index);

        session.SelectCharacter(index);
        session.SelectCharacter(character.Id);

        // 화면 전환 중 중복 선택을 막는다.
        _isLeaving = true;

        if (purpose == CharacterSelectPurpose.LoadoutEdit)
        {
            EquipmentRequested.Invoke();
            return;
        }

        // 실제 전투 씬 로딩은 Town의 출격 연출이 끝난 뒤 처리한다.
        string destination = $"Stage{session.SelectedStageIndex + 1}";
        DepartureRequested.Invoke(destination);
    }

    // 전투용 선택에서는 스테이지로 돌아가고, 상점용 선택에서는 창을 닫는다.
    public void Back()
    {
        if (!_initialized || _isLeaving)
        {
            return;
        }

        GameSessionManager session = GameSessionManager.Instance;

        switch (session.CurrentCharacterSelectPurpose)
        {
            case CharacterSelectPurpose.Battle:
                if (StageSelectionRequested == null)
                {
                    Debug.LogError(
                        "스테이지 선택 복귀 이벤트가 연결되지 않았습니다.",
                        this);
                    return;
                }

                _isLeaving = true;
                session.ResetSelection();
                StageSelectionRequested.Invoke();
                break;

            case CharacterSelectPurpose.LoadoutEdit:
                if (CloseRequested == null)
                {
                    Debug.LogError(
                        "서비스 창 닫기 이벤트가 연결되지 않았습니다.",
                        this);
                    return;
                }

                // 선택 초기화와 Town 조작 복구는 창 관리자가 처리한다.
                _isLeaving = true;
                CloseRequested.Invoke();
                break;

            default:
                Debug.LogError(
                    "캐릭터 선택 목적이 설정되지 않았습니다.",
                    this);
                break;
        }
    }

    private void OnDestroy()
    {
        // 씬 이동 시 이 화면에서 생성한 트윈을 정리한다.
        if (_ringTween != null)
        {
            _ringTween.Kill();
        }

        if (_slots == null)
        {
            return;
        }

        foreach (CharacterSlot slot in _slots)
        {
            if (slot == null)
            {
                continue;
            }

            if (slot.scaleTween != null)
            {
                slot.scaleTween.Kill();
            }

            if (slot.brightnessTween != null)
            {
                slot.brightnessTween.Kill();
            }
        }
    }
}
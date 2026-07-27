using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 현재 웨폰 띄우기
/// </summary>
public class WeaponUI : MonoBehaviour
{
    [Header("현재웨폰슬롯칸")]
    [SerializeField] Image _currentWeapon;

    [Header("웨폰이미지들")]
    [SerializeField] Sprite[] _weaponIconSprites;

    [Header("컴포넌트")]
    [SerializeField] Weapon _weapon;

    private void Start()
    {
        UpdateCurrentWeapon(_weapon.CurrentWeaponIndex);
    }


    public void UpdateCurrentWeapon(int currentWeaponIndex)
    {
        _currentWeapon.sprite = _weaponIconSprites[currentWeaponIndex];
    }
}

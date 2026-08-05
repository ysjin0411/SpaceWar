using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 현재 사용 중인 무기의 아이콘을 화면에 표시한다.
/// </summary>
public class WeaponUI : MonoBehaviour
{
    [Header("현재 무기 이미지")]
    [SerializeField] Image _currentWeapon;

    /// <summary>
    /// 전달받은 무기 아이콘을 현재 무기 UI에 표시한다.
    /// </summary>
    /// <param name="weaponIcon">현재 장착 무기의 WeaponData 아이콘</param>
    public void UpdateCurrentWeapon(Sprite weaponIcon)
    {
        // 아이콘이 없다면 이미지 자체를 숨긴다.
        if (weaponIcon == null)
        {
            _currentWeapon.sprite = null;
            _currentWeapon.enabled = false;
            return;
        }

        // 표시할 아이콘이 있으므로 이미지를 활성화한다.
        _currentWeapon.enabled = true;

        // 현재 무기 이미지를 전달받은 아이콘으로 변경한다.
        _currentWeapon.sprite = weaponIcon;
    }
}
using UnityEngine;
using UnityEngine.UI;

public class HPUI : MonoBehaviour
{
    [Header("컴포넌트")]
    [SerializeField] PlayerController _playerController;        // 플레이어 컨트롤러 컴포넌트

    [Header("체력바 이미지")]
    [SerializeField] Image _hpBar;      // 체력바 이미지



    public void UpdateHpUI()
    {
        _hpBar.fillAmount = (float)_playerController.CurrentHp / _playerController.MaxHp;
    }
}




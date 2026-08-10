using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Stage에 생성된 플레이어의 현재 체력을 UI로 표시한다.
/// 캐릭터 프리팹은 Stage의 UI를 직접 참조할 수 없으므로, StageManager가 캐릭터를 생성한 후 Initialize()를 호출하여 새 PlayerController를 전달한다.
/// </summary>
public class HPUI : MonoBehaviour
{
    [Header("런타임 플레이어")]
    [Tooltip("StageManager가 생성한 현재 PlayerController")]
    [SerializeField] PlayerController _playerController;

    [Header("체력바 이미지")]
    [Tooltip("현재 체력을 fillAmount로 표시할 Image")]
    [SerializeField] Image _hpBar;

    /// <summary>
    /// StageManager가 새로 생성한 플레이어를 HP UI에 연결한다.
    /// </summary>
    /// <param name="playerController">이번 Stage에서 유저가 조작할 PlayerController</param>
    public void Initialize(PlayerController playerController)
    {
        if (playerController == null)
        {
            throw new ArgumentNullException(nameof(playerController));
        }

        // 기존 Stage Player 참조를 새로 생성한 선택 캐릭터로 교체한다.
        _playerController = playerController;

        // 연결 직후 현재 체력을 표시한다.
        UpdateHpUI();
    }

    /// <summary>
    /// PlayerController의 현재 체력과 최대 체력을 읽어
    /// 체력바의 fillAmount를 갱신한다.
    /// </summary>
    public void UpdateHpUI()
    {
        // 플레이어가 아직 생성되지 않았다면 체력 정보를 표시할 수 없다.
        if (_playerController == null)
        {
            _hpBar.fillAmount = 0f;
            return;
        }

        // 최대 체력이 0이면 나눗셈을 할 수 없으므로 체력바를 비운다.
        if (_playerController.MaxHp <= 0)
        {
            _hpBar.fillAmount = 0f;
            return;
        }

        // 현재 체력 비율을 0~1 범위로 계산한다.
        float hpRatio = (float)_playerController.CurrentHp / _playerController.MaxHp;

        // 계산된 비율을 체력바에 적용한다.
        _hpBar.fillAmount = Mathf.Clamp01(hpRatio);
    }
}
using UnityEngine;

/// <summary>Town에서 다른 창을 사용하지 않을 때 Esc로 환경설정을 엽니다.</summary>
[DefaultExecutionOrder(-100)]
public sealed class TownSettingsInput : MonoBehaviour
{
    // 대화와 가방, 퀘스트, NPC 기능 창이 공유하는 조작 잠금 상태를 확인한다.
    [SerializeField] private TownPlayerController _player;

    // Town에 배치된 공통 환경설정 창이다.
    [SerializeField] private AudioSettingsUI _settingsUI;

    // 독립적인 확인 창이 열려 있는 동안에도 환경설정을 열지 않는다.
    [SerializeField] private ConfirmationDialogUI[] _confirmationDialogs = new ConfirmationDialogUI[0];

    /// <summary>입력 처리에 필요한 씬 참조를 검사합니다.</summary>
    private void Awake()
    {
        // 연결 누락은 실행 시작 시 알리고 입력 처리를 중단한다.
        if (_player == null || _settingsUI == null)
        {
            Debug.LogError("TownSettingsInput의 Player와 Settings UI를 연결해주세요.", this);
            enabled = false;
        }
    }

    /// <summary>다른 창의 닫기 처리보다 먼저 Esc를 누른 순간의 상태를 확인합니다.</summary>
    private void Update()
    {
        // 이미 열린 창의 Esc 입력은 해당 창이 처리하도록 넘긴다.
        if (!Input.GetKeyDown(KeyCode.Escape) || _settingsUI.IsOpen ||
            !_player.isActiveAndEnabled || !_player.CanControl || Time.timeScale <= 0f)
        {
            return;
        }

        // 확인 또는 안내 창이 있으면 그 창을 먼저 처리한다.
        foreach (ConfirmationDialogUI dialog in _confirmationDialogs)
        {
            if (dialog != null && dialog.IsOpen)
            {
                return;
            }
        }

        // 공통 열기 경로를 사용해 음량 표시와 열림 이벤트를 유지한다.
        _settingsUI.Toggle();
    }
}

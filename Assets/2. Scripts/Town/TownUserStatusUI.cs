using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Town 좌측 상단에 로그인한 User의 정보를 표시한다.
public class TownUserStatusUI : MonoBehaviour
{
    // 로그인한 User ID를 표시할 텍스트.
    [SerializeField] private TMP_Text _userIdText;

    // User 레벨을 표시할 텍스트.
    [SerializeField] private TMP_Text _userLevelText;

    // 현재 레벨의 경험치 비율을 표시할 텍스트.
    [SerializeField] private TMP_Text _expText;

    // 현재 User 경험치 비율에 따라 채워지는 원형 게이지.
    [SerializeField] private Image _currentLevelFill;

    // 변경 알림을 구독한 저장 관리자.
    private SaveManager _saveManager;

    // 필수 UI 연결을 검사한다.
    private void Awake()
    {
        if (_userIdText == null ||
            _userLevelText == null ||
            _expText == null ||
            _currentLevelFill == null)
        {
            Debug.LogError(
                "TownUserStatusUI의 텍스트 세 개와 Current Level Fill을 연결해주세요.", this);

            enabled = false;
            return;
        }

        // ID에 포함된 문자가 TMP 서식으로 해석되지 않게 한다.
        _userIdText.richText = false;
    }

    // 화면을 다시 켰을 때도 최신 데이터를 표시한다.
    private void OnEnable()
    {
        BindSaveManager();
    }

    // 같은 씬의 SaveManager가 늦게 Awake된 경우에도 연결한다.
    private void Start()
    {
        BindSaveManager();

        if (_saveManager == null)
        {
            Debug.LogError(
                "SaveManager가 없습니다. 로그인 후 Town으로 진입해주세요.",
                this);
        }
    }

    // 저장 관리자의 변경 알림을 중복 없이 구독한다.
    private void BindSaveManager()
    {
        if (_saveManager == null)
        {
            _saveManager = SaveManager.Instance;

            if (_saveManager != null)
            {
                _saveManager.UserStatusChanged += Refresh;
            }
        }

        // Town에 들어오기 전에 완료된 로그인 정보도 바로 표시한다.
        Refresh();
    }

    // 저장 데이터로 ID·레벨·경험치 비율을 갱신한다.
    private void Refresh()
    {
        if (_saveManager == null || !_saveManager.IsLoggedIn)
        {
            _userIdText.text = "ID: -";
            _userLevelText.text = "User Level -";
            _expText.text = "Exp -";
            // 표시할 경험치 정보가 없으면 이전 게이지를 비운다.
            _currentLevelFill.fillAmount = 0f;
            return;
        }

        _userIdText.text = $"ID: {_saveManager.CurrentUserId}";
        _userLevelText.text =
            $"User Level {_saveManager.CurrentUserLevel}";

        if (!_saveManager.TryGetCurrentUserRequiredExp(
                out int requiredExp))
        {
            // 설정 누락을 경험치 0%로 잘못 표시하지 않는다.
            _expText.text = "Exp -";
            // 표시할 경험치 정보가 없으면 이전 게이지를 비운다.
            _currentLevelFill.fillAmount = 0f;

            Debug.LogWarning(
                "UserLevelData 연결과 현재 레벨의 필요 경험치를 확인해주세요.",
                this);

            return;
        }

        // 현재 레벨의 경험치 비율을 0~1 범위로 계산한다.
        float ratio = Mathf.Clamp01(
            (float)_saveManager.CurrentUserExp / requiredExp);

        // 텍스트와 게이지에 동일한 비율을 반영한다.
        _expText.text = $"Exp {ratio * 100f:F1}%";
        _currentLevelFill.fillAmount = ratio;
    }

    // 화면이 꺼지면 저장 관리자의 이벤트 구독을 해제한다.
    private void OnDisable()
    {
        if (_saveManager != null)
        {
            _saveManager.UserStatusChanged -= Refresh;
        }

        _saveManager = null;
    }
}
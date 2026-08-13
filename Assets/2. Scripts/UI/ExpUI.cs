using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ExpUI : MonoBehaviour
{
    [Header("컴포넌트")]
    [SerializeField] CharacterProgression _characterProgression;    // 캐릭터 레벨정보 

    [Header("레벨")]
    [SerializeField] TMP_Text _currentLevelText;    // 현재레벨 텍스트

    [Header("이미지")]
    [SerializeField] Image _currentLevelGaugeImage; // 현제레벨바 이미지

    private void OnEnable()
    {
        if (_characterProgression == null)
        {
            return;
        }

        // 경험치가 변경될 때 UpdateExp가 호출되도록 연결한다.
        _characterProgression.OnProgressChanged += UpdateExp;

        // 이미 초기화된 상태에서 UI가 켜질 수도 있으므로 즉시 한 번 갱신한다.
        UpdateExp(
            _characterProgression.CurrentLevel,
            _characterProgression.CurrentExp,
            _characterProgression.RequiredExp);
    }

    private void OnDisable()
    {
        if (_characterProgression == null)
        {
            return;
        }

        // UI가 꺼질 때 이벤트 연결을 해제한다.
        _characterProgression.OnProgressChanged -= UpdateExp;
    }

    private void UpdateExp(int currentLevel, int currentExp, int requiredExp)
    {
        _currentLevelText.text = $"{currentLevel}";

        if (requiredExp <= 0)
        {
            _currentLevelGaugeImage.fillAmount = 0f;
            return;
        }

        float expRatio = (float)currentExp / requiredExp;

        _currentLevelGaugeImage.fillAmount = Mathf.Clamp01(expRatio);
    }
}
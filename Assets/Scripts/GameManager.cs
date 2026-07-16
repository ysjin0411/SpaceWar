using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    [Header("컴포넌트")]
    [SerializeField] PlayerController _playerController;

    [Header("게이지관련")]
    [SerializeField] Image _gaugeBar;
    [SerializeField] float _maxGaugeBar = 100f;
    [SerializeField] float _timeCharge;
    [SerializeField] float _bonusCharge;
    [SerializeField] GameObject _selectPanel;


    

    [Header("프리팹")]
    [SerializeField] GameObject _fastEnemyPrefab;

    public void UpdateGaugeBar()
    {
        if (_timeCharge > _maxGaugeBar)
        {
            Time.timeScale = 0f;
            // 증강 패널 열기
            _selectPanel.SetActive(true);
        }
        _gaugeBar.fillAmount = _timeCharge / _maxGaugeBar;
    }

    public void BonusGauge()
    {
        _timeCharge += _bonusCharge;
    }

    private void Update()
    {
        _timeCharge += Time.deltaTime;

    }

    // 1번을 누르거나 버튼에서 함수를 호출시킨다.
    public void SelectA()
    {
        // 1번 증강을 고른다 > 능력치 적용시킨다 > 패널을 끄며 다시 타임스케일 돌린다 

    }
}
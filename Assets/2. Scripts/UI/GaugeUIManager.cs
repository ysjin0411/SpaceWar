using UnityEngine;
using UnityEngine.UI;

public class GaugeUIManager : MonoBehaviour
{
    [Header("게이지바")]
    [SerializeField] Image _gaugeBar;

    [Header("컴포넌트")]
    [SerializeField] GameManager _gameManager;

    private void Update()
    {
        _gameManager.UpdateGaugeBar();
    }
}
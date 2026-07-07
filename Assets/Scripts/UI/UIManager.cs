using UnityEngine;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject _mainPanel;
    public GameObject _skillPanel;
    public GameObject _statPanel;
    public GameObject _settingPanel;

    private void Start()
    {
        OpenMain();
    }

    public void OpenMain()
    {
        _mainPanel.SetActive(true);
        _skillPanel.SetActive(false);
        _statPanel.SetActive(false);
        _settingPanel.SetActive(false);
    }

    public void OnClickPlay()
    {
        SceneManager.LoadScene("PlayScene");
    }

    public void OnClickSkill()
    {
        _mainPanel.SetActive(false);
        _skillPanel.SetActive(true);
    }

    public void OnClickStat()
    {
        _mainPanel.SetActive(false);
        _statPanel.SetActive(true);
    }

    public void OnClickSetting()
    {
        _mainPanel.SetActive(false);
        _settingPanel.SetActive(true);
    }

    public void OnClickBack()
    {
        _mainPanel.SetActive(true);
        _skillPanel.SetActive(false);
        _statPanel.SetActive(false);
        _settingPanel.SetActive(false);
    }
}
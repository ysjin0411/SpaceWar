using UnityEngine;
using UnityEngine.SceneManagement;

public class BootstrapManager : MonoBehaviour
{
    private void Start()
    {
        SceneManager.LoadScene("1.MainMenu");
    }
}
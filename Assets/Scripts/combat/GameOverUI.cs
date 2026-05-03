using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameOverUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button quitButton;

    private void Awake()
    {
        if (panel == null)
        {
            panel = gameObject;
        }

        Button[] buttons = GetComponentsInChildren<Button>(true);
        if (buttons.Length >= 1) restartButton = buttons[0];
        if (buttons.Length >= 2) quitButton = buttons[1];

        Debug.Log($"버튼 개수: {buttons.Length}");
        Hide();

        if (restartButton != null) restartButton.onClick.AddListener(OnRestart);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuit);
    }

    public void Show()
    {
        if (panel == null)
        {
            Debug.LogError("[GameOverUI] Show() failed because panel reference is null.", this);
            return;
        }

        Debug.Log($"[GameOverUI] Show() before SetActive(true): panel={panel.name} activeSelf={panel.activeSelf} activeInHierarchy={panel.activeInHierarchy}", this);
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
        Debug.Log($"[GameOverUI] Show() after SetActive(true): panel={panel.name} activeSelf={panel.activeSelf} activeInHierarchy={panel.activeInHierarchy}", this);
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }

    private void OnRestart()
    {
        Hide();
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

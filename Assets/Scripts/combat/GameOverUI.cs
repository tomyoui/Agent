using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameOverUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private int gameOverSortingOrder = 100;

    private void Awake()
    {
        if (panel == null)
        {
            panel = gameObject;
        }

        Button[] buttons = GetComponentsInChildren<Button>(true);
        if (buttons.Length >= 1) restartButton = buttons[0];
        if (buttons.Length >= 2) quitButton = buttons[1];

        ConfigurePanelCanvas();
        Debug.Log($"[GameOverUI] 버튼 개수: {buttons.Length}");
        Hide();

        if (restartButton != null) restartButton.onClick.AddListener(OnRestart);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuit);
    }

    public void Show()
    {
        if (panel == null)
        {
            Debug.LogError("[GameOverUI] 패널 참조가 없어 표시할 수 없습니다.", this);
            return;
        }

        ConfigurePanelCanvas();
        Debug.Log($"[GameOverUI] 표시 전 상태: panel={panel.name} activeSelf={panel.activeSelf} activeInHierarchy={panel.activeInHierarchy}", this);
        panel.SetActive(true);
        Debug.Log($"[GameOverUI] 표시 후 상태: panel={panel.name} activeSelf={panel.activeSelf} activeInHierarchy={panel.activeInHierarchy}", this);
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

    private void ConfigurePanelCanvas()
    {
        if (panel == null)
        {
            return;
        }

        Canvas panelCanvas = panel.GetComponent<Canvas>();
        if (panelCanvas == null)
        {
            panelCanvas = panel.AddComponent<Canvas>();
        }

        panelCanvas.overrideSorting = true;
        panelCanvas.sortingOrder = gameOverSortingOrder;
    }
}

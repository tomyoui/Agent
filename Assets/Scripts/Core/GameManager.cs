using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 게임 전역 상태 관리 싱글턴.
/// GameOver / RestartGame 진입점을 단일 창구로 제공.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 전원 사망 등 게임 종료 조건 충족 시 호출.
    /// Time.timeScale을 0으로 고정해 모든 업데이트를 정지.
    /// </summary>
    public void GameOver()
    {
        Time.timeScale = 0f;
        Debug.Log("[GameManager] Game Over");
    }

    /// <summary>
    /// 현재 씬을 처음부터 다시 로드해 게임을 재시작.
    /// </summary>
    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 일정 간격으로 적 프리팹을 스폰 포인트에 생성하고 최대 생존 수를 관리하는 스포너
[DisallowMultipleComponent]
public class EnemySpawner : MonoBehaviour
{
    [Header("Spawner Setup")]
    [Tooltip("생성할 적 프리팹")]
    [SerializeField] private GameObject enemyPrefab;
    [Tooltip("적을 생성할 스폰 포인트 목록")]
    [SerializeField] private List<Transform> spawnPoints = new List<Transform>();

    [Header("Spawn Rules")]
    [SerializeField] private bool spawnOnStart = false;
    [Tooltip("적 생성 간격 (초)")]
    [SerializeField] private float spawnInterval = 2f;
    [Tooltip("동시에 살아있을 수 있는 최대 적 수")]
    [SerializeField] private int maxAliveEnemies = 5;

    // 생성된 적 참조를 보관하며, 매 사이클마다 파괴된 항목을 제거
    private readonly List<GameObject> _spawnedEnemies = new List<GameObject>();
    private Coroutine _spawnLoopRoutine;

    private void OnEnable()
    {
        if (!spawnOnStart)
        {
            return;
        }

        _spawnLoopRoutine = StartCoroutine(SpawnLoop());
    }

    private void OnDisable()
    {
        if (_spawnLoopRoutine != null)
        {
            StopCoroutine(_spawnLoopRoutine);
            _spawnLoopRoutine = null;
        }
    }

    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Mathf.Max(0.01f, spawnInterval));

            if (enemyPrefab == null || spawnPoints == null || spawnPoints.Count == 0)
            {
                continue;
            }

            CleanupDestroyedEnemies();

            if (_spawnedEnemies.Count >= Mathf.Max(0, maxAliveEnemies))
            {
                continue;
            }

            Transform spawnPoint = GetRandomSpawnPoint();
            if (spawnPoint == null)
            {
                continue;
            }

            GameObject spawned = Instantiate(
                enemyPrefab,
                spawnPoint.position,
                spawnPoint.rotation
            );

            _spawnedEnemies.Add(spawned);
        }
    }

    private void CleanupDestroyedEnemies()
    {
        for (int i = _spawnedEnemies.Count - 1; i >= 0; i--)
        {
            if (_spawnedEnemies[i] == null)
            {
                _spawnedEnemies.RemoveAt(i);
            }
        }
    }

    private Transform GetRandomSpawnPoint()
    {
        int index = Random.Range(0, spawnPoints.Count);
        return spawnPoints[index];
    }
}

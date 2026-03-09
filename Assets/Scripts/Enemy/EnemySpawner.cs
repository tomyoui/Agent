using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemySpawner : MonoBehaviour
{
    [Header("Spawner Setup")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private List<Transform> spawnPoints = new List<Transform>();

    [Header("Spawn Rules")]
    [SerializeField] private float spawnInterval = 2f;
    [SerializeField] private int maxAliveEnemies = 5;

    // We keep references to spawned enemies and remove nulls (destroyed enemies) each cycle.
    private readonly List<GameObject> _spawnedEnemies = new List<GameObject>();
    private Coroutine _spawnLoopRoutine;

    private void OnEnable()
    {
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

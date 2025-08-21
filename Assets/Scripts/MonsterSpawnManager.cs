using System.Collections.Generic;
using UnityEngine;
using MyGame.Environment; // Bush

/// <summary>
/// Centralized monster spawn manager.
/// Spawns enemies around the player on grass only, within a distance ring [min,max], avoiding bushes.
/// Requires TerrainInitialization in scene for walkability checks.
/// </summary>
[DisallowMultipleComponent]
public class MonsterSpawnManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private GameObject enemyPrefab;   // Prefab with Enemy component
    [SerializeField] private Transform monstersParent; // Optional parent for organization

    [Header("Spawn Settings")] 
    [SerializeField] private int initialCount = 8;
    [SerializeField] private int maxTries = 100;
    [SerializeField] private float minDistance = 30f;
    [SerializeField] private float maxDistance = 40f;
    [SerializeField] private float avoidBushRadius = 1.5f;

    [Header("Runtime")] 
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private bool autoWaveSpawn = true;
    [SerializeField] private float waveIntervalSeconds = 30f;
    [SerializeField] private int waveCount = 6; // monsters per wave

    private System.Random rnd;

    private void Awake()
    {
        rnd = new System.Random();
        if (player == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null) player = p.transform;
        }
    }

    private void Start()
    {
        if (spawnOnStart)
        {
            SpawnEnemies(initialCount);
        }

        if (autoWaveSpawn)
        {
            StartCoroutine(AutoWaveSpawn());
        }
    }

    [ContextMenu("Spawn Initial Enemies")] 
    public void SpawnInitialEnemies()
    {
        SpawnEnemies(initialCount);
    }

    /// <summary>
    /// Spawn a number of enemies around player obeying constraints.
    /// </summary>
    public void SpawnEnemies(int count)
    {
        if (enemyPrefab == null || player == null)
        {
            Debug.LogWarning("[MonsterSpawnManager] Missing player or enemyPrefab.");
            return;
        }

        int spawned = 0;
        for (int i = 0; i < maxTries && spawned < count; i++)
        {
            Vector3? pos = SampleSpawnPosition();
            if (!pos.HasValue) continue;
            var inst = Instantiate(enemyPrefab, pos.Value, Quaternion.identity);
            if (monstersParent != null) inst.transform.SetParent(monstersParent);
            // hook death -> +10 exp
            var deathHook = inst.GetComponent<OnEnemyDeathExpGiver>();
            if (deathHook == null)
            {
                deathHook = inst.AddComponent<OnEnemyDeathExpGiver>();
            }
            deathHook.expAmount = 10;
            spawned++;
        }
        Debug.Log($"[MonsterSpawnManager] Spawned {spawned}/{count} enemies");
    }

    private System.Collections.IEnumerator AutoWaveSpawn()
    {
        while (true)
        {
            yield return new WaitForSeconds(waveIntervalSeconds);
            SpawnEnemies(waveCount);
        }
    }

    private Vector3? SampleSpawnPosition()
    {
        // Random in ring
        float dist = Mathf.Lerp(minDistance, maxDistance, (float)rnd.NextDouble());
        float ang = (float)(rnd.NextDouble() * Mathf.PI * 2);
        Vector3 candidate = player.position + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * dist;

        // Must be walkable grass tile
        if (TerrainInitialization.Instance != null)
        {
            if (!TerrainInitialization.Instance.IsWalkable(candidate)) return null;
        }

        // Avoid near bushes
        Bush[] bushes = FindObjectsOfType<Bush>();
        foreach (var b in bushes)
        {
            if (b == null) continue;
            if (Vector2.Distance(candidate, b.transform.position) < avoidBushRadius)
                return null;
        }
        return candidate;
    }
}



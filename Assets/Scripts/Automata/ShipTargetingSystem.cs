using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Per-ship targeting system with dynamic asteroid targeting control
/// </summary>
public class ShipTargetingSystem : MonoBehaviour, ITargetProvider
{
    [Header("Targeting Configuration")]
    public float sonarRadius = 100f;
    public TargetingConfig targetingConfig;
    public int maxTrackedTargets = 50;

    private List<Targetable> trackedTargets = new List<Targetable>();
    private TargetData[] targetDataArray = new TargetData[0];

    // Alignment filtering
    private Alignment shipAlignment;

    // Cache own layer to prevent self-targeting
    private int ownLayer;

    // Track original asteroid setting
    private bool originalAsteroidSetting;

    void Awake()
    {
        shipAlignment = GetComponentInParent<Alignment>();
        ownLayer = gameObject.layer;

        if (shipAlignment == null)
        {
            Debug.LogWarning($"ShipTargetingSystem on {gameObject.name} has no Alignment component in parent! Targeting may not work correctly.");
        }

        // ✅ FIXED: Only clone config for NPCs, player keeps inspector-assigned config
        if (targetingConfig != null)
        {
            originalAsteroidSetting = targetingConfig.asteroids;

            // Check if this is the player ship
            bool isPlayerShip = gameObject.layer == LayerMask.NameToLayer("Player");

            if (!isPlayerShip)
            {
                // NPCs: Clone the config for independent settings
                targetingConfig = Instantiate(targetingConfig);
                targetingConfig.name = $"{targetingConfig.name} (Clone for {gameObject.name})";
            }
            else
            {
                // Player: Keep the inspector-assigned config
            }
        }
    }


    /// <summary>
    /// Enable/disable asteroid targeting (for stuck-in-asteroids behavior)
    /// </summary>
    public void SetAsteroidTargeting(bool enabled)
    {
        if (targetingConfig == null) return;

        if (targetingConfig.asteroids != enabled)
        {
            targetingConfig.asteroids = enabled;
            Debug.Log($"[{gameObject.name}] Asteroid targeting: {(enabled ? "ENABLED" : "DISABLED")}");
        }
    }

    /// <summary>
    /// Check if asteroid targeting is currently enabled
    /// </summary>
    public bool IsAsteroidTargetingEnabled()
    {
        return targetingConfig != null && targetingConfig.asteroids;
    }

    /// <summary>
    /// Reset to original asteroid setting
    /// </summary>
    public void ResetAsteroidTargeting()
    {
        if (targetingConfig != null)
        {
            targetingConfig.asteroids = originalAsteroidSetting;
        }
    }

    public void RefreshTargets()
    {
        ScanForTargets(maxTrackedTargets);
        FormTargetDataArray();
    }

    private void ScanForTargets(int maxCount)
    {
        var colliders = Physics.OverlapSphere(transform.position, sonarRadius);
        var potentialTargets = new List<Targetable>();

        foreach (var col in colliders)
        {
            var targetable = col.GetComponent<Targetable>();
            if (targetable == null || !targetable.IsAlive)
                continue;

            // Prevent self-targeting by checking layer
            if (col.gameObject.layer == ownLayer)
                continue;

            // Check targeting config - this handles ALL filtering now
            if (targetingConfig != null && !targetingConfig.Allows(targetable))
                continue;

            // ✅ SIMPLIFIED: Only check targetAll for friendly ships
            // Let TargetingConfig.Allows() handle all other logic
            if (targetingConfig != null && !targetingConfig.targetAll)
            {
                var targetAlignment = col.GetComponent<Alignment>();
                if (targetAlignment != null && shipAlignment != null)
                {
                    // Skip friendly targets if targetAll is false
                    if (targetAlignment.isFriendly && shipAlignment.isFriendly)
                        continue;
                }
            }

            potentialTargets.Add(targetable);
        }

        int count = potentialTargets.Count;
        if (count == 0)
        {
            trackedTargets.Clear();
            return;
        }

        // Use Unity Jobs for performance
        var positions = new NativeArray<float3>(count, Allocator.TempJob);
        var indices = new NativeArray<int>(count, Allocator.TempJob);
        var distances = new NativeArray<float>(count, Allocator.TempJob);

        for (int i = 0; i < count; i++)
        {
            positions[i] = potentialTargets[i].transform.position;
            indices[i] = i;
        }

        var distanceJob = new SonarDistanceJob
        {
            SeekerPos = (float3)transform.position,
            TargetPositions = positions,
            Distances = distances
        };

        var sortJob = new SonarSortJob
        {
            Distances = distances,
            Indices = indices
        };

        JobHandle distanceHandle = distanceJob.Schedule(count, 64);
        JobHandle sortHandle = sortJob.Schedule(distanceHandle);
        sortHandle.Complete();

        trackedTargets.Clear();
        for (int i = 0; i < Mathf.Min(maxCount, count); i++)
            trackedTargets.Add(potentialTargets[indices[i]]);

        positions.Dispose();
        indices.Dispose();
        distances.Dispose();
    }

    private void FormTargetDataArray()
    {
        targetDataArray = new TargetData[trackedTargets.Count];

        for (int i = 0; i < trackedTargets.Count; i++)
        {
            var target = trackedTargets[i];
            float distance = (target.transform.position - transform.position).sqrMagnitude;
            targetDataArray[i] = new TargetData(target.gameObject, distance);
        }
    }

    // ITargetProvider implementation
    public List<Targetable> GetTrackedTargets() => trackedTargets;
    public TargetData[] GetTargetDataArray() => targetDataArray;
    public Vector3 ScanPosition => transform.position;

    // Nested struct
    public struct TargetData
    {
        public GameObject TargetObject { get; }
        public float UnifiedDistance { get; }

        public TargetData(GameObject obj, float distance)
        {
            TargetObject = obj;
            UnifiedDistance = distance;
        }
    }
}

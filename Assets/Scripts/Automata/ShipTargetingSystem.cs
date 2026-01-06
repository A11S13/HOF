using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Optimized ShipTargetingSystem using NonAlloc physics and refresh throttling.
/// </summary>
public class ShipTargetingSystem : MonoBehaviour, ITargetProvider
{
    [Header("Targeting Configuration")]
    public float sonarRadius = 100f;
    public TargetingConfig targetingConfig;
    public int maxTrackedTargets = 50;

    [Header("Optimization Settings")]
    [Tooltip("Layers to scan. IMPORTANT: Set this to Ships + Asteroids.")]
    public LayerMask scanLayers = ~0;
    [Tooltip("How often to scan for targets (Seconds). Prevents CPU spikes.")]
    public float scanInterval = 0.5f;

    private List<Targetable> trackedTargets = new List<Targetable>();
    private TargetData[] targetDataArray = new TargetData[0];

    // Cache
    private Alignment shipAlignment;
    private int ownLayer;
    private bool originalAsteroidSetting;
    private float lastScanTime;

    // NonAlloc Buffer - Reduces Garbage Collection
    private readonly Collider[] hitColliders = new Collider[150];

    void Awake()
    {
        shipAlignment = GetComponentInParent<Alignment>();
        ownLayer = gameObject.layer;

        if (shipAlignment == null)
        {
            Debug.LogWarning($"ShipTargetingSystem on {gameObject.name} has no Alignment!");
        }

        if (targetingConfig != null)
        {
            originalAsteroidSetting = targetingConfig.asteroids;
            bool isPlayerShip = gameObject.layer == LayerMask.NameToLayer("Player");
            if (!isPlayerShip)
            {
                targetingConfig = Instantiate(targetingConfig);
                targetingConfig.name = $"{targetingConfig.name} (Clone)";
            }
        }
    }

    public void SetAsteroidTargeting(bool enabled)
    {
        if (targetingConfig == null) return;

        if (targetingConfig.asteroids != enabled)
        {
            targetingConfig.asteroids = enabled;
            // Force an immediate refresh if we are turning it ON so reaction is instant
            if (enabled)
            {
                lastScanTime = 0f; // Reset timer to allow immediate scan
                RefreshTargets();
            }
        }
    }

    public bool IsAsteroidTargetingEnabled() => targetingConfig != null && targetingConfig.asteroids;

    public void ResetAsteroidTargeting()
    {
        if (targetingConfig != null) targetingConfig.asteroids = originalAsteroidSetting;
    }

    public void RefreshTargets()
    {
        // Throttle scans to prevent lag when multiple NPCs are active
        if (Time.time < lastScanTime + scanInterval) return;
        lastScanTime = Time.time;

        ScanForTargets(maxTrackedTargets);
        FormTargetDataArray();
    }

    private void ScanForTargets(int maxCount)
    {
        // 1. USE NON-ALLOC PHYSICS
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, sonarRadius, hitColliders, scanLayers);

        var potentialTargets = new List<Targetable>(hitCount);

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = hitColliders[i];
            if (col == null) continue;

            // Fast layer check
            if (col.gameObject.layer == ownLayer) continue;

            // GetComponent is heavy, do it only after layer checks pass
            var targetable = col.GetComponent<Targetable>();
            if (targetable == null || !targetable.IsAlive) continue;

            if (targetingConfig != null && !targetingConfig.Allows(targetable))
                continue;

            if (targetingConfig != null && !targetingConfig.targetAll)
            {
                var targetAlignment = col.GetComponent<Alignment>();
                if (targetAlignment != null && shipAlignment != null)
                {
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

        // 2. JOB SYSTEM
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

    public List<Targetable> GetTrackedTargets() => trackedTargets;
    public TargetData[] GetTargetDataArray() => targetDataArray;
    public Vector3 ScanPosition => transform.position;

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


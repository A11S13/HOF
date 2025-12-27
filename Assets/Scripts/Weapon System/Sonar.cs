using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class Sonar : MonoBehaviour
{
    ShipCore _sC;
    TargetingConfig config;

    public float sonarCurrentRadius = 100f;
    public Material sonarMaterial;

    LineRenderer sonarVisual;

    public List<Targetable> trackedTargets = new List<Targetable>();

    void Awake()
    {
        _sC = GameObject.Find("MOTHERSHIP").GetComponent<ShipCore>();
        config = _sC.targetingConfig;
        sonarCurrentRadius = _sC.sonarRadius;
        CreateSonarVisual();
    }

    void FixedUpdate() { }

    void CreateSonarVisual()
    {
        sonarVisual = gameObject.AddComponent<LineRenderer>();
        sonarVisual.material = sonarMaterial;
        sonarVisual.positionCount = 100;
        sonarVisual.startWidth = 0.2f;
        sonarVisual.endWidth = 0.2f;

        var points = new Vector3[100];
        for (int i = 0; i < 100; i++)
        {
            float a = i * 2 * Mathf.PI / 100;
            points[i] = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * sonarCurrentRadius;
        }

        sonarVisual.SetPositions(points);
    }

    public void DriveSonar(int maxCount)
    {
        var colliders = Physics.OverlapSphere(transform.position, sonarCurrentRadius);
        var targets = new List<Targetable>();

        foreach (var col in colliders)
        {
            var t = col.GetComponent<Targetable>();
            if (t == null || !t.IsAlive)
                continue;

            if (!config.Allows(t))
                continue;

            targets.Add(t);
        }

        int count = targets.Count;
        if (count == 0)
        {
            trackedTargets.Clear();
            return;
        }

        var positions = new NativeArray<float3>(count, Allocator.TempJob);
        var indices = new NativeArray<int>(count, Allocator.TempJob);
        var distances = new NativeArray<float>(count, Allocator.TempJob);

        for (int i = 0; i < count; i++)
        {
            positions[i] = targets[i].transform.position;
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
            trackedTargets.Add(targets[indices[i]]);

        positions.Dispose();
        indices.Dispose();
        distances.Dispose();
    }
}

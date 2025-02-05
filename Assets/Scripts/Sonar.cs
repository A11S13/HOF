using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class Sonar : MonoBehaviour
{
    ShipCore _sC;
    public Collider[] enemyColliders;
    public int maxTrackedCap;
    public float sonarCurrentRadius = 100f;
    public float sonarPrevMaxRadius;
    public Material sonarMaterial;

    private LineRenderer sonarVisual;
    private int maxTracked;

    public List<Collider> trackedColliders = new List<Collider>();

    void FixedUpdate() { }

    void Awake()
    {
        _sC = GameObject.Find("MOTHERSHIP").GetComponent<ShipCore>();
        sonarPrevMaxRadius = _sC.sonarRadius;
        trackedColliders = new List<Collider>();
        CreateSonarVisual();
        sonarCurrentRadius = 100f;
    }

    void Start()
    {
        //SonarCalculation();
    }

    private void CreateSonarVisual()
    {
        sonarVisual = gameObject.AddComponent<LineRenderer>();
        sonarVisual.material = sonarMaterial;
        sonarVisual.positionCount = 100; // Adjust based on the circle's smoothness

        // Set the initial circle points using polar coordinates
        List<Vector3> points = new List<Vector3>();
        for (int i = 0; i < 100; i++)
        {
            float angle = i * 2 * Mathf.PI / 100;
            float x = Mathf.Cos(angle) * sonarCurrentRadius;
            float z = Mathf.Sin(angle) * sonarCurrentRadius;
            float y = 0;

            points.Add(new Vector3(x, y, z));
        }

        // Set positions
        sonarVisual.SetPositions(points.ToArray());

        // Set the width of the line
        sonarVisual.startWidth = 0.2f; // Adjust the width as needed
        sonarVisual.endWidth = 0.2f; // Adjust the width as needed
    }

    private void SonarCalculation()
    {
        sonarCurrentRadius += 100f * Time.fixedDeltaTime;

        if (sonarCurrentRadius >= _sC.sonarRadius)
        {
            sonarCurrentRadius = 0f;
        }

        // Update the circle points based on sonar radius
        for (int i = 0; i < 100; i++)
        {
            float angle = i * 2 * Mathf.PI / 98;
            float x = Mathf.Cos(angle) * sonarCurrentRadius + transform.position.x;
            float z = Mathf.Sin(angle) * sonarCurrentRadius + transform.position.z;
            sonarVisual.SetPosition(i, new Vector3(x, 0, z));
        }
    }

    public void SonarPhysics()
    {
        //maxTracked = maxTrackedCap + _sC.Weapons.Length;

        enemyColliders = Physics.OverlapSphere(
            transform.position,
            sonarCurrentRadius,
            LayerMask.GetMask("Enemy")
        );

        trackedColliders.AddRange(enemyColliders);
        foreach (Collider collider in trackedColliders)
        {
            collider.transform.Find("IndicatorBlue").gameObject.SetActive(false);
        }

        foreach (Collider collider in enemyColliders)
        {
            collider.transform.Find("IndicatorBlue").gameObject.SetActive(true);
        }
    }


    public void MakeupTracked()
    {
        // Remove destroyed
        trackedColliders = trackedColliders
            .Where(collider => !collider.GetComponent<Enemy>().isDestroyed)
            .ToList();

        // Remove null elements
        trackedColliders.RemoveAll(item => item == null);

        // Remove duplicate elements
        trackedColliders = trackedColliders.Distinct().ToList();

        // Sort the list by distance to _sonar
        trackedColliders.Sort(
            (a, b) =>
                Vector3
                    .Distance(a.transform.position, transform.position)
                    .CompareTo(Vector3.Distance(b.transform.position, transform.position))
        );

        foreach (Collider collider in trackedColliders)
        {
            // Set indicators based on isTargeted
            if (collider.GetComponent<Enemy>().isTargeted)
            {
                collider.transform.Find("IndicatorRed").gameObject.SetActive(true);
                collider.transform.Find("IndicatorBlue").gameObject.SetActive(false);
            }
        }
    }

    public void DriveSonar(string[] tags, int maxCount)
    {
        // 1. Get all colliders in sphere
        Collider[] allColliders = Physics.OverlapSphere(transform.position, sonarCurrentRadius);

        // 2. Filter by tags
        List<Collider> filteredColliders = new List<Collider>();
        foreach (var col in allColliders)
        {
            foreach (string tag in tags)
            {
                if (col.CompareTag(tag))
                {
                    filteredColliders.Add(col);
                    break;
                }
            }
        }

        int targetCount = filteredColliders.Count;
        if (targetCount == 0)
        {
            trackedColliders.Clear();
            return;
        }

        // 3. Prepare data for jobs
        NativeArray<float3> positions = new NativeArray<float3>(targetCount, Allocator.TempJob);
        NativeArray<int> indices = new NativeArray<int>(targetCount, Allocator.TempJob);
        NativeArray<float> distances = new NativeArray<float>(targetCount, Allocator.TempJob);

        for (int i = 0; i < targetCount; i++)
        {
            positions[i] = filteredColliders[i].transform.position;
            indices[i] = i;
        }

        // 4. Schedule jobs
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

        JobHandle distanceHandle = distanceJob.Schedule(targetCount, 64);
        JobHandle sortHandle = sortJob.Schedule(distanceHandle);
        sortHandle.Complete();

        // 5. Update tracked colliders
        trackedColliders.Clear();
        int resultCount = Mathf.Min(maxCount, targetCount);
        for (int i = 0; i < resultCount; i++)
        {
            trackedColliders.Add(filteredColliders[indices[i]]);
        }

        // 6. Cleanup
        positions.Dispose();
        indices.Dispose();
        distances.Dispose();
    }
}

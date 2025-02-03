using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class Sonar : MonoBehaviour
{
    ShipCore _sC;
    public int sonarCurrentRadius;
    public int maxSonarRadius;
    public int targetCount;
    public int maxTargetCount;

    private Collider[] filteredColliders;
    public List<Collider> actualColliders;
    public Transform[] trackedTransforms;
    public float[] trackedDistances;

    private int _frameCount;

    NativeArray<float3> TargetPositions;
    NativeArray<float> TargetDistances; 
    NativeArray<float3> NearestTargetPositions;
    NativeArray<float> NearestTargetDistances; 

    void Awake()
    {
        _sC = GameObject.Find("MOTHERSHIP").GetComponent<ShipCore>();
        _frameCount = 0; // Initialize counter
        filteredColliders = new Collider[0]; // Temporary to avoid null
    }

    private void Start()
    {
        maxTargetCount = _sC.Weapons.Length * 8;
        filteredColliders = new Collider[maxTargetCount];
        actualColliders = new List<Collider>();

        sonarCurrentRadius = 0;
        maxSonarRadius = _sC.sonarRadius;

        TargetPositions = new NativeArray<float3>(maxTargetCount, Allocator.Persistent);
        TargetDistances = new NativeArray<float>(maxTargetCount, Allocator.Persistent); 
        NearestTargetPositions = new NativeArray<float3>(maxTargetCount, Allocator.Persistent);
        NearestTargetDistances = new NativeArray<float>(maxTargetCount, Allocator.Persistent); 
    }

    public void OnDestroy()
    {
        TargetPositions.Dispose();
        TargetDistances.Dispose();
        NearestTargetPositions.Dispose();
        NearestTargetDistances.Dispose();
    }

    public void DriveSonar(LayerMask[] targetLayers)
    {
        if (sonarCurrentRadius < maxSonarRadius)
        {
            sonarCurrentRadius += 10;
        }
        else
        {
            sonarCurrentRadius = 0;
        }

        _frameCount++;
        bool shouldRefresh = true;

        // 1. Get colliders only every 10th frame
        if (shouldRefresh)
        {
            // 2. Combine LayerMasks
            LayerMask combinedMask = 0;  // Initialize to 0
            foreach (LayerMask mask in targetLayers)
            {
                combinedMask |= mask; // Bitwise OR to combine
            }

            Physics.OverlapSphereNonAlloc(transform.position, sonarCurrentRadius, filteredColliders, combinedMask);
            
            if (filteredColliders.Length < actualColliders.Count)
            {
                actualColliders.InsertRange(0, filteredColliders.Where(x => x != null)); // Insert at index 0 (the beginning)
            }
            else
            {
                actualColliders.AddRange(filteredColliders);
            }
                        
            if (actualColliders.Count > maxTargetCount) // Truncate if necessary
            {
                while (actualColliders.Count > maxTargetCount)
                {
                    actualColliders.RemoveAt(actualColliders.Count - 1); // Remove the last element
                }
            }
        }

        actualColliders.RemoveAll(item => item == null); // Remove null elements
        actualColliders = actualColliders.Distinct().ToList(); // Remove duplicate elements
        targetCount = actualColliders.Count;

        // Allocate job arrays with TempJob since we're going to dispose them soon.
        TargetPositions = new NativeArray<float3>(maxTargetCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
        TargetDistances = new NativeArray<float>(maxTargetCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
        NearestTargetPositions = new NativeArray<float3>(maxTargetCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
        NearestTargetDistances = new NativeArray<float>(maxTargetCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);

        // 3. Prepare data for jobs
        for (int i = 0; i < targetCount; i++)
        {
            TargetPositions[i] = actualColliders[i].transform.position;
        }

        // 4. Schedule jobs
        SortJob<float3, AxisXComparer> sortJob = TargetPositions.SortJob(new AxisXComparer());
        FindNearestJob findJob = new()
        {
            SeekerPos = (float3)transform.position,
            TargetPositions = TargetPositions,
            TargetDistances = TargetDistances, // Must be allocated!
            NearestTargetPositions = NearestTargetPositions,
            NearestTargetDistances = NearestTargetDistances
        };

        JobHandle sortHandle = sortJob.Schedule();
        JobHandle findHandle = findJob.Schedule(sortHandle);
        findHandle.Complete();

        // 5. Update tracked arrays (copy from results)
        trackedTransforms = new Transform[targetCount];
        trackedDistances = new float[targetCount];
        for (int i = 0; i < targetCount; i++)
        {
            trackedTransforms[i] = actualColliders[i].transform;
            trackedDistances[i] = NearestTargetDistances[i];
            trackedTransforms[i].gameObject.gameObject.transform.Find("IndicatorBlue").gameObject.SetActive(true);
        }

        // Dispose our temporary job arrays
        TargetPositions.Dispose();
        TargetDistances.Dispose();
        NearestTargetPositions.Dispose();
        NearestTargetDistances.Dispose();
    }
}


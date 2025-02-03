using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct FindNearestJob : IJob
{
    [ReadOnly] public float3 SeekerPos;
    [ReadOnly] public NativeArray<float3> TargetPositions;

    public NativeArray<float> TargetDistances; 
    public NativeArray<float> NearestTargetDistances;
    public NativeArray<float3> NearestTargetPositions;

    public void Execute()
    {
        if (TargetPositions.Length == 0) return;

        // Step 1: Binary Search for closest X coordinate
        int startIdx = TargetPositions.BinarySearch(SeekerPos, new AxisXComparer { });

        if (startIdx < 0) startIdx = ~startIdx;
        if (startIdx >= TargetPositions.Length) startIdx = TargetPositions.Length - 1;

        // Step 2: Search for `maxTargetCount` closest targets (both directions)
        FindClosestTargets(startIdx, NearestTargetPositions.Length);
    }

    void FindClosestTargets(int startIdx, int maxCount)
    {
        int left = startIdx - 1;
        int right = startIdx;
        int count = 0;

        while (count < maxCount && (left >= 0 || right < TargetPositions.Length))
        {
            float3 leftPos = left >= 0 ? TargetPositions[left] : new float3(float.MaxValue);
            float3 rightPos = right < TargetPositions.Length ? TargetPositions[right] : new float3(float.MaxValue);

            float leftDistSq = math.distancesq(SeekerPos, leftPos);
            float rightDistSq = math.distancesq(SeekerPos, rightPos);

            if (left >= 0 && (right >= TargetPositions.Length || leftDistSq < rightDistSq))
            {
                NearestTargetPositions[count] = leftPos;
                NearestTargetDistances[count] = math.sqrt(leftDistSq);
                left--;
            }
            else
            {
                NearestTargetPositions[count] = rightPos;
                NearestTargetDistances[count] = math.sqrt(rightDistSq);
                right++;
            }

            count++;
        }
    }
}

public struct AxisXComparer : IComparer<float3>
{
    public int Compare(float3 a, float3 b)
    {
        return a.x.CompareTo(b.x);
    }
}
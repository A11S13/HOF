using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct SonarDistanceJob : IJobParallelFor
{
    [ReadOnly] public float3 SeekerPos;
    [ReadOnly] public NativeArray<float3> TargetPositions;
    public NativeArray<float> Distances;

    public void Execute(int index)
    {
        Distances[index] = math.distance(SeekerPos, TargetPositions[index]);
    }
}

[BurstCompile]
public struct SonarSortJob : IJob
{
    public NativeArray<float> Distances;
    public NativeArray<int> Indices;

    public void Execute()
    {
        // Sort indices based on distances
        for (int i = 0; i < Indices.Length - 1; i++)
        {
            int minIndex = i;
            for (int j = i + 1; j < Indices.Length; j++)
            {
                if (Distances[Indices[j]] < Distances[Indices[minIndex]])
                {
                    minIndex = j;
                }
            }

            // Swap indices
            (Indices[i], Indices[minIndex]) = (Indices[minIndex], Indices[i]);
        }
    }
}
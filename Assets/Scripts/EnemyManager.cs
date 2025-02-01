using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using UnityEngine.Jobs;
using Unity.Mathematics;

public class EnemyManager : MonoBehaviour
{
    public static EnemyManager Instance { get; private set; }
    readonly List<Enemy> enemies = new List<Enemy>();

    // Global settings for enemy behavior (set by EnemySpawner at start)
    [System.Serializable]
    public struct Settings
    {
        public int rayCount;
        public float arcAngle;
        public float fRadius;
        public float cRadius;
        public float wPlayer;
        public float wCrowd;
        public float wAlign;
        public float wCohesion;
        public float wNoise;
        public bool swarm;
        public bool tPlayer;
        public bool align;
        public bool cohesion;
        public bool crowd;
        public bool noise;
        public float enemySpeed;
        public Vector3 playerPosition;
    }
    public Settings GlobalSettings;

    // (Optional) Cached player transform for dynamic update.
    Transform playerTransform;

    TransformAccessArray taArray;
    NativeArray<Vector3> enemyPositions;
    NativeArray<quaternion> enemyRotations;
    NativeArray<Enemy.EnemyData> enemyJobData;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Try to cache the player's transform.
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            playerTransform = player.transform;
    }

    public void RegisterEnemy(Enemy enemy)
    {
        if (!enemies.Contains(enemy))
            enemies.Add(enemy);
    }

    public void UnregisterEnemy(Enemy enemy)
    {
        enemies.Remove(enemy);
    }

    void Update()
    {
        int count = enemies.Count;
        if (count == 0)
            return;

        // Update the player's position if it’s moving.
        if (playerTransform != null)
            GlobalSettings.playerPosition = playerTransform.position;

        // Build arrays for the job.
        var transforms = new Transform[count];
        enemyPositions = new NativeArray<Vector3>(count, Allocator.TempJob);
        enemyRotations = new NativeArray<quaternion>(count, Allocator.TempJob);
        enemyJobData = new NativeArray<Enemy.EnemyData>(count, Allocator.TempJob);
        for (int i = 0; i < count; i++)
        {
            transforms[i] = enemies[i].transform;
            enemyPositions[i] = enemies[i].transform.position;
            enemyRotations[i] = enemies[i].transform.rotation;
            enemyJobData[i] = enemies[i].data;
        }
        taArray = new TransformAccessArray(transforms);

        // Schedule the job.
        var job = new EnemyAIJob
        {
            enemyPositions = enemyPositions,
            enemyRotations = enemyRotations,
            enemyJobData = enemyJobData,
            deltaTime = Time.deltaTime,
            enemySpeed = GlobalSettings.enemySpeed,
            playerPosition = GlobalSettings.playerPosition,
            swarm = GlobalSettings.swarm,
            tPlayer = GlobalSettings.tPlayer,
            align = GlobalSettings.align,
            cohesion = GlobalSettings.cohesion,
            crowd = GlobalSettings.crowd,
            noise = GlobalSettings.noise,
            fRadius = GlobalSettings.fRadius,
            arcAngle = GlobalSettings.arcAngle,
            cRadius = GlobalSettings.cRadius,
            wPlayer = GlobalSettings.wPlayer,
            wCrowd = GlobalSettings.wCrowd,
            wAlign = GlobalSettings.wAlign,
            wCohesion = GlobalSettings.wCohesion,
            wNoise = GlobalSettings.wNoise,
            seed = 12345u  // Consider using a more dynamic seed if needed.
        };

        JobHandle handle = job.Schedule(taArray);
        handle.Complete();

        // Write back updated enemy data.
        for (int i = 0; i < count; i++)
            enemies[i].data = enemyJobData[i];

        // Dispose temporary arrays.
        taArray.Dispose();
        enemyPositions.Dispose();
        enemyRotations.Dispose();
        enemyJobData.Dispose();
    }

    [BurstCompile]
    struct EnemyAIJob : IJobParallelForTransform
    {
        // Read-only arrays for neighbor search.
        [ReadOnly] public NativeArray<Vector3> enemyPositions;
        [ReadOnly] public NativeArray<quaternion> enemyRotations;
        // Per-enemy data updated by the job.
        public NativeArray<Enemy.EnemyData> enemyJobData;

        // Global parameters.
        public float deltaTime;
        public float enemySpeed;
        public Vector3 playerPosition;
        public bool swarm, tPlayer, align, cohesion, crowd, noise;
        public float fRadius, arcAngle, cRadius;
        public float wPlayer, wCrowd, wAlign, wCohesion, wNoise;
        public uint seed;

        public void Execute(int index, TransformAccess transform)
        {
            float3 pos = transform.position;
            quaternion rot = transform.rotation;
            float3 currentForward = math.mul(rot, new float3(0, 0, 1));
            float3 desiredForward = currentForward;
            var ed = enemyJobData[index];

            // Swarm behaviors.
            if (swarm)
            {
                float3 alignSum = float3.zero;
                int alignCount = 0;
                float3 cohesionSum = float3.zero;
                int cohesionCount = 0;
                float3 crowdAdjust = float3.zero;

                // Loop through all enemy positions to detect neighbors.
                for (int i = 0; i < enemyPositions.Length; i++)
                {
                    if (i == index) continue;
                    float3 otherPos = enemyPositions[i];
                    float dist = math.distance(pos, otherPos);
                    if (dist < fRadius)
                    {
                        float3 toOther = math.normalize(otherPos - pos);
                        // Check if within the arc.
                        float angle = math.degrees(math.acos(math.clamp(math.dot(currentForward, toOther), -1f, 1f)));
                        if (angle < arcAngle * 0.5f)
                        {
                            float3 otherForward = math.mul(enemyRotations[i], new float3(0, 0, 1));
                            alignSum += otherForward;
                            alignCount++;
                        }
                        // Cohesion: accumulate positions.
                        cohesionSum += otherPos;
                        cohesionCount++;
                        // Crowd avoidance: if too close.
                        if (dist < cRadius)
                            crowdAdjust += (pos - otherPos) / dist;
                    }
                }
                if (align && alignCount > 0)
                {
                    float3 avgForward = math.normalize(alignSum / alignCount);
                    desiredForward = math.normalize(math.lerp(desiredForward, avgForward, wAlign * deltaTime));
                }
                if (cohesion && cohesionCount > 0)
                {
                    float3 avgPos = cohesionSum / cohesionCount;
                    float3 toAvg = math.normalize(avgPos - pos);
                    desiredForward = math.normalize(math.lerp(desiredForward, toAvg, wCohesion * deltaTime));
                }
                if (crowd && math.length(crowdAdjust) > 0)
                {
                    float3 avoidance = math.normalize(crowdAdjust);
                    desiredForward = math.normalize(math.lerp(desiredForward, avoidance, wCrowd * deltaTime));
                }
                if (noise)
                {
                    ed.rotationCounter++;
                    if (ed.rotationCounter >= ed.rotationFrequency)
                    {
                        // Generate a random noise direction.
                        var rand = new Unity.Mathematics.Random(seed + (uint)index);
                        float3 noiseDir = math.normalize(new float3(rand.NextFloat(-1f, 1f), 0, rand.NextFloat(-1f, 1f)));
                        ed.targetRotation = quaternion.LookRotationSafe(noiseDir, new float3(0, 1, 0));
                        ed.rotationCounter = 0;
                        ed.rotationFrequency = (int)math.clamp(rand.NextFloat(1f, 300f), 1, 300);
                    }
                    // Blend in the noise influence.
                    quaternion noiseRot = math.slerp(quaternion.identity, ed.targetRotation, wNoise * deltaTime);
                    desiredForward = math.normalize(math.mul(noiseRot, desiredForward));
                }
            }
            // Always target the player if enabled.
            if (tPlayer)
            {
                float3 toPlayer = math.normalize((float3)playerPosition - pos);
                desiredForward = math.normalize(math.lerp(desiredForward, toPlayer, wPlayer * deltaTime));
            }
            // Compute the new rotation and update transform.
            quaternion newRot = quaternion.LookRotationSafe(desiredForward, new float3(0, 1, 0));
            transform.rotation = newRot;
            transform.position = pos + math.mul(newRot, new float3(0, 0, 1)) * enemySpeed * deltaTime;
            enemyJobData[index] = ed;
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

public class AnimaManager : MonoBehaviour
{
    public static AnimaManager Instance { get; private set; }

    [Header("Swarm Settings")]
    public int rayCount = 3;
    public float arcAngle = 90f;
    public float fRadius = 40f;
    public float cRadius = 8f;
    [Range(0, 20)] public float wPlayer = 0.2f, wCrowd = 12f, wAlign = 1f, wCohesion = 1f, wNoise = 0.15f;
    public bool swarm = true, tPlayer = true, align = true, cohesion = true, crowd = true, noise = true;
    public float baseMinSpeed = 2f;
    public float baseMaxSpeed = 10f;
    public float maxRotationSpeed = 180f;

    [Header("Obstacle Settings")]
    public LayerMask obstacleLayerMask;
    public float minObstacleScanRadius = 8f;
    public float maxObstacleScanRadius = 12f;
    public float minObstacleAvoidWeight = 1.5f;
    public float maxObstacleAvoidWeight = 2.5f;

    Transform playerTf;
    readonly List<Anima> animas = new();

    struct Settings
    {
        public float fRadius, cRadius, arcAngle;
        public float wPlayer, wCrowd, wAlign, wCohesion, wNoise;
        public bool swarm, tPlayer, align, cohesion, crowd, noise;
        public float baseMinSpeed;
        public float baseMaxSpeed;
        public float maxRotationSpeed;
        public Vector3 playerPosition;
        public float minObstacleScanRadius;
        public float maxObstacleScanRadius;
        public float minObstacleAvoidWeight;
        public float maxObstacleAvoidWeight;
    }
    Settings G;

    void Awake()
    {
        if (Instance) { Destroy(gameObject); return; }
        Instance = this;
        playerTf = GameObject.FindGameObjectWithTag("Player")?.transform;
    }

    void Start() => UpdateSettings();

    void UpdateSettings()
    {
        G = new Settings
        {
            fRadius = fRadius,
            cRadius = cRadius,
            arcAngle = arcAngle,
            wPlayer = wPlayer,
            wCrowd = wCrowd,
            wAlign = wAlign,
            wCohesion = wCohesion,
            wNoise = wNoise,
            swarm = swarm,
            tPlayer = tPlayer,
            align = align,
            cohesion = cohesion,
            crowd = crowd,
            noise = noise,
            baseMinSpeed = baseMinSpeed,
            baseMaxSpeed = baseMaxSpeed,
            maxRotationSpeed = maxRotationSpeed,
            playerPosition = playerTf ? playerTf.position : Vector3.zero,
            minObstacleScanRadius = minObstacleScanRadius,
            maxObstacleScanRadius = maxObstacleScanRadius,
            minObstacleAvoidWeight = minObstacleAvoidWeight,
            maxObstacleAvoidWeight = maxObstacleAvoidWeight
        };
    }

    public void RegisterAnima(Anima a) { if (!animas.Contains(a)) animas.Add(a); }
    public void UnregisterAnima(Anima a) => animas.Remove(a);

    void Update()
    {
        UpdateSettings();
        var actives = animas.Where(a => a && a.gameObject.activeSelf).ToList();
        int n = actives.Count; if (n == 0) return;

        var ta = new TransformAccessArray(n);
        var posArr = new NativeArray<float3>(n, Allocator.TempJob);
        var rotArr = new NativeArray<quaternion>(n, Allocator.TempJob);
        var dataArr = new NativeArray<Anima.AnimaData>(n, Allocator.TempJob);
        var speedMultipliers = new NativeArray<float>(n, Allocator.TempJob);

        for (int i = 0; i < n; i++)
        {
            ta.Add(actives[i].transform);
            posArr[i] = actives[i].transform.position;
            rotArr[i] = actives[i].transform.rotation;
            dataArr[i] = actives[i].data;
            speedMultipliers[i] = actives[i].animaSpeedMultiplier;
        }

        var cols = Physics.OverlapSphere(G.playerPosition, 300f, obstacleLayerMask);
        var obsArr = new NativeArray<float4>(cols.Length, Allocator.TempJob);
        for (int i = 0; i < cols.Length; i++)
        {
            var b = cols[i].bounds;
            obsArr[i] = new float4(b.center, b.extents.magnitude);
        }

        new AnimaAIJob
        {
            animaPositions = posArr,
            animaRotations = rotArr,
            animaJobData = dataArr,
            speedMultipliers = speedMultipliers,
            obstaclePositions = obsArr,
            obstacleCount = obsArr.Length,
            minObstacleScanRadius = G.minObstacleScanRadius,
            maxObstacleScanRadius = G.maxObstacleScanRadius,
            minObstacleAvoidWeight = G.minObstacleAvoidWeight,
            maxObstacleAvoidWeight = G.maxObstacleAvoidWeight,
            deltaTime = Time.deltaTime,
            fRadius = G.fRadius,
            cRadius = G.cRadius,
            arcAngle = G.arcAngle,
            wPlayer = G.wPlayer,
            wCrowd = G.wCrowd,
            wAlign = G.wAlign,
            wCohesion = G.wCohesion,
            wNoise = G.wNoise,
            swarm = G.swarm,
            tPlayer = G.tPlayer,
            align = G.align,
            cohesion = G.cohesion,
            crowd = G.crowd,
            noise = G.noise,
            baseMinSpeed = G.baseMinSpeed,
            baseMaxSpeed = G.baseMaxSpeed,
            maxRotationSpeed = G.maxRotationSpeed,
            playerPosition = G.playerPosition,
            seed = (uint)UnityEngine.Random.Range(1, int.MaxValue)
        }.Schedule(ta).Complete();

        for (int i = 0; i < n; i++) actives[i].data = dataArr[i];

        ta.Dispose(); posArr.Dispose(); rotArr.Dispose();
        dataArr.Dispose(); obsArr.Dispose(); speedMultipliers.Dispose();
    }

    [BurstCompile]
    struct AnimaAIJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<float3> animaPositions;
        [ReadOnly] public NativeArray<quaternion> animaRotations;
        public NativeArray<Anima.AnimaData> animaJobData;
        [ReadOnly] public NativeArray<float> speedMultipliers;

        [ReadOnly] public NativeArray<float4> obstaclePositions;
        public int obstacleCount;
        public float minObstacleScanRadius;
        public float maxObstacleScanRadius;
        public float minObstacleAvoidWeight;
        public float maxObstacleAvoidWeight;
        public float deltaTime;
        public float fRadius, cRadius, arcAngle;
        public float wPlayer, wCrowd, wAlign, wCohesion, wNoise;
        public bool swarm, tPlayer, align, cohesion, crowd, noise;
        public float baseMinSpeed;
        public float baseMaxSpeed;
        public float maxRotationSpeed;
        public float3 playerPosition;
        public uint seed;

        public void Execute(int i, TransformAccess tx)
        {
            float3 pos = tx.position;
            quaternion rot = tx.rotation;
            float3 forward = math.mul(rot, new float3(0, 0, 1));
            float3 desired = forward;
            var ed = animaJobData[i];
            var rnd = new Unity.Mathematics.Random(seed + (uint)i);

            // Speed calculations
            float speedMultiplier = speedMultipliers[i];
            float minSpeed = baseMinSpeed * speedMultiplier;
            float maxSpeed = baseMaxSpeed * speedMultiplier;

            // Obstacle avoidance
            float obsInf = 0;
            float3 obsSum = 0;
            for (int j = 0; j < obstacleCount; j++)
            {
                float3 toObs = obstaclePositions[j].xyz - pos;
                float d = math.length(toObs);
                float rO = obstaclePositions[j].w;
                float scanRadius = rnd.NextFloat(minObstacleScanRadius, maxObstacleScanRadius);

                if (d < scanRadius + rO)
                {
                    float w = ((scanRadius + rO) - d) / (scanRadius + rO);
                    obsSum -= math.normalize(toObs) * w;
                    obsInf += w;
                }
            }
            if (obsInf > 0)
            {
                float avoidWeight = rnd.NextFloat(minObstacleAvoidWeight, maxObstacleAvoidWeight);
                desired = math.lerp(desired, math.normalize(obsSum), avoidWeight * deltaTime);
            }

            // Neighbor processing
            float3 alignSum = 0, cohSum = 0, sepSum = 0;
            int alignCount = 0, cohCount = 0;
            for (int j = 0; j < animaPositions.Length; j++)
            {
                if (j == i) continue;

                float d = math.distance(pos, animaPositions[j]);
                if (d < fRadius)
                {
                    float3 neighborForward = math.mul(animaRotations[j], new float3(0, 0, 1));
                    alignSum += neighborForward;
                    alignCount++;

                    cohSum += animaPositions[j];
                    cohCount++;

                    if (d < cRadius)
                    {
                        sepSum += (pos - animaPositions[j]) / math.max(d, 0.0001f);
                    }
                }
            }

            // Alignment
            if (align && alignCount > 0)
            {
                float3 alignmentForce = math.normalize(alignSum / alignCount);
                desired += alignmentForce * wAlign;
            }

            // Cohesion
            if (cohesion && cohCount > 0)
            {
                float3 cohesionForce = math.normalize((cohSum / cohCount) - pos);
                desired += cohesionForce * wCohesion;
            }

            // Separation
            if (crowd && math.length(sepSum) > 0)
            {
                float3 separationForce = math.normalize(sepSum);
                desired += separationForce * wCrowd;
            }

            // Noise
            if (noise)
            {
                float3 noiseForce = rnd.NextFloat3Direction() * wNoise;
                desired += noiseForce;
            }

            // Player attraction
            if (tPlayer)
            {
                float3 toPlayer = math.normalize(playerPosition - pos);
                desired += toPlayer * wPlayer;
            }

            // Normalize and stabilize
            desired = math.normalizesafe(desired);
            desired += new float3(0.01f, 0, 0.01f); // Stabilization factor
            desired = math.normalize(desired);

            // Speed calculation
            float speed = obsInf > 0 ? maxSpeed : math.lerp(minSpeed, maxSpeed,
                math.length(desired) * 0.5f + 0.5f);

            // Rotation calculation
            float3 horizDir = math.normalizesafe(new float3(desired.x, 0, desired.z));
            quaternion targetRot = quaternion.LookRotation(horizDir, math.up());
            float angle = math.acos(math.clamp(math.dot(math.forward(rot), horizDir), -1f, 1f));
            float rotateT = math.min(1f, math.radians(maxRotationSpeed) * deltaTime / math.max(angle, 0.0001f));

            tx.rotation = math.slerp(rot, targetRot, rotateT);
            tx.position = pos + math.mul(tx.rotation, new float3(0, 0, 1)) * speed * deltaTime;
            animaJobData[i] = ed;
        }
    }
}
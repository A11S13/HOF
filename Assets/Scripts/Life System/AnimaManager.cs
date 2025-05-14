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
    public float wPlayer = 0.2f, wCrowd = 12f, wAlign = 1f, wCohesion = 1f, wNoise = 0.15f;
    public bool swarm = true, tPlayer = true, align = true, cohesion = true, crowd = true, noise = true;
    public float minSpeed = 2f, maxSpeed = 10f, maxRotationSpeed = 180f;

    [Header("Obstacle Settings")]
    public LayerMask obstacleLayerMask;
    public float obstacleScanRadius = 10f, obstacleAvoidWeight = 2f;

    Transform playerTf;
    readonly List<Anima> animas = new();

    struct Settings
    {
        public float fRadius, cRadius, arcAngle;
        public float wPlayer, wCrowd, wAlign, wCohesion, wNoise;
        public bool swarm, tPlayer, align, cohesion, crowd, noise;
        public float minSpeed, maxSpeed, maxRotationSpeed;
        public Vector3 playerPosition;
        public float obstacleScanRadius, obstacleAvoidWeight;
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
            minSpeed = minSpeed,
            maxSpeed = maxSpeed,
            maxRotationSpeed = maxRotationSpeed,
            playerPosition = playerTf ? playerTf.position : Vector3.zero,
            obstacleScanRadius = obstacleScanRadius,
            obstacleAvoidWeight = obstacleAvoidWeight
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
        for (int i = 0; i < n; i++)
        {
            ta.Add(actives[i].transform);
            posArr[i] = actives[i].transform.position;
            rotArr[i] = actives[i].transform.rotation;
            dataArr[i] = actives[i].data;
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
            obstaclePositions = obsArr,
            obstacleCount = obsArr.Length,
            obstacleAvoidRadius = G.obstacleScanRadius,
            obstacleAvoidWeight = G.obstacleAvoidWeight,
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
            minSpeed = G.minSpeed,
            maxSpeed = G.maxSpeed,
            maxRotationSpeed = G.maxRotationSpeed,
            playerPosition = G.playerPosition,
            seed = (uint)UnityEngine.Random.Range(1, int.MaxValue)
        }.Schedule(ta).Complete();

        for (int i = 0; i < n; i++) actives[i].data = dataArr[i];

        ta.Dispose(); posArr.Dispose(); rotArr.Dispose(); dataArr.Dispose(); obsArr.Dispose();
    }

    [BurstCompile]
    struct AnimaAIJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<float3> animaPositions;
        [ReadOnly] public NativeArray<quaternion> animaRotations;
        public NativeArray<Anima.AnimaData> animaJobData;

        [ReadOnly] public NativeArray<float4> obstaclePositions;
        public int obstacleCount;
        public float obstacleAvoidRadius, obstacleAvoidWeight;
        public float deltaTime;
        public float fRadius, cRadius, arcAngle;
        public float wPlayer, wCrowd, wAlign, wCohesion, wNoise;
        public bool swarm, tPlayer, align, cohesion, crowd, noise;
        public float minSpeed, maxSpeed, maxRotationSpeed;
        public float3 playerPosition;
        public uint seed;

        public void Execute(int i, TransformAccess tx)
        {
            float3 pos = tx.position;
            quaternion rot = tx.rotation;
            float3 forward = math.mul(rot, new float3(0, 0, 1));
            float3 desired = forward;
            var ed = animaJobData[i];

            float obsInf = 0; float3 obsSum = 0;
            for (int j = 0; j < obstacleCount; j++)
            {
                float3 toObs = obstaclePositions[j].xyz - pos;
                float d = math.length(toObs), rO = obstaclePositions[j].w;
                if (d < obstacleAvoidRadius + rO)
                {
                    float w = ((obstacleAvoidRadius + rO) - d) / (obstacleAvoidRadius + rO);
                    obsSum -= math.normalize(toObs) * w; obsInf += w;
                }
            }
            if (obsInf > 0)
                desired = math.normalize(math.lerp(desired, math.normalize(obsSum / obsInf), obstacleAvoidWeight * deltaTime));

            float3 alignSum = 0, cohSum = 0, sepSum = 0;
            int alignCount = 0, cohCount = 0;
            for (int j = 0; j < animaPositions.Length; j++) if (j != i)
                {
                    float d = math.distance(pos, animaPositions[j]);
                    if (d < fRadius)
                    {
                        alignSum += math.mul(animaRotations[j], new float3(0, 0, 1)); alignCount++;
                        cohSum += animaPositions[j]; cohCount++;
                        if (d < cRadius) sepSum += (pos - animaPositions[j]) / d;
                    }
                }

            if (align && alignCount > 0)
                desired = math.normalize(math.lerp(desired, math.normalize(alignSum / alignCount), wAlign * deltaTime));
            if (cohesion && cohCount > 0)
                desired = math.normalize(math.lerp(desired, math.normalize((cohSum / cohCount) - pos), wCohesion * deltaTime));
            float crowdInf = crowd ? math.length(sepSum) : 0;
            if (crowd && crowdInf > 0)
                desired = math.normalize(math.lerp(desired, math.normalize(sepSum), wCrowd * deltaTime));

            float wanderInf = 0;
            if (noise)
            {
                ed.rotationCounter++;
                if (ed.rotationCounter >= ed.rotationFrequency)
                {
                    var rnd = new Unity.Mathematics.Random(seed + (uint)i);
                    ed.targetRotation = quaternion.LookRotationSafe(math.normalize(new float3(rnd.NextFloat(-1f, 1f), 0, rnd.NextFloat(-1f, 1f))), math.up());
                    ed.rotationCounter = 0;
                    ed.rotationFrequency = (int)math.clamp(rnd.NextFloat(1f, 30f), 1f, 30f);
                }
                desired = math.normalize(math.mul(math.slerp(quaternion.identity, ed.targetRotation, wNoise * deltaTime), desired));
                wanderInf = wNoise;
            }

            float playerInf = 0;
            if (tPlayer)
            {
                var toPlayer = math.normalize(playerPosition - pos);
                desired = math.normalize(math.lerp(desired, toPlayer, wPlayer * deltaTime));
                playerInf = wPlayer * math.clamp(math.distance(playerPosition, pos) / fRadius, 0, 1);
            }

            float speed = obsInf > 0 ? maxSpeed :
                math.lerp(minSpeed, maxSpeed,
                    math.clamp((obsInf + crowdInf + (align ? wAlign : 0) + playerInf + wanderInf) /
                    (obstacleAvoidWeight + wCrowd + wAlign + wPlayer + wNoise), 0, 1));

            float3 horiz = math.normalize(new float3(desired.x, 0, desired.z));
            quaternion tgt = math.lengthsq(horiz) > 0.001f ? quaternion.LookRotationSafe(horiz, math.up()) : rot;
            float maxRad = math.radians(maxRotationSpeed) * deltaTime;
            float dot = math.clamp(math.dot(rot.value, tgt.value), -1f, 1f);
            quaternion newR = math.slerp(rot, tgt, math.min(1f, maxRad / math.acos(dot)));

            tx.rotation = newR;
            tx.position = pos + math.mul(newR, new float3(0, 0, 1)) * speed * deltaTime;
            animaJobData[i] = ed;
        }
    }
}

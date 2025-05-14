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
    public float wPlayer = 0.2f;
    public float wCrowd = 12f;
    public float wAlign = 1f;
    public float wCohesion = 1f;
    public float wNoise = 0.15f;
    public bool swarm = true;
    public bool tPlayer = true;
    public bool align = true;
    public bool cohesion = true;
    public bool crowd = true;
    public bool noise = true;
    public float animaSpeed = 5f;
    public float maxRotationSpeed = 180f;

    [Header("Obstacle Settings")]
    public LayerMask obstacleLayerMask;
    public float obstacleScanRadius = 10f;
    public float obstacleAvoidWeight = 2f;

    Transform playerTf;
    readonly List<Anima> animas = new List<Anima>();

    struct Settings
    {
        public float fRadius, cRadius, arcAngle;
        public float wPlayer, wCrowd, wAlign, wCohesion, wNoise;
        public bool swarm, tPlayer, align, cohesion, crowd, noise;
        public float animaSpeed, maxRotationSpeed;
        public Vector3 playerPosition;
        public float obstacleScanRadius, obstacleAvoidWeight;
    }
    Settings G;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        playerTf = GameObject.FindGameObjectWithTag("Player")?.transform;
    }

    void Start()
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
            animaSpeed = animaSpeed,
            maxRotationSpeed = maxRotationSpeed,
            playerPosition = playerTf ? playerTf.position : Vector3.zero,
            obstacleScanRadius = obstacleScanRadius,
            obstacleAvoidWeight = obstacleAvoidWeight
        };
        Anima.animaSpeed = animaSpeed;
    }

    public void RegisterAnima(Anima a) { if (!animas.Contains(a)) animas.Add(a); }
    public void UnregisterAnima(Anima a) { animas.Remove(a); }

    void Update()
    {
        // propagate inspector tweaks
        G.fRadius = fRadius;
        G.cRadius = cRadius;
        G.arcAngle = arcAngle;
        G.wPlayer = wPlayer;
        G.wCrowd = wCrowd;
        G.wAlign = wAlign;
        G.wCohesion = wCohesion;
        G.wNoise = wNoise;
        G.swarm = swarm;
        G.tPlayer = tPlayer;
        G.align = align;
        G.cohesion = cohesion;
        G.crowd = crowd;
        G.noise = noise;
        G.animaSpeed = animaSpeed;
        G.maxRotationSpeed = maxRotationSpeed;
        G.obstacleScanRadius = obstacleScanRadius;
        G.obstacleAvoidWeight = obstacleAvoidWeight;

        G.playerPosition = playerTf ? playerTf.position : Vector3.zero;

        var act = animas.Where(a => a && a.gameObject.activeSelf).ToList();
        int n = act.Count;
        if (n == 0) return;

        var ta = new TransformAccessArray(n);
        var posArr = new NativeArray<float3>(n, Allocator.TempJob);
        var rotArr = new NativeArray<quaternion>(n, Allocator.TempJob);
        var dataArr = new NativeArray<Anima.AnimaData>(n, Allocator.TempJob);
        for (int i = 0; i < n; i++)
        {
            var t = act[i].transform;
            ta.Add(t);
            posArr[i] = t.position;
            rotArr[i] = t.rotation;
            dataArr[i] = act[i].data;
        }

        var cols = Physics.OverlapSphere(G.playerPosition, G.obstacleScanRadius, obstacleLayerMask);
        var obsArr = new NativeArray<float3>(cols.Length, Allocator.TempJob);
        for (int i = 0; i < cols.Length; i++)
            obsArr[i] = cols[i].transform.position;

        var job = new AnimaAIJob
        {
            animaPositions = posArr,
            animaRotations = rotArr,
            animaJobData = dataArr,
            obstaclePositions = obsArr,
            obstacleCount = obsArr.Length,
            obstacleAvoidRadius = G.obstacleScanRadius,
            obstacleAvoidWeight = G.obstacleAvoidWeight,
            deltaTime = Time.deltaTime,
            animaSpeed = G.animaSpeed,
            maxRotationSpeed = G.maxRotationSpeed,
            playerPosition = G.playerPosition,
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
            seed = (uint)UnityEngine.Random.Range(1, int.MaxValue)
        };
        job.Schedule(ta).Complete();

        for (int i = 0; i < n; i++) act[i].data = dataArr[i];
        ta.Dispose(); posArr.Dispose(); rotArr.Dispose(); dataArr.Dispose(); obsArr.Dispose();
    }

    [BurstCompile]
    struct AnimaAIJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<float3> animaPositions;
        [ReadOnly] public NativeArray<quaternion> animaRotations;
        public NativeArray<Anima.AnimaData> animaJobData;

        [ReadOnly] public NativeArray<float3> obstaclePositions;
        public int obstacleCount;
        public float obstacleAvoidRadius;
        public float obstacleAvoidWeight;

        public float deltaTime, animaSpeed, maxRotationSpeed;
        public float fRadius, cRadius, arcAngle;
        public float wPlayer, wCrowd, wAlign, wCohesion, wNoise;
        public bool swarm, tPlayer, align, cohesion, crowd, noise;
        public Vector3 playerPosition;
        public uint seed;

        public void Execute(int i, TransformAccess tx)
        {
            float3 pos = tx.position;
            quaternion r = tx.rotation;
            float3 forward = math.mul(r, new float3(0, 0, 1));
            float3 desired = forward;
            var ed = animaJobData[i];

            // 360° obstacle avoidance
            if (obstacleCount > 0)
            {
                float3 tot = 0; float w = 0;
                for (int j = 0; j < obstacleCount; j++)
                {
                    float3 toObs = obstaclePositions[j] - pos;
                    float d = math.length(toObs);
                    if (d < obstacleAvoidRadius)
                    {
                        var dir = math.normalize(toObs);
                        float a = math.degrees(math.acos(math.clamp(math.dot(forward, dir), -1f, 1f)));
                        float dw = math.lerp(1f, 0.2f, a / 180f);
                        float pw = (obstacleAvoidRadius - d) / obstacleAvoidRadius;
                        float wt = dw * pw;
                        tot += -dir * wt;
                        w += wt;
                    }
                }
                if (w > 0)
                    desired = math.normalize(math.lerp(desired, math.normalize(tot / w), obstacleAvoidWeight * deltaTime));
            }

            // boid behaviors
            if (swarm)
            {
                float3 aSum = 0, cSum = 0, cAdj = 0;
                int aC = 0, cC = 0;
                for (int j = 0; j < animaPositions.Length; j++) if (j != i)
                    {
                        float3 op = animaPositions[j];
                        float d = math.distance(pos, op);
                        if (d < fRadius)
                        {
                            float3 toO = math.normalize(op - pos);
                            float ang = math.degrees(math.acos(math.clamp(math.dot(forward, toO), -1f, 1f)));
                            if (ang < arcAngle * 0.5f)
                            { aSum += math.mul(animaRotations[j], new float3(0, 0, 1)); aC++; }
                            cSum += op; cC++;
                            if (d < cRadius) cAdj += (pos - op) / d;
                        }
                    }
                if (align && aC > 0)
                    desired = math.normalize(math.lerp(desired, math.normalize(aSum / aC), wAlign * deltaTime));
                if (cohesion && cC > 0)
                {
                    var toAvg = math.normalize(cSum / cC - pos);
                    desired = math.normalize(math.lerp(desired, toAvg, wCohesion * deltaTime));
                }
                if (crowd && math.length(cAdj) > 0)
                    desired = math.normalize(math.lerp(desired, math.normalize(cAdj), wCrowd * deltaTime));
                if (noise)
                {
                    ed.rotationCounter++;
                    if (ed.rotationCounter >= ed.rotationFrequency)
                    {
                        var rnd = new Unity.Mathematics.Random(seed + (uint)i);
                        float3 nd = math.normalize(new float3(rnd.NextFloat(-1f, 1f), 0, rnd.NextFloat(-1f, 1f)));
                        ed.targetRotation = quaternion.LookRotationSafe(nd, math.up());
                        ed.rotationCounter = 0;
                        ed.rotationFrequency = (int)math.clamp(rnd.NextFloat(1f, 30f), 1f, 30f);
                    }
                    var nr = math.slerp(quaternion.identity, ed.targetRotation, wNoise * deltaTime);
                    desired = math.normalize(math.mul(nr, desired));
                }
            }

            // target player
            if (tPlayer)
                desired = math.normalize(math.lerp(desired,
                    math.normalize((float3)playerPosition - pos), wPlayer * deltaTime));

            // apply transform
            var horiz = math.normalize(new float3(desired.x, 0, desired.z));
            quaternion tgt = math.lengthsq(horiz) > 0.001f
                ? quaternion.LookRotationSafe(horiz, math.up())
                : r;
            float maxRad = math.radians(maxRotationSpeed) * deltaTime;
            float dotV = math.clamp(math.dot(r.value, tgt.value), -1f, 1f);
            var newR = math.slerp(r, tgt, math.min(1f, maxRad / math.acos(dotV)));

            tx.rotation = newR;
            float3 move = math.mul(newR, new float3(0, 0, 1)) * animaSpeed * deltaTime;
            tx.position = pos + move;
            animaJobData[i] = ed;
        }
    }
}

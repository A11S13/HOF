using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using UnityEngine.Jobs;
using Unity.Mathematics;
using Random = UnityEngine.Random;
using System.Linq;

public class AnimaManager : MonoBehaviour
{
    public static AnimaManager Instance { get; private set; }
    readonly List<Anima> animas = new List<Anima>();

    // Global settings for anima behavior (set by AnimaSpawner at start)
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
        public float animaSpeed;
        public float maxRotationSpeed; // Added max rotation speed
        public Vector3 playerPosition;
    }
    public Settings GlobalSettings;

    // Cached player transform for dynamic update.
    Transform playerTransform;

    TransformAccessArray taArray;
    NativeArray<Vector3> animaPositions;
    NativeArray<quaternion> animaRotations;
    NativeArray<Anima.AnimaData> animaJobData;

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
    public bool noise = true;
    public bool crowd = true;
    public float animaSpeed = 5f; // Example default speed
    public float maxRotationSpeed = 180f; // Degrees per second

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

    void Start()
    {
        // Initialize global settings. Player position will be updated in Update.
        GlobalSettings = new Settings
        {
            rayCount = rayCount,
            arcAngle = arcAngle,
            fRadius = fRadius,
            cRadius = cRadius,
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
            playerPosition = playerTransform != null ? playerTransform.position : Vector3.zero
        };

        Anima.animaSpeed = animaSpeed; // Ensure the static variable is also updated.
    }

    public void RegisterAnima(Anima anima)
    {
        if (!animas.Contains(anima))
            animas.Add(anima);
    }

    public void UnregisterAnima(Anima anima)
    {
        animas.Remove(anima);
    }

    void Update()
    {
        // Dynamically update the player position in GlobalSettings every frame.
        if (playerTransform != null)
        {
            GlobalSettings.playerPosition = playerTransform.position;
        }
        else
        {
            // If the player is not found, you might want to handle this case
            // (e.g., set playerPosition to a default or zero vector).
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerTransform = player.transform;
            else
                GlobalSettings.playerPosition = Vector3.zero;
        }

        var activeAnimas = animas.Where(e => e != null && e.gameObject.activeSelf).ToList();
        int count = activeAnimas.Count;
        if (count == 0)
            return;

        var transforms = new Transform[count];
        animaPositions = new NativeArray<Vector3>(count, Allocator.TempJob);
        animaRotations = new NativeArray<quaternion>(count, Allocator.TempJob);
        animaJobData = new NativeArray<Anima.AnimaData>(count, Allocator.TempJob);

        for (int i = 0; i < count; i++)
        {
            transforms[i] = activeAnimas[i].transform;
            animaPositions[i] = activeAnimas[i].transform.position;
            animaRotations[i] = activeAnimas[i].transform.rotation;
            animaJobData[i] = activeAnimas[i].data;
        }
        taArray = new TransformAccessArray(transforms);

        // Schedule the job.
        var job = new AnimaAIJob
        {
            animaPositions = animaPositions,
            animaRotations = animaRotations,
            animaJobData = animaJobData,
            deltaTime = Time.deltaTime,
            animaSpeed = GlobalSettings.animaSpeed,
            maxRotationSpeed = GlobalSettings.maxRotationSpeed, // Pass max rotation speed to the job
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
            seed = 12345u // Consider using a more dynamic seed if needed.
        };

        JobHandle handle = job.Schedule(taArray);
        handle.Complete();

        // Write back updated anima data.
        for (int i = 0; i < count; i++)
            animas[i].data = animaJobData[i];

        // Dispose temporary arrays.
        taArray.Dispose();
        animaPositions.Dispose();
        animaRotations.Dispose();
        animaJobData.Dispose();
    }

    [BurstCompile]
    struct AnimaAIJob : IJobParallelForTransform
    {
        // Read-only arrays for neighbor search.
        [ReadOnly] public NativeArray<Vector3> animaPositions;
        [ReadOnly] public NativeArray<quaternion> animaRotations;
        // Per-anima data updated by the job.
        public NativeArray<Anima.AnimaData> animaJobData;

        // Global parameters.
        public float deltaTime;
        public float animaSpeed;
        public float maxRotationSpeed; // Added max rotation speed in the job
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
            var ed = animaJobData[index];

            // Swarm behaviors.
            if (swarm)
            {
                float3 alignSum = float3.zero;
                int alignCount = 0;
                float3 cohesionSum = float3.zero;
                int cohesionCount = 0;
                float3 crowdAdjust = float3.zero;

                // Loop through all anima positions to detect neighbors.
                for (int i = 0; i < animaPositions.Length; i++)
                {
                    if (i == index) continue;
                    float3 otherPos = animaPositions[i];
                    float dist = math.distance(pos, otherPos);
                    if (dist < fRadius)
                    {
                        float3 toOther = math.normalize(otherPos - pos);
                        // Check if within the arc.
                        float angle = math.degrees(math.acos(math.clamp(math.dot(currentForward, toOther), -1f, 1f)));
                        if (angle < arcAngle * 0.5f)
                        {
                            float3 otherForward = math.mul(animaRotations[i], new float3(0, 0, 1));
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
                        ed.rotationFrequency = (int)math.clamp(rand.NextFloat(1f, 30f), 1, 30);
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

            // Project desiredForward onto the XZ plane
            float3 horizontalForward = math.normalize(new float3(desiredForward.x, 0f, desiredForward.z));

            // Compute the target rotation based on the horizontal forward direction.
            quaternion targetRotation = quaternion.identity;
            if (math.lengthsq(horizontalForward) > 0.001f) // Avoid zero vector
            {
                targetRotation = quaternion.LookRotationSafe(horizontalForward, math.up());
            }
            else
            {
                targetRotation = rot; // If no horizontal direction, maintain current rotation
            }
            
            // Smoothly interpolate the rotation with a maximum speed.
            float maxRadiansDelta = math.radians(maxRotationSpeed) * deltaTime;
            quaternion newRot = math.slerp(rot, targetRotation, math.min(1f, maxRadiansDelta / math.acos(math.clamp(math.dot(rot.value, targetRotation.value), -1f, 1f))));

            transform.rotation = newRot;
            transform.position = pos + math.mul(newRot, new float3(0, 0, 1)) * animaSpeed * deltaTime;
            animaJobData[index] = ed;
        }
    }
}
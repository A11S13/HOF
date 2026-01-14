using System.Collections.Generic;
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
    public float obstacleUpdateInterval = 0.1f;

    [Header("Spatial Partitioning")]
    [SerializeField] private float spatialCellSize = 40f;
    [SerializeField] private bool useSpatialGrid = true;

    [Header("Despawn Settings")]
    [SerializeField] private float despawnCheckInterval = 0.2f;
    [SerializeField] private int animasPerCheck = 20;
    [SerializeField] private bool debugDespawn = false;

    Transform playerTf;
    // The Master List: Contains both Active and Inactive (Pooled) objects
    readonly List<Anima> animas = new();

    // Reusable collections
    private readonly List<Anima> _activeAnimasCache = new List<Anima>(200);

    private float _despawnCheckTimer = 0f;
    private int _despawnCheckIndex = 0;
    private WorldDataManager _dataManager;
    private WorldProcessor _worldProcessor;

    private float _obstacleUpdateTimer = 0f;
    private NativeArray<float4> _cachedObstacles;
    private bool _obstaclesInitialized = false;

    private NativeParallelMultiHashMap<int, int> _spatialGrid;
    private bool _spatialGridInitialized = false;
    private int _lastGridCapacity = 0;

    private int _totalDespawnedThisSecond = 0;
    private float _debugTimer = 0f;

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

        _dataManager = FindFirstObjectByType<WorldDataManager>();
        _worldProcessor = FindFirstObjectByType<WorldProcessor>();

        if (_dataManager == null) Debug.LogError("AnimaManager: WorldDataManager not found!");
        if (_worldProcessor == null) Debug.LogError("AnimaManager: WorldProcessor not found!");

        _spatialGrid = new NativeParallelMultiHashMap<int, int>(200, Allocator.Persistent);
        _spatialGridInitialized = true;
        _lastGridCapacity = 200;
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

    public void RegisterAnima(Anima a)
    {
        if (!animas.Contains(a))
        {
            animas.Add(a);
            if (debugDespawn) Debug.Log($"Registered anima. Total: {animas.Count}");
        }
    }

    public void UnregisterAnima(Anima a)
    {
        animas.Remove(a);
        if (debugDespawn) Debug.Log($"Unregistered anima. Total: {animas.Count}");
    }

    void Update()
    {
        UpdateSettings();
        ProcessAnimaAI();
        ProcessDespawnChecks();

        if (debugDespawn)
        {
            _debugTimer += Time.deltaTime;
            if (_debugTimer >= 1f)
            {
                int activeCount = 0;
                for (int i = 0; i < animas.Count; i++)
                {
                    if (animas[i] && animas[i].gameObject.activeSelf)
                        activeCount++;
                }

                Debug.Log($"[AnimaManager] MasterList: {animas.Count} | Active: {activeCount} | " +
                         $"Despawned/sec: {_totalDespawnedThisSecond} | " +
                         $"Active Quadrants: {_worldProcessor?._currentActiveQuadrants.Count ?? 0}");
                _totalDespawnedThisSecond = 0;
                _debugTimer = 0f;
            }
        }
    }

    private void ProcessAnimaAI()
    {
        // Rebuild the active cache for the Job System
        _activeAnimasCache.Clear();
        for (int i = 0; i < animas.Count; i++)
        {
            if (animas[i] && animas[i].gameObject.activeSelf)
                _activeAnimasCache.Add(animas[i]);
        }

        int n = _activeAnimasCache.Count;
        if (n == 0) return;

        // ... (Job Setup logic remains unchanged) ...
        var friendlyArr = new NativeArray<bool>(n, Allocator.TempJob);
        var ta = new TransformAccessArray(n);
        var posArr = new NativeArray<float3>(n, Allocator.TempJob);
        var rotArr = new NativeArray<quaternion>(n, Allocator.TempJob);
        var dataArr = new NativeArray<Anima.AnimaData>(n, Allocator.TempJob);
        var speedMultipliers = new NativeArray<float>(n, Allocator.TempJob);

        for (int i = 0; i < n; i++)
        {
            var anima = _activeAnimasCache[i];
            ta.Add(anima.transform);
            posArr[i] = anima.transform.position;
            rotArr[i] = anima.transform.rotation;
            dataArr[i] = anima.data;
            speedMultipliers[i] = anima.animaSpeedMultiplier;
            friendlyArr[i] = anima.isFriendly;
        }

        UpdateObstacles();
        BuildSpatialGrid(posArr);

        new AnimaAIJob
        {
            animaPositions = posArr,
            animaRotations = rotArr,
            animaJobData = dataArr,
            speedMultipliers = speedMultipliers,
            animaFriendly = friendlyArr,
            spatialGrid = _spatialGrid,
            spatialCellSize = spatialCellSize,
            useSpatialGrid = useSpatialGrid,
            obstaclePositions = _cachedObstacles,
            obstacleCount = _cachedObstacles.IsCreated ? _cachedObstacles.Length : 0,
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

        for (int i = 0; i < n; i++)
            _activeAnimasCache[i].data = dataArr[i];

        ta.Dispose();
        posArr.Dispose();
        rotArr.Dispose();
        dataArr.Dispose();
        speedMultipliers.Dispose();
        friendlyArr.Dispose();
    }

    private void UpdateObstacles()
    {
        _obstacleUpdateTimer += Time.deltaTime;
        if (_obstacleUpdateTimer >= obstacleUpdateInterval || !_obstaclesInitialized)
        {
            _obstacleUpdateTimer = 0f;
            if (_cachedObstacles.IsCreated) _cachedObstacles.Dispose();

            Collider[] tempBuffer = new Collider[100];
            int count = Physics.OverlapSphereNonAlloc(G.playerPosition, 300f, tempBuffer, obstacleLayerMask);

            _cachedObstacles = new NativeArray<float4>(count, Allocator.Persistent);
            for (int i = 0; i < count; i++)
            {
                var b = tempBuffer[i].bounds;
                _cachedObstacles[i] = new float4(b.center, b.extents.magnitude);
            }
            _obstaclesInitialized = true;
        }
    }

    private void BuildSpatialGrid(NativeArray<float3> positions)
    {
        if (!useSpatialGrid) return;
        int count = positions.Length;
        if (count > _lastGridCapacity)
        {
            if (_spatialGrid.IsCreated) _spatialGrid.Dispose();
            _lastGridCapacity = Mathf.NextPowerOfTwo(count);
            _spatialGrid = new NativeParallelMultiHashMap<int, int>(_lastGridCapacity, Allocator.Persistent);
        }
        _spatialGrid.Clear();
        for (int i = 0; i < count; i++)
        {
            int cellKey = GetCellKey(positions[i], spatialCellSize);
            _spatialGrid.Add(cellKey, i);
        }
    }

    private int GetCellKey(float3 position, float cellSize)
    {
        int x = (int)math.floor(position.x / cellSize);
        int z = (int)math.floor(position.z / cellSize);
        return (x * 73856093) ^ (z * 19349663);
    }

    private void ProcessDespawnChecks()
    {
        // Safety check for dependencies
        if (_dataManager == null || _worldProcessor == null)
        {
            if (_dataManager == null) _dataManager = FindFirstObjectByType<WorldDataManager>();
            if (_worldProcessor == null) _worldProcessor = FindFirstObjectByType<WorldProcessor>();
            if (_dataManager == null || _worldProcessor == null) return;
        }

        // Use the Master List 'animas' instead of '_activeAnimasCache'.
        // This list is stable (indices don't shift when objects disable).
        int totalAnimas = animas.Count;
        if (totalAnimas == 0) return;

        _despawnCheckTimer += Time.deltaTime;

        if (_despawnCheckTimer >= despawnCheckInterval)
        {
            _despawnCheckTimer = 0f;

            int checkedThisFrame = 0;
            int despawnedThisFrame = 0;

            // Loop through the allotted number of checks
            while (checkedThisFrame < animasPerCheck)
            {
                // Wrap around logic
                if (_despawnCheckIndex >= totalAnimas)
                    _despawnCheckIndex = 0;

                // Validate index
                if (_despawnCheckIndex < animas.Count)
                {
                    var anima = animas[_despawnCheckIndex];

                    // CRITICAL FIX: Only check if Active. 
                    // If Inactive, we just skip increment the counter but NOT the checkedThisFrame counter?
                    // No, for performance, we should increment checkedThisFrame to prevent infinite loops if all are inactive.
                    if (anima != null && anima.gameObject.activeSelf)
                    {
                        bool wasDespawned = anima.CheckDespawn(_dataManager, _worldProcessor);
                        if (wasDespawned)
                        {
                            despawnedThisFrame++;
                            _totalDespawnedThisSecond++;
                        }
                    }
                }
                else
                {
                    // Index out of sync (rare case of removal during loop), reset
                    _despawnCheckIndex = 0;
                }

                _despawnCheckIndex++;
                checkedThisFrame++;

                // Break if we've looped through everyone (prevents infinite loop if animasPerCheck > total)
                if (checkedThisFrame >= totalAnimas) break;
            }

            if (debugDespawn && despawnedThisFrame > 0)
            {
                Debug.Log($"[Despawn Check] Checked: {checkedThisFrame} | Despawned: {despawnedThisFrame}");
            }
        }
    }

    void OnDestroy()
    {
        if (_cachedObstacles.IsCreated) _cachedObstacles.Dispose();
        if (_spatialGridInitialized && _spatialGrid.IsCreated) _spatialGrid.Dispose();
    }

    [BurstCompile]
    struct AnimaAIJob : IJobParallelForTransform
    {
        // ... (Job Implementation identical to your provided code) ...
        [ReadOnly] public NativeArray<bool> animaFriendly;
        [ReadOnly] public NativeArray<float3> animaPositions;
        [ReadOnly] public NativeArray<quaternion> animaRotations;
        public NativeArray<Anima.AnimaData> animaJobData;
        [ReadOnly] public NativeArray<float> speedMultipliers;

        [ReadOnly] public NativeParallelMultiHashMap<int, int> spatialGrid;
        public float spatialCellSize;
        public bool useSpatialGrid;

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

            // Flocking behavior
            float3 alignSum = 0, cohSum = 0, sepSum = 0;
            int alignCount = 0, cohCount = 0;

            if (useSpatialGrid)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        float3 offsetPos = pos + new float3(dx * spatialCellSize, 0, dz * spatialCellSize);
                        int cellKey = GetCellKey(offsetPos, spatialCellSize);

                        if (spatialGrid.TryGetFirstValue(cellKey, out int neighborIdx, out var iterator))
                        {
                            do
                            {
                                if (neighborIdx == i) continue;
                                float d = math.distance(pos, animaPositions[neighborIdx]);
                                if (d < fRadius)
                                {
                                    float3 neighborForward = math.mul(animaRotations[neighborIdx], new float3(0, 0, 1));
                                    alignSum += neighborForward;
                                    alignCount++;
                                    cohSum += animaPositions[neighborIdx];
                                    cohCount++;
                                    if (d < cRadius) sepSum += (pos - animaPositions[neighborIdx]) / math.max(d, 0.0001f);
                                }
                            }
                            while (spatialGrid.TryGetNextValue(out neighborIdx, ref iterator));
                        }
                    }
                }
            }
            else
            {
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
                        if (d < cRadius) sepSum += (pos - animaPositions[j]) / math.max(d, 0.0001f);
                    }
                }
            }

            if (align && alignCount > 0) desired += math.normalize(alignSum / alignCount) * wAlign;
            if (cohesion && cohCount > 0) desired += math.normalize((cohSum / cohCount) - pos) * wCohesion;
            if (crowd && math.length(sepSum) > 0) desired += math.normalize(sepSum) * wCrowd;
            if (noise) desired += rnd.NextFloat3Direction() * wNoise;
            if (tPlayer && !animaFriendly[i]) desired += math.normalize(playerPosition - pos) * wPlayer;

            desired = math.normalizesafe(desired);
            desired += new float3(0.01f, 0, 0.01f);
            desired = math.normalize(desired);

            float speed = obsInf > 0 ? maxSpeed : math.lerp(minSpeed, maxSpeed, math.length(desired) * 0.5f + 0.5f);

            float3 horizDir = math.normalizesafe(new float3(desired.x, 0, desired.z));
            quaternion targetRot = quaternion.LookRotation(horizDir, math.up());
            float angle = math.acos(math.clamp(math.dot(math.forward(rot), horizDir), -1f, 1f));
            float rotateT = math.min(1f, math.radians(maxRotationSpeed) * deltaTime / math.max(angle, 0.0001f));

            tx.rotation = math.slerp(rot, targetRot, rotateT);
            tx.position = pos + math.mul(tx.rotation, new float3(0, 0, 1)) * speed * deltaTime;
            animaJobData[i] = ed;
        }

        private int GetCellKey(float3 position, float cellSize)
        {
            int x = (int)math.floor(position.x / cellSize);
            int z = (int)math.floor(position.z / cellSize);
            return (x * 73856093) ^ (z * 19349663);
        }
    }
}

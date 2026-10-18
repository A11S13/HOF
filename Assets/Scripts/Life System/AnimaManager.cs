using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class AnimaManager : MonoBehaviour
{
    public static AnimaManager Instance { get; private set; }

    [Header("Compute Shader Reference")]
    public ComputeShader animaComputeShader;

    [Header("Swarm Settings")]
    public float fRadius = 40f;
    public float cRadius = 8f;
    [Range(0, 20)] public float wPlayer = 0.2f, wCrowd = 12f, wAlign = 1f, wCohesion = 1f, wNoise = 0.15f;
    public bool swarm = true, tPlayer = true, align = true, cohesion = true, crowd = true, noise = true;
    public float baseMinSpeed = 2f;
    public float baseMaxSpeed = 10f;
    public float maxRotationSpeed = 180f;

    [Header("Obstacle Settings")]
    public LayerMask obstacleLayerMask;
    public float obstacleUpdateInterval = 0.1f;

    [Header("Despawn Settings")]
    [SerializeField] private float despawnCheckInterval = 0.2f;
    [SerializeField] private int animasPerCheck = 20;
    [SerializeField] private bool debugDespawn = false;

    Transform playerTf;
    readonly List<Anima> animas = new();
    private readonly List<Anima> _activeAnimasCache = new List<Anima>(200);
    private int _lastActiveCount = -1;

    // GPU Buffers
    private ComputeBuffer _animasBuffer;
    private ComputeBuffer _obstaclesBuffer;
    private struct GpuAnimaData
    {
        public Vector3 position;
        public Vector3 forward;
        public float speedMultiplier;
        public uint isFriendly;
    }

    private float _despawnCheckTimer = 0f;
    private int _despawnCheckIndex = 0;
    private WorldDataManager _dataManager;
    private WorldProcessor _worldProcessor;

    private float _obstacleUpdateTimer = 0f;
    private int _kernelIndex;

    void Awake()
    {
        if (Instance) { Destroy(gameObject); return; }
        Instance = this;
        playerTf = GameObject.FindGameObjectWithTag("Player")?.transform;

        _dataManager = FindFirstObjectByType<WorldDataManager>();
        _worldProcessor = FindFirstObjectByType<WorldProcessor>();

        if (animaComputeShader != null)
            _kernelIndex = animaComputeShader.FindKernel("CSMain");
    }

    public void RegisterAnima(Anima a)
    {
        if (!animas.Contains(a))
        {
            animas.Add(a);
            _lastActiveCount = -1;
        }
    }

    public void UnregisterAnima(Anima a)
    {
        if (animas.Remove(a))
        {
            _lastActiveCount = -1;
        }
    }

    void Update()
    {
        ProcessAnimaAICompute();
        ProcessDespawnChecks();
    }

    private void ProcessAnimaAICompute()
    {
        if (animaComputeShader == null) return;

        _activeAnimasCache.Clear();
        for (int i = 0; i < animas.Count; i++)
        {
            if (animas[i] && animas[i].gameObject.activeSelf)
                _activeAnimasCache.Add(animas[i]);
        }

        int n = _activeAnimasCache.Count;
        if (n == 0) return;

        Anima[] requestAnimas = new Anima[n];
        for (int i = 0; i < n; i++)
        {
            requestAnimas[i] = _activeAnimasCache[i];
        }

        bool needInitialUpload = false;
        if (_animasBuffer == null || _animasBuffer.count < n || _lastActiveCount != n)
        {
            _animasBuffer?.Release();
            _animasBuffer = new ComputeBuffer(Mathf.NextPowerOfTwo(n), sizeof(float) * 7 + sizeof(uint));
            _lastActiveCount = n;
            needInitialUpload = true;
        }

        if (needInitialUpload)
        {
            GpuAnimaData[] initialDataArray = new GpuAnimaData[n];
            for (int i = 0; i < n; i++)
            {
                var anima = requestAnimas[i];
                Vector3 flatPos = anima.transform.position;
                flatPos.y = 0f; // Force initial Y to 0

                Vector3 flatFwd = anima.transform.forward;
                flatFwd.y = 0f;
                flatFwd = flatFwd.sqrMagnitude > 0.001f ? flatFwd.normalized : Vector3.forward;

                initialDataArray[i] = new GpuAnimaData
                {
                    position = flatPos,
                    forward = flatFwd,
                    speedMultiplier = anima.animaSpeedMultiplier,
                    isFriendly = anima.isFriendly ? 1u : 0u
                };
            }
            _animasBuffer.SetData(initialDataArray, 0, 0, n);
        }

        UpdateObstaclesGPU();

        // Set shader parameters
        animaComputeShader.SetBuffer(_kernelIndex, "animasBuffer", _animasBuffer);
        animaComputeShader.SetInt("animaCount", n);
        animaComputeShader.SetFloat("deltaTime", Time.deltaTime);
        animaComputeShader.SetFloat("fRadius", fRadius);
        animaComputeShader.SetFloat("cRadius", cRadius);
        animaComputeShader.SetFloat("baseMinSpeed", baseMinSpeed);
        animaComputeShader.SetFloat("baseMaxSpeed", baseMaxSpeed);
        animaComputeShader.SetFloat("maxRotationSpeed", maxRotationSpeed);

        Vector3 flatPlayerPos = playerTf ? playerTf.position : Vector3.zero;
        flatPlayerPos.y = 0f;
        animaComputeShader.SetVector("playerPosition", flatPlayerPos);

        animaComputeShader.SetFloat("wPlayer", wPlayer);
        animaComputeShader.SetFloat("wCrowd", wCrowd);
        animaComputeShader.SetFloat("wAlign", wAlign);
        animaComputeShader.SetFloat("wCohesion", wCohesion);
        animaComputeShader.SetFloat("wNoise", wNoise);

        animaComputeShader.SetInt("swarm", swarm ? 1 : 0);
        animaComputeShader.SetInt("tPlayer", tPlayer ? 1 : 0);
        animaComputeShader.SetInt("align", align ? 1 : 0);
        animaComputeShader.SetInt("cohesion", cohesion ? 1 : 0);
        animaComputeShader.SetInt("crowd", crowd ? 1 : 0);
        animaComputeShader.SetInt("noise", noise ? 1 : 0);
        animaComputeShader.SetInt("seed", Random.Range(1, int.MaxValue));

        int threadGroups = Mathf.CeilToInt(n / 64f);
        animaComputeShader.Dispatch(_kernelIndex, threadGroups, 1, 1);

        int bufferSize = n * (sizeof(float) * 7 + sizeof(uint));
        AsyncGPUReadback.Request(_animasBuffer, bufferSize, 0, request =>
        {
            if (request.hasError || this == null) return;
            NativeArray<GpuAnimaData> updatedData = request.GetData<GpuAnimaData>();

            for (int i = 0; i < requestAnimas.Length; i++)
            {
                if (requestAnimas[i] != null)
                {
                    var data = updatedData[i];
                    Transform t = requestAnimas[i].transform;

                    // Lock Y to 0 on transform update
                    t.position = new Vector3(data.position.x, 0f, data.position.z);

                    Vector3 flatFwd = new Vector3(data.forward.x, 0f, data.forward.z);
                    if (flatFwd.sqrMagnitude > 0.001f)
                    {
                        t.rotation = Quaternion.LookRotation(flatFwd.normalized, Vector3.up);
                    }
                }
            }
        });
    }

    private void UpdateObstaclesGPU()
    {
        _obstacleUpdateTimer += Time.deltaTime;
        if (_obstacleUpdateTimer >= obstacleUpdateInterval || _obstaclesBuffer == null)
        {
            _obstacleUpdateTimer = 0f;
            Collider[] tempBuffer = new Collider[100];
            Vector3 center = playerTf ? playerTf.position : Vector3.zero;
            int count = Physics.OverlapSphereNonAlloc(center, 300f, tempBuffer, obstacleLayerMask);

            Vector4[] obsData = new Vector4[Mathf.Max(1, count)];
            for (int i = 0; i < count; i++)
            {
                var b = tempBuffer[i].bounds;
                obsData[i] = new Vector4(b.center.x, 0f, b.center.z, b.extents.magnitude); // Flatten obstacle Y
            }

            _obstaclesBuffer?.Release();
            _obstaclesBuffer = new ComputeBuffer(obsData.Length, sizeof(float) * 4);
            _obstaclesBuffer.SetData(obsData);

            animaComputeShader.SetBuffer(_kernelIndex, "obstaclesBuffer", _obstaclesBuffer);
            animaComputeShader.SetInt("obstacleCount", count);
        }
    }

    private void ProcessDespawnChecks()
    {
        if (_dataManager == null || _worldProcessor == null) return;
        int totalAnimas = animas.Count;
        if (totalAnimas == 0) return;

        _despawnCheckTimer += Time.deltaTime;
        if (_despawnCheckTimer >= despawnCheckInterval)
        {
            _despawnCheckTimer = 0f;
            int checkedThisFrame = 0;

            while (checkedThisFrame < animasPerCheck)
            {
                if (_despawnCheckIndex >= totalAnimas) _despawnCheckIndex = 0;
                if (_despawnCheckIndex < animas.Count)
                {
                    var anima = animas[_despawnCheckIndex];
                    if (anima != null && anima.gameObject.activeSelf)
                    {
                        anima.CheckDespawn(_dataManager, _worldProcessor);
                    }
                }
                _despawnCheckIndex++;
                checkedThisFrame++;
                if (checkedThisFrame >= totalAnimas) break;
            }
        }
    }

    void OnDestroy()
    {
        _animasBuffer?.Release();
        _obstaclesBuffer?.Release();
    }
}
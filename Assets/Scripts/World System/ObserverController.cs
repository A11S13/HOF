using UnityEngine;

public class ObserverController : MonoBehaviour
{
    [Header("Autonomous Agent Settings")]
    public float moveSpeed = 15f;
    public float rotationSpeed = 5f;
    public float targetReachThreshold = 2f;
    public Vector3 targetPosition;
    public bool isNavigating = false;

    [Header("Visual Mesh & Morph Settings")]
    public GameObject animaPrefab;
    private GameObject _observerBody;
    private MorphController _morphController;
    private SkinnedMeshRenderer _skinnedMeshRenderer;

    [Header("Oscillator Wave Settings")]
    public bool enableOscillators = true;
    public float oscAmpL = 1.0f;
    public float oscFreqL = 0.5f; // 0.5 Hz base pulse
    public float oscAmpH = 0.5f;
    public float oscFreqH = 2.0f; // 2.0 Hz harmonic oscillation
    public float phaseShift = 0.0f;

    [Header("Bionic Chaotic Movement & Entropy Settings")]
    public string movementMode = "ORBITAL_SENTINEL"; // ORBITAL_SENTINEL, LISSAJOUS_KNOT, PERLIN_ENTROPY, DANCE_MORPH
    [Range(0f, 1f)] public float chaosWeight = 0.35f; // 0 = Pure Linear, 1 = Max Bionic Chaos
    [Range(0f, 1f)] public float entropyDepth = 0.5f; // Noise & scale depth
    public float lissajousSpeed = 1.2f;
    public float perlinNoiseScale = 25f;
    public bool morphMotionSync = true; // Sync rotation / velocity surges to morph wave peaks

    private float _currentWaveL = 0.0f;
    private float _currentWaveH = 0.0f;
    private Vector3 _anchorPosition;
    private Transform _playerTransform;

    // Organic State & Drift Engine Private Variables
    private float _phaseTimeOffset = 0f;
    private float _nextBurstTime = 0f;
    private float _nextPatternShiftTime = 0f;
    private float _currentSpeedMultiplier = 1f;

    // Organic Oscillation Frequency & Phase Drift
    private float _driftedFreqL;
    private float _driftedFreqH;
    private float _dynamicPhaseL;
    private float _dynamicPhaseH;

    // Soft Tether / Detached Lag Tracking Vector
    private Vector3 _looseTargetVector;

    private readonly string[] _availablePatterns = new string[] {
        "ORBITAL_SENTINEL", "LISSAJOUS_KNOT", "PERLIN_ENTROPY", "DANCE_MORPH"
    };

    private void Awake()
    {
        targetPosition = transform.position;
        _anchorPosition = transform.position;
        _looseTargetVector = transform.position;
        EnsureObserverVisualBody();
    }

    private void Start()
    {
        EnsureObserverVisualBody();
    }

    private void EnsureObserverVisualBody()
    {
        _morphController = GetComponentInChildren<MorphController>();
        _skinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
    }

    private void Update()
    {
        // 1. Local 60 FPS Dual Wave Oscillator Modulation with Programmatic Frequency Drift
        if (enableOscillators)
        {
            UpdateOscillators();
        }

        // 2. Local 60 FPS Bionic Movement & Autonomous Pattern Shifting Engine
        UpdateBionicMovement();
    }

    private void UpdateOscillators()
    {
        if (_skinnedMeshRenderer == null)
        {
            _skinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
            if (_skinnedMeshRenderer == null) return;
        }

        if (_skinnedMeshRenderer.sharedMesh == null) return;

        float time = Time.time;

        // Programmatic Frequency & Phase Drift synchronized with speed bursts and entropy
        float freqModulationL = Mathf.Sin(time * 0.4f) * 0.25f * entropyDepth;
        float freqModulationH = Mathf.Cos(time * 0.7f) * 0.5f * entropyDepth;
        
        _driftedFreqL = Mathf.Clamp(oscFreqL + freqModulationL + (_currentSpeedMultiplier - 1f) * 0.3f, 0.1f, 8.0f);
        _driftedFreqH = Mathf.Clamp(oscFreqH + freqModulationH + (_currentSpeedMultiplier - 1f) * 0.5f, 0.1f, 15.0f);

        _dynamicPhaseL += Time.deltaTime * Mathf.PI * 2f * _driftedFreqL;
        _dynamicPhaseH += Time.deltaTime * Mathf.PI * 2f * _driftedFreqH;

        // Calculate organic wave amplitudes (0.0 to 1.0)
        _currentWaveL = (Mathf.Sin(_dynamicPhaseL) + 1f) * 0.5f * oscAmpL;
        _currentWaveH = (Mathf.Sin(_dynamicPhaseH + phaseShift) + 1f) * 0.5f * oscAmpH;

        int lIndex = _skinnedMeshRenderer.sharedMesh.GetBlendShapeIndex("Observer_L");
        int hIndex = _skinnedMeshRenderer.sharedMesh.GetBlendShapeIndex("Observer_H");

        if (lIndex >= 0)
            _skinnedMeshRenderer.SetBlendShapeWeight(lIndex, Mathf.Clamp01(_currentWaveL) * 100f);
        else if (_skinnedMeshRenderer.sharedMesh.blendShapeCount > 0)
            _skinnedMeshRenderer.SetBlendShapeWeight(0, Mathf.Clamp01(_currentWaveL) * 100f);

        if (hIndex >= 0)
            _skinnedMeshRenderer.SetBlendShapeWeight(hIndex, Mathf.Clamp01(_currentWaveH) * 100f);
        else if (_skinnedMeshRenderer.sharedMesh.blendShapeCount > 1)
            _skinnedMeshRenderer.SetBlendShapeWeight(1, Mathf.Clamp01(_currentWaveH) * 100f);
    }

    private void UpdateBionicMovement()
    {
        if (_playerTransform == null)
        {
            _playerTransform = FindFirstObjectByType<Player>()?.transform;
        }

        float time = Time.time;
        Vector3 rawPlayerPos = _playerTransform != null ? _playerTransform.position : (isNavigating ? targetPosition : _anchorPosition);

        // 1. Soft-Tether / Detached Lag Dynamics: Smoothly drift loose target instead of rigid snap
        float tetherLagSpeed = Mathf.Lerp(0.4f, 3.5f, 1f - chaosWeight); // Lower chaos = tighter tether, higher chaos = loose roaming
        _looseTargetVector = Vector3.Lerp(_looseTargetVector, rawPlayerPos, tetherLagSpeed * Time.deltaTime);

        // 2. Autonomous Dynamic Movement Pattern Shifting
        if (time > _nextPatternShiftTime && chaosWeight > 0.15f)
        {
            _nextPatternShiftTime = time + Random.Range(6.0f, 14.0f);
            int randIdx = Random.Range(0, _availablePatterns.Length);
            movementMode = _availablePatterns[randIdx];
            Debug.Log($"[OBSERVER BIONIC] Autonomous Choreography Shift: Switched pattern mode to -> {movementMode}");
        }

        // 3. Multi-Scale Breathing Distance Cycle (10m close flyby <-> 450m distant roaming)
        float macroScaleCycle = (Mathf.Cos(time * 0.18f) + 1f) * 0.5f;
        float currentDistanceScale = Mathf.Lerp(20f, 420f, macroScaleCycle * entropyDepth);

        // 4. Velocity Shifts & Burst Maneuvers
        if (time > _nextBurstTime)
        {
            _nextBurstTime = time + Random.Range(2.0f, 6.0f);
            _currentSpeedMultiplier = Random.Range(0.4f, 4.0f);
            _phaseTimeOffset += Random.Range(-3.0f, 3.0f);
        }

        float adjustedTime = (time + _phaseTimeOffset) * (0.8f + chaosWeight * 1.5f);

        // 5. Calculate Pattern Vectors Anchored to Loose Target
        Vector3 patternOffset = Vector3.zero;

        if (movementMode == "ORBITAL_SENTINEL")
        {
            float dynamicRadius = currentDistanceScale * (0.5f + Mathf.Sin(adjustedTime * 1.2f) * 0.5f);
            float angle = adjustedTime * (0.9f + _currentWaveL * 1.8f);
            patternOffset = new Vector3(Mathf.Cos(angle) * dynamicRadius, Mathf.Sin(adjustedTime * 0.7f) * dynamicRadius * 0.4f, Mathf.Sin(angle) * dynamicRadius);
        }
        else if (movementMode == "LISSAJOUS_KNOT")
        {
            float radius = currentDistanceScale * 1.3f;
            float a = 3f;
            float b = 2f;
            float t = adjustedTime * lissajousSpeed;
            float x = radius * Mathf.Sin(a * t + Mathf.PI * 0.5f);
            float y = (radius * 0.5f) * Mathf.Cos(t * 1.8f);
            float z = radius * Mathf.Sin(b * t);
            patternOffset = new Vector3(x, y, z);
        }
        else if (movementMode == "PERLIN_ENTROPY")
        {
            float noiseX = (Mathf.PerlinNoise(adjustedTime * 0.6f, 0.0f) - 0.5f) * currentDistanceScale * 2.8f;
            float noiseY = (Mathf.PerlinNoise(200.0f, adjustedTime * 0.6f) - 0.5f) * currentDistanceScale * 0.9f;
            float noiseZ = (Mathf.PerlinNoise(0.0f, adjustedTime * 0.6f) - 0.5f) * currentDistanceScale * 2.8f;
            patternOffset = new Vector3(noiseX, noiseY, noiseZ);
        }
        else if (movementMode == "DANCE_MORPH")
        {
            float morphSurge = (_currentWaveL * 2.0f + _currentWaveH * 3.0f);
            float danceRadius = Mathf.Lerp(12f, currentDistanceScale * 1.6f, morphSurge / 5.0f);
            float danceAngle = adjustedTime * (2.2f + morphSurge);
            float danceY = Mathf.Sin(adjustedTime * 2.5f) * danceRadius * 0.6f;
            patternOffset = new Vector3(Mathf.Cos(danceAngle) * danceRadius, danceY, Mathf.Sin(danceAngle) * danceRadius);
        }

        // 6. Blend Target Position with Loose Target & Chaos Pattern
        Vector3 desiredTarget = Vector3.Lerp(_looseTargetVector, _looseTargetVector + patternOffset, chaosWeight);

        Vector3 moveDir = (desiredTarget - transform.position);
        moveDir.y = 0f;

        float distance = moveDir.magnitude;
        if (distance > 0.2f)
        {
            // 7. Dynamic Velocity & Acrobatic Rotation Choreography
            float currentMoveSpeed = moveSpeed * _currentSpeedMultiplier;
            float currentRotSpeed = rotationSpeed * (1f + _currentSpeedMultiplier * 0.6f);

            if (morphMotionSync)
            {
                float wavePeak = Mathf.Max(_currentWaveL, _currentWaveH);
                currentMoveSpeed *= (1f + wavePeak * 3.0f);
                currentRotSpeed *= (1f + wavePeak * 3.5f);
            }

            Quaternion targetRot = Quaternion.LookRotation(moveDir.normalized);
            
            if (chaosWeight > 0.05f)
            {
                float rotWobbleY = Mathf.Sin(time * 5f) * 22f * chaosWeight * entropyDepth;
                float rotRollZ = Mathf.Cos(time * 3.5f) * 30f * chaosWeight * _currentSpeedMultiplier;
                targetRot *= Quaternion.Euler(0f, rotWobbleY, rotRollZ);
            }

            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, currentRotSpeed * Time.deltaTime);

            float actualStep = Mathf.Min(distance, currentMoveSpeed * Time.deltaTime);
            transform.position += transform.forward * actualStep;
        }
    }

    public void ConfigureBionicMovement(string mode, float chaos, float entropy, float lissSpeed, float pScale, bool sync)
    {
        if (!string.IsNullOrEmpty(mode)) movementMode = mode;
        chaosWeight = Mathf.Clamp01(chaos);
        entropyDepth = Mathf.Clamp01(entropy);
        if (lissSpeed > 0f) lissajousSpeed = lissSpeed;
        if (pScale > 0f) perlinNoiseScale = pScale;
        morphMotionSync = sync;
    }

    public void ConfigureOscillators(float ampL, float ampH, float freqL, float freqH, bool enableOsc, float phase = 0f)
    {
        oscAmpL = Mathf.Clamp01(ampL);
        oscAmpH = Mathf.Clamp01(ampH);
        
        if (freqL > 0f) oscFreqL = freqL;
        if (freqH > 0f) oscFreqH = freqH;
        
        enableOscillators = enableOsc;
        phaseShift = phase;

        if (!enableOscillators)
        {
            SetDualMorph(ampL, ampH);
        }
    }

    public void NavigateTo(Vector3 worldPos)
    {
        targetPosition = worldPos;
        targetPosition.y = 0f;
        _anchorPosition = targetPosition;
        _looseTargetVector = targetPosition;
        isNavigating = true;
    }

    public void SetMorphWeight(float weight)
    {
        weight = Mathf.Clamp01(weight);
        oscAmpL = weight;

        if (!enableOscillators)
        {
            if (_morphController != null)
                _morphController.morph = weight;
            else if (_skinnedMeshRenderer != null && _skinnedMeshRenderer.sharedMesh.blendShapeCount > 0)
                _skinnedMeshRenderer.SetBlendShapeWeight(0, weight * 100f);
        }
    }

    public void SetDualMorph(float morphL, float morphH)
    {
        oscAmpL = Mathf.Clamp01(morphL);
        oscAmpH = Mathf.Clamp01(morphH);

        if (_skinnedMeshRenderer == null)
        {
            _skinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        }

        if (_skinnedMeshRenderer != null && _skinnedMeshRenderer.sharedMesh != null)
        {
            int lIndex = _skinnedMeshRenderer.sharedMesh.GetBlendShapeIndex("Observer_L");
            int hIndex = _skinnedMeshRenderer.sharedMesh.GetBlendShapeIndex("Observer_H");

            if (lIndex >= 0)
                _skinnedMeshRenderer.SetBlendShapeWeight(lIndex, oscAmpL * 100f);
            else if (_skinnedMeshRenderer.sharedMesh.blendShapeCount > 0)
                _skinnedMeshRenderer.SetBlendShapeWeight(0, oscAmpL * 100f);

            if (hIndex >= 0)
                _skinnedMeshRenderer.SetBlendShapeWeight(hIndex, oscAmpH * 100f);
            else if (_skinnedMeshRenderer.sharedMesh.blendShapeCount > 1)
                _skinnedMeshRenderer.SetBlendShapeWeight(1, oscAmpH * 100f);
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum WeaponFireType
{
    Discrete,   // Projectiles, Cannons
    Continuous  // Beams, Lasers
}

public abstract class Weapon : MonoBehaviour
{
    // --- DEPENDENCIES ---
    protected ShipCore shipCore;
    protected IShipController shipController;
    protected ITargetProvider targetProvider;
    protected Rigidbody shipRb;
    public ShipCore ShipCore => shipCore;
    public int ID;

    protected IWeaponControlMode controlMode;
    public bool isAI { get; private set; }

    // --- CONFIGURATION ---
    [Header("Weapon Configuration")]
    public WeaponFireType fireType = WeaponFireType.Discrete;

    [Header("Rotation Settings")]
    public bool lockRotation = false;
    [Range(0f, 360f)]
    public float fireArc = 360f;
    public float localRotationDeadZone = 0.1f;
    public float rotationSpeed = 5f;

    // --- LONG RANGE SENSOR (Fixed Weapons) ---
    [Header("Long Range Sensor")]
    public bool useSectorScan = false;
    public float sectorScanRange = 800f;
    [Tooltip("Minimum width of the scan fan.")]
    public float minScanArc = 6f;
    public float scanResolution = 2f;
    [Tooltip("Radius of the scan rays.")]
    public float scanThickness = 2f;
    public LayerMask scanLayers;

    [Header("Debug")]
    public bool drawDebugRays = false;

    // PUBLIC: So AIWeaponControl can see it
    public List<GameObject> sectorScanResults = new List<GameObject>();
    private float lastSectorScanTime;
    private Collider[] proximityBuffer = new Collider[10];

    // --- STATE ---
    public float fireAccumulator { get; set; }
    public ShipTargetingSystem.TargetData[] CombinedTargets { get; private set; }
    public GameObject prevTarget;
    protected Transform weaponSpawnPoint;

    [Header("Fire Rate")]
    public float initialFireRate;
    public float fireRate;
    public bool isFiring;
    public float firingRate;

    public float angularDistance;
    public int incrementSkipFrames;

    protected AudioSource fireAudio;
    protected WeaponAudioDSP audioDSP;

    private static Dictionary<GameObject, int> targetWeaponCount = new Dictionary<GameObject, int>();
    private static System.Object lockObject = new System.Object();
    private bool isInitialized = false;

    void Awake()
    {
        fireAudio = GetComponent<AudioSource>();
        audioDSP = GetComponent<WeaponAudioDSP>();
    }

    public void Initialize(ShipCore core, IShipController controller, ITargetProvider targets, bool useAI)
    {
        if (isInitialized) return;
        shipCore = core;
        shipController = controller;
        targetProvider = targets;
        shipRb = controller.ShipRigidbody;
        this.isAI = useAI;
        SetupControlMode();

        firingRate = 1;
        if (initialFireRate <= 0f) initialFireRate = fireRate;
        fireAccumulator = Random.value;
        incrementSkipFrames = 3;

        FindWeaponSpawnPoint();
        InitializeWeapon();

        isInitialized = true;
    }

    // --- RESTORED ARCHITECTURE METHODS ---

    public void ToggleAI()
    {
        isAI = !isAI;
        SetupControlMode();
    }

    protected virtual void InitializeWeapon() { }
    protected abstract void Fire(float aimQuality);
    protected virtual void StopFiring() { }

    protected virtual void FindWeaponSpawnPoint()
    {
        weaponSpawnPoint = transform.Find("projectileSpawnPoint") ?? transform.Find("weaponSpawnPoint") ?? transform.Find("beamSpawnPoint");
        if (weaponSpawnPoint == null) weaponSpawnPoint = transform;
    }

    void SetupControlMode()
    {
        if (isAI)
        {
            controlMode = new AIWeaponControl(Random.Range(0.0f, 0.75f));
        }
        else
        {
            var joystickObj = GameObject.FindGameObjectWithTag("TurretJoystick");
            if (joystickObj != null)
            {
                var joystick = joystickObj.GetComponent<FixedJoystick>();
                controlMode = new ManualWeaponControl(joystick, localRotationDeadZone);
            }
            else
            {
                isAI = true;
                controlMode = new AIWeaponControl(0.5f);
            }
        }
    }

    void OnDestroy()
    {
        var aiControl = controlMode as AIWeaponControl;
        if (aiControl != null && aiControl.GetCurrentTarget() != null)
        {
            RemoveTargetAssignment(aiControl.GetCurrentTarget());
        }
    }

    void Update()
    {
        if (!isInitialized) return;

        // 1. SENSOR LOGIC (Bypass Sonar)
        if (useSectorScan)
        {
            if (lockRotation) PerformSectorScan();
            else PerformSectorScanThrottled();

            if (drawDebugRays && sectorScanResults.Count > 0)
            {
                foreach (var target in sectorScanResults)
                    if (target != null) Debug.DrawLine(transform.position, target.transform.position, Color.cyan);
            }
        }

        // 2. CONTROL LOGIC
        float aimFactor = controlMode.UpdateControl(this);

        // Debug Firing Line
        if (drawDebugRays && weaponSpawnPoint != null)
        {
            Debug.DrawRay(weaponSpawnPoint.position, weaponSpawnPoint.forward * (aimFactor > 0 ? 50 : 10), aimFactor > 0 ? Color.green : Color.red);
        }

        // 3. FIRE EXECUTION
        TryFire(aimFactor);
        audioDSP?.Drive();
    }

    // --- SCANNING LOGIC ---

    private void PerformSectorScanThrottled()
    {
        if (Time.time < lastSectorScanTime + 0.1f) return;
        PerformSectorScan();
    }

    private void PerformSectorScan()
    {
        lastSectorScanTime = Time.time;
        sectorScanResults.Clear();

        float effectiveArc = Mathf.Max(fireArc, minScanArc);
        int rayCount = Mathf.Max(3, Mathf.CeilToInt(effectiveArc / scanResolution));
        if (rayCount > 60) rayCount = 60;

        float angleStep = effectiveArc / (rayCount - 1);
        float startAngle = -effectiveArc * 0.5f;

        Vector3 origin = weaponSpawnPoint != null ? weaponSpawnPoint.position : transform.position;
        Vector3 castOrigin = origin + (transform.forward * 0.5f);
        Transform myRoot = transform.root;

        // A. Proximity (Touch)
        int proxCount = Physics.OverlapSphereNonAlloc(origin, scanThickness, proximityBuffer, scanLayers);
        for (int k = 0; k < proxCount; k++)
        {
            Collider col = proximityBuffer[k];
            if (col.transform.root == myRoot) continue;
            AddValidTarget(col);
        }

        // B. Sector Fan (Long Range)
        for (int i = 0; i < rayCount; i++)
        {
            float currentAngle = startAngle + (angleStep * i);
            Quaternion rot = Quaternion.Euler(0, currentAngle, 0);
            Vector3 rayDir = (weaponSpawnPoint != null ? weaponSpawnPoint.rotation : transform.rotation) * rot * Vector3.forward;

            RaycastHit[] hits = Physics.SphereCastAll(castOrigin, scanThickness, rayDir, sectorScanRange, scanLayers, QueryTriggerInteraction.Ignore);

            // Sort by distance to find closest VALID target
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                if (hit.transform.root == myRoot) continue;
                bool isValid = AddValidTarget(hit.collider);
                if (isValid || hit.collider != null) break;
            }
        }
    }

    private bool AddValidTarget(Collider col)
    {
        GameObject foundTarget = null;
        var targetable = col.GetComponentInParent<Targetable>();
        if (targetable != null) foundTarget = targetable.gameObject;
        else if (col.GetComponentInParent<Asteroid>() != null) foundTarget = col.GetComponentInParent<Asteroid>().gameObject;
        else
        {
            var health = col.GetComponentInParent<Health>();
            if (health != null) foundTarget = health.gameObject;
            else
            {
                var dmg = col.GetComponentInParent<Damageable>();
                if (dmg != null) foundTarget = dmg.gameObject;
            }
        }

        if (foundTarget != null)
        {
            if (!sectorScanResults.Contains(foundTarget))
                sectorScanResults.Add(foundTarget);
            return true;
        }
        return false;
    }

    // --- TARGETING HELPERS ---

    public void PrioritizeTargets()
    {
        // Standard Sonar Logic
        ShipTargetingSystem.TargetData[] closestTargets = FindClosest();
        Dictionary<GameObject, ShipTargetingSystem.TargetData> combinedDict = new Dictionary<GameObject, ShipTargetingSystem.TargetData>();

        foreach (var t in closestTargets) if (t.TargetObject) combinedDict[t.TargetObject] = t;

        // Add Scan Results
        if (useSectorScan)
        {
            foreach (var longRangeObj in sectorScanResults)
            {
                if (longRangeObj == null) continue;
                float d = (longRangeObj.transform.position - transform.position).sqrMagnitude;
                if (!combinedDict.ContainsKey(longRangeObj))
                    combinedDict[longRangeObj] = new ShipTargetingSystem.TargetData(longRangeObj, d);
            }
        }

        CombinedTargets = combinedDict.Values.ToArray();
        System.Array.Sort(CombinedTargets, (a, b) => a.UnifiedDistance.CompareTo(b.UnifiedDistance));
    }

    public GameObject FindBestAvailableTarget()
    {
        GameObject best = null; float bestScore = float.MaxValue;
        foreach (var t in CombinedTargets)
        {
            if (!t.TargetObject) continue;
            float s = (t.TargetObject.transform.position - transform.position).sqrMagnitude;
            if (s < bestScore) { bestScore = s; best = t.TargetObject; }
        }
        return best;
    }

    public bool ShouldSwitch(GameObject cur, GameObject best) => true;
    public void AddTargetAssignment(GameObject t) { lock (lockObject) { if (t) { if (targetWeaponCount.ContainsKey(t)) targetWeaponCount[t]++; else targetWeaponCount[t] = 1; } } }
    public void RemoveTargetAssignment(GameObject t) { lock (lockObject) { if (t && targetWeaponCount.ContainsKey(t)) { targetWeaponCount[t]--; if (targetWeaponCount[t] <= 0) targetWeaponCount.Remove(t); } } }

    // --- BASIC UTILS ---
    public bool IsAimedAtTarget(GameObject t) { return t != null && (useSectorScan ? sectorScanResults.Contains(t) : IsAimedAtTarget(t.transform.position)); }
    public bool IsAimedAtTarget(Vector3 pos)
    {
        Vector3 dir = pos - transform.position; dir.y = 0;
        return Vector3.Angle(transform.forward, dir) <= shipCore.firingThresholdAngle;
    }
    public void RotateTowardsTarget(Vector3 pos) { if (lockRotation) transform.localRotation = Quaternion.identity; }
    public virtual Vector3 CalculateAimPosition(GameObject t, Vector3 v) => t ? t.transform.position : Vector3.zero;

    // --- FIRE ---
    void TryFire(float aimFactor) { if (aimFactor <= 0) { isFiring = false; if (fireType == WeaponFireType.Continuous) StopFiring(); } else { if (fireType == WeaponFireType.Discrete) HandleDiscrete(aimFactor); else HandleContinuous(aimFactor); } }
    void HandleDiscrete(float aim) { fireAccumulator += Time.deltaTime * fireRate; if (fireAccumulator >= 1f && shipCore.RequestFirePermission()) { isFiring = true; fireAccumulator -= 1f; Fire(aim); } }
    void HandleContinuous(float aim) { if (isFiring || shipCore.RequestFirePermission()) { isFiring = true; Fire(aim); } else { isFiring = false; StopFiring(); } }

    // --- ABSTRACT IMPLEMENTATION ---
    ShipTargetingSystem.TargetData[] FindClosest()
    {
        var src = targetProvider.GetTargetDataArray(); var l = new List<ShipTargetingSystem.TargetData>();
        foreach (var t in src) if (t.TargetObject) l.Add(new ShipTargetingSystem.TargetData(t.TargetObject, (t.TargetObject.transform.position - transform.position).sqrMagnitude));
        return l.ToArray();
    }
    ShipTargetingSystem.TargetData[] FindAngularClosest() => FindClosest();
    public void UpdateFirePower(float e) { fireRate = initialFireRate; }
}
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum WeaponFireType
{
    Discrete,   // Projectiles, Cannons (Fire -> Wait -> Fire)
    Continuous  // Beams, Lasers (Fire every frame while held)
}

/// <summary>
/// Base weapon class - fully decoupled and reusable
/// </summary>
public abstract class Weapon : MonoBehaviour
{

    [Header("Long Range Sensor (Fixed Weapons)")]
    public bool useSectorScan = false;
    public float sectorScanRange = 800f;
    [Tooltip("Degrees between each ray. Smaller = More Rays.")]
    public float scanResolution = 2f;
    public LayerMask scanLayers;

    // Cache for our manual scan results
    private List<GameObject> sectorScanResults = new List<GameObject>();
    private float lastSectorScanTime;

    // Dependencies (injected)
    protected ShipCore shipCore;
    protected IShipController shipController;
    protected ITargetProvider targetProvider;
    protected Rigidbody shipRb;
    public ShipCore ShipCore => shipCore;

    public int ID;

    // Control mode
    protected IWeaponControlMode controlMode;
    public bool isAI { get; private set; }

    // Configuration
    [Header("Weapon Configuration")]
    public WeaponFireType fireType = WeaponFireType.Discrete;

    [Header("Rotation Settings")]
    public bool lockRotation = false; // If true, weapon never rotates (fixed mount)
    [Range(0f, 360f)]
    public float fireArc = 360f;      // Firing arc in degrees centered on forward
    public float localRotationDeadZone = 0.1f;
    public float rotationSpeed = 5f;

    // Targeting state
    public float fireAccumulator { get; set; }

    public ShipTargetingSystem.TargetData[] CombinedTargets { get; private set; }
    public GameObject prevTarget;

    protected Transform weaponSpawnPoint;

    [Header("Fire Rate / Damage")]
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
        if (isInitialized)
        {
            Debug.LogWarning($"Weapon {gameObject.name} already initialized!");
            return;
        }

        if (core == null || controller == null || targets == null)
        {
            Debug.LogError($"Weapon {gameObject.name}: Null dependency!");
            return;
        }

        shipCore = core;
        shipController = controller;
        targetProvider = targets;
        shipRb = controller.ShipRigidbody;

        // Setup control mode
        isAI = useAI;
        SetupControlMode();

        // Setup weapon
        firingRate = 1;
        if (initialFireRate <= 0f)
            initialFireRate = fireRate;

        fireAccumulator = Random.value;
        incrementSkipFrames = 3;
        FindWeaponSpawnPoint();

        InitializeWeapon();

        isInitialized = true;
    }

    void SetupControlMode()
    {
        if (isAI)
        {
            float reactionDelay = Random.Range(0.0f, 0.75f);
            controlMode = new AIWeaponControl(reactionDelay);
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
                controlMode = new AIWeaponControl(Random.Range(0.0f, 0.75f));
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

        float aimFactor = controlMode.UpdateControl(this);
        TryFire(aimFactor);

        audioDSP?.Drive();
    }

    /// <summary>
    /// Unified firing logic handling both Discrete (Projectile) and Continuous (Beam) types
    /// </summary>
    void TryFire(float aimFactor)
    {
        // 1. Check aim quality/trigger release
        if (aimFactor <= 0f)
        {
            isFiring = false;
            if (fireType == WeaponFireType.Discrete)
            {
                // Cap accumulator so we don't overcharge
                fireAccumulator = Mathf.Min(fireAccumulator, 1.0f);
            }
            else
            {
                // Continuous weapons stop immediately
                StopFiring();
            }
            return;
        }

        // 2. Logic Split based on Type
        if (fireType == WeaponFireType.Discrete)
        {
            HandleDiscreteFire(aimFactor);
        }
        else // Continuous
        {
            HandleContinuousFire(aimFactor);
        }
    }

    void HandleDiscreteFire(float aimFactor)
    {
        fireAccumulator += Time.deltaTime * fireRate;
        firingRate = fireRate;

        if (fireAccumulator >= 1f)
        {
            if (shipCore.RequestFirePermission())
            {
                isFiring = true;
                fireAccumulator -= 1f; // Consume charge
                audioDSP?.NotifyShot();
                Fire(aimFactor);
            }
            else
            {
                fireAccumulator = Mathf.Min(fireAccumulator, 1.5f);
                isFiring = false;
            }
        }
        else
        {
            isFiring = fireAccumulator >= 0.95f;
        }
    }

    // Inside Weapon.cs

    void HandleContinuousFire(float aimFactor)
    {
        // DEBUG: Uncomment to see if Core is denying you
        // Debug.Log($"Weapon {gameObject.name} Continuous Check. Core Perm: {shipCore.RequestFirePermission()}");

        // FIX: Beams shouldn't ask for "New Shot" permission every frame if they are already firing.
        // They only need permission to START.

        bool hasPermission = false;

        if (isFiring)
        {
            // If we are already firing, we assume we keep the "channel" open
            // (Unless you have an Energy system, checking CanSustainFire() would go here)
            hasPermission = true;
        }
        else
        {
            // We are trying to START firing, so we ask the Core
            hasPermission = shipCore.RequestFirePermission();
        }

        if (hasPermission)
        {
            isFiring = true;
            audioDSP?.NotifyShot(); // Note: You might want to change this to NotifyLoop() for beams later
            Fire(aimFactor);
        }
        else
        {
            // Only stop if we were actually firing and lost permission/trigger
            if (isFiring)
            {
                Debug.Log($"Weapon {gameObject.name}: Stopped. Permission Denied or Interrupt.");
            }
            isFiring = false;
            StopFiring();
        }
    }



    public void ToggleAI()
    {
        isAI = !isAI;
        SetupControlMode();
    }

    // === PUBLIC HELPER METHODS ===

    public void RotateTowardsTarget(Vector3 targetPosition)
    {
        // 1. Fixed Weapon Check
        if (lockRotation)
        {
            // Ensure we are reset to local forward if locked
            transform.localRotation = Quaternion.RotateTowards(
                transform.localRotation,
                Quaternion.identity,
                Time.deltaTime * rotationSpeed
            );
            return;
        }

        Vector3 targetDir = targetPosition - transform.position;
        targetDir.y = 0f;

        if (targetDir == Vector3.zero) return;

        // 2. Calculate Angle for Arc Clamping
        // Convert world target direction to local space to check against arc
        Vector3 localDir = transform.parent.InverseTransformDirection(targetDir);
        float angle = Vector3.SignedAngle(Vector3.forward, localDir, Vector3.up);

        // 3. Clamp Angle within Fire Arc
        float halfArc = fireArc * 0.5f;

        // If target is outside arc, we clamp the aim vector to the edge of the arc
        if (Mathf.Abs(angle) > halfArc)
        {
            float clampedAngle = Mathf.Sign(angle) * halfArc;
            // Reconstruct local direction from clamped angle
            Quaternion clampRot = Quaternion.Euler(0, clampedAngle, 0);
            localDir = clampRot * Vector3.forward;
            // Convert back to world for the final rotation logic
            targetDir = transform.parent.TransformDirection(localDir);
        }

        // 4. Perform Rotation
        Quaternion targetRotation = Quaternion.LookRotation(targetDir);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            Time.deltaTime * rotationSpeed
        );

        // 5. Update Aim Stats
        if (Time.frameCount % incrementSkipFrames == 0)
        {
            angularDistance = Quaternion.Angle(transform.rotation, targetRotation);
            if (angularDistance < 0.01f) angularDistance = 0.01f;
        }
    }

    public bool IsAimedAtTarget(Vector3 targetPosition)
    {
        Vector3 directionToTarget = targetPosition - transform.position;
        directionToTarget.y = 0f;

        // FIX: For checking arcs, we must use the Ship's forward, not the slot's parent
        // If weapon is Locked (Fixed), it is relative to Ship. 
        // If Turret, it is relative to Mount.
        Transform referenceTransform = lockRotation ? shipCore.transform : transform.parent;

        // Safety check for hierarchy
        if (referenceTransform == null) referenceTransform = transform.parent;

        if (fireArc < 360f)
        {
            Vector3 localDir = referenceTransform.InverseTransformDirection(directionToTarget);
            float angle = Vector3.Angle(Vector3.forward, localDir);
            if (angle > fireArc * 0.5f) return false;
        }

        // For fixed weapons, "IsAimed" means "Is the ship pointing at it?"
        // We use the weapon's own forward vector for the final precision check
        Vector3 weaponForward = transform.forward;
        float angleToTarget = Vector3.Angle(weaponForward, directionToTarget);

        return angleToTarget <= shipCore.firingThresholdAngle;
    }

    public virtual Vector3 CalculateAimPosition(GameObject target, Vector3 targetVelocity)
    {
        return target != null ? target.transform.position : transform.position + transform.forward * 100f;
    }

    public void PrioritizeTargets()
    {
        // 1. Get Standard Sonar Targets (Short/Medium Range)
        ShipTargetingSystem.TargetData[] closestTargets = FindClosest();
        ShipTargetingSystem.TargetData[] angularClosestTargets = FindAngularClosest();

        Dictionary<GameObject, ShipTargetingSystem.TargetData> combinedDict = new Dictionary<GameObject, ShipTargetingSystem.TargetData>();

        // 2. Add Standard Targets to Dictionary
        // (If Fixed Weapon, we filter out "Behind" targets in step 4 to avoid getting stuck)
        foreach (var t in closestTargets) if (t.TargetObject) combinedDict[t.TargetObject] = t;
        foreach (var t in angularClosestTargets)
        {
            if (t.TargetObject)
            {
                if (!combinedDict.ContainsKey(t.TargetObject) || t.UnifiedDistance < combinedDict[t.TargetObject].UnifiedDistance)
                    combinedDict[t.TargetObject] = t;
            }
        }

        // 3. NEW: Perform Long-Range Sector Scan (Raycast Fan)
        if (useSectorScan)
        {
            PerformSectorScan();
            foreach (var longRangeObj in sectorScanResults)
            {
                if (longRangeObj == null) continue;

                // Calculate distance manually since it might be outside sonar range
                float d = (longRangeObj.transform.position - transform.position).sqrMagnitude;

                // Add to our candidates list (or update if found by sonar too)
                var targetData = new ShipTargetingSystem.TargetData(longRangeObj, d);

                if (!combinedDict.ContainsKey(longRangeObj))
                {
                    combinedDict[longRangeObj] = targetData;
                }
            }
        }

        // 4. Filter & Sort
        var candidates = combinedDict.Values.ToArray();

        if (lockRotation)
        {
            // STRICT FILTER for Fixed Weapons:
            // Remove targets that are physically impossible to hit (outside Fire Arc)
            // unless we are using SectorScan (which guarantees they are in arc).
            var validFixedTargets = new List<ShipTargetingSystem.TargetData>();

            foreach (var t in candidates)
            {
                if (t.TargetObject == null) continue;

                // Check if aiming is possible
                if (IsAimedAtTarget(t.TargetObject.transform.position))
                {
                    validFixedTargets.Add(t);
                }
                // If it was found by our Sector Scan, it's definitely in front, so keep it!
                else if (sectorScanResults.Contains(t.TargetObject))
                {
                    validFixedTargets.Add(t);
                }
            }
            CombinedTargets = validFixedTargets.ToArray();
        }
        else
        {
            CombinedTargets = candidates;
        }

        // Final Sort by Distance
        System.Array.Sort(CombinedTargets, (a, b) => a.UnifiedDistance.CompareTo(b.UnifiedDistance));
    }

    // --- ADD THIS NEW HELPER METHOD ---
    private void PerformSectorScan()
    {
        // Throttling: Scan 10 times per second max
        if (Time.time < lastSectorScanTime + 0.1f) return;
        lastSectorScanTime = Time.time;

        sectorScanResults.Clear();

        // 1. Calculate how many rays we need
        // If arc is 10 degrees and res is 2, we need 5 rays.
        // We ensure at least 1 ray (center)
        float activeArc = fireArc > 0 ? fireArc : 1f;
        int rayCount = Mathf.Max(1, Mathf.CeilToInt(activeArc / scanResolution));

        // Clamp to prevent performance explosion on 360 turrets accidentally left on
        if (rayCount > 20) rayCount = 20;

        // 2. Calculate Angle Step
        float angleStep = activeArc / rayCount;
        float startAngle = -activeArc * 0.5f + (angleStep * 0.5f); // Center the rays

        // 3. Scan Loop
        Vector3 origin = weaponSpawnPoint != null ? weaponSpawnPoint.position : transform.position;
        Vector3 forward = weaponSpawnPoint != null ? weaponSpawnPoint.forward : transform.forward;

        // Use root to ignore self
        Transform root = transform.root;

        for (int i = 0; i < rayCount; i++)
        {
            float currentAngle = startAngle + (angleStep * i);
            Quaternion rot = Quaternion.Euler(0, currentAngle, 0);

            // Convert local rotation to world direction
            Vector3 rayDir = (weaponSpawnPoint != null ? weaponSpawnPoint.rotation : transform.rotation) * rot * Vector3.forward;

            RaycastHit hit;
            if (Physics.Raycast(origin, rayDir, out hit, sectorScanRange, scanLayers))
            {
                if (hit.transform.root != root) // Don't hit self
                {
                    // Check if it's a valid target (has Health/Damageable)
                    if (hit.collider.GetComponentInParent<Damageable>() != null ||
                        hit.collider.GetComponentInParent<Health>() != null)
                    {
                        GameObject targetRoot = hit.collider.transform.root.gameObject;
                        if (!sectorScanResults.Contains(targetRoot))
                        {
                            sectorScanResults.Add(targetRoot);
                        }
                    }
                }
            }

            // DEBUG: Visualize Rays in Scene View
            // Debug.DrawRay(origin, rayDir * sectorScanRange, Color.red, 0.1f);
        }
    }

    public GameObject FindBestAvailableTarget()
    {
        GameObject best = null;
        float bestScore = float.MaxValue;

        foreach (var t in CombinedTargets)
        {
            var obj = t.TargetObject;
            if (!obj) continue;
            float d = (obj.transform.position - transform.position).sqrMagnitude;
            if (d < 25f * 25f) return obj;
            float score = TargetScore(obj) + WeaponBias(obj);
            if (score < bestScore) { bestScore = score; best = obj; }
        }
        return best;
    }

    float TargetScore(GameObject t)
    {
        float d = (t.transform.position - transform.position).sqrMagnitude;
        float pressure = GetTargetWeaponCount(t);
        float baseScore = d * (1f + pressure * 0.15f);
        return ModifyTargetScore(t, baseScore);
    }

    protected virtual float ModifyTargetScore(GameObject target, float baseScore) => baseScore;

    float WeaponBias(GameObject t) => Mathf.Abs((t.GetInstanceID() ^ ID) % 17) * 0.001f;

    public bool ShouldSwitch(GameObject cur, GameObject best)
    {
        if (!cur) return true;
        float dCur = (cur.transform.position - transform.position).sqrMagnitude;
        float dBest = (best.transform.position - transform.position).sqrMagnitude;
        return dBest < dCur * 0.85f;
    }

    public void AddTargetAssignment(GameObject targetObj)
    {
        lock (lockObject) { if (targetWeaponCount.ContainsKey(targetObj)) targetWeaponCount[targetObj]++; else targetWeaponCount[targetObj] = 1; }
    }

    public void RemoveTargetAssignment(GameObject targetObj)
    {
        if (targetObj == null) return;
        lock (lockObject) { if (targetWeaponCount.ContainsKey(targetObj)) { targetWeaponCount[targetObj]--; if (targetWeaponCount[targetObj] <= 0) targetWeaponCount.Remove(targetObj); } }
    }

    int GetTargetWeaponCount(GameObject targetObj)
    {
        lock (lockObject) { return targetWeaponCount.ContainsKey(targetObj) ? targetWeaponCount[targetObj] : 0; }
    }

    ShipTargetingSystem.TargetData[] FindClosest()
    {
        var src = targetProvider.GetTargetDataArray();
        var list = new List<ShipTargetingSystem.TargetData>(src.Length);
        foreach (var t in src)
        {
            if (!t.TargetObject) continue;
            // Check arc visibility before adding to candidates
            if (fireArc < 360f)
            {
                Vector3 localDir = transform.parent.InverseTransformDirection(t.TargetObject.transform.position - transform.position);
                if (Vector3.Angle(Vector3.forward, localDir) > fireArc * 0.5f) continue;
            }

            float d = (t.TargetObject.transform.position - transform.position).sqrMagnitude;
            list.Add(new ShipTargetingSystem.TargetData(t.TargetObject, d * shipCore.distanceThreshold));
        }
        list.Sort((a, b) => a.UnifiedDistance.CompareTo(b.UnifiedDistance));
        return list.ToArray();
    }

    ShipTargetingSystem.TargetData[] FindAngularClosest()
    {
        var src = targetProvider.GetTargetDataArray();
        var list = new List<ShipTargetingSystem.TargetData>(src.Length);
        Vector3 fwd = transform.forward;
        foreach (var t in src)
        {
            if (!t.TargetObject) continue;
            Vector3 dir = t.TargetObject.transform.position - transform.position;
            dir.y = 0f;

            // Check arc visibility
            if (fireArc < 360f)
            {
                Vector3 localDir = transform.parent.InverseTransformDirection(dir);
                if (Vector3.Angle(Vector3.forward, localDir) > fireArc * 0.5f) continue;
            }

            float a = Vector3.Angle(fwd, dir);
            list.Add(new ShipTargetingSystem.TargetData(t.TargetObject, a * shipCore.angularThreshold + shipCore.angularMarging));
        }
        list.Sort((a, b) => a.UnifiedDistance.CompareTo(b.UnifiedDistance));
        return list.ToArray();
    }

    protected virtual void FindWeaponSpawnPoint()
    {
        weaponSpawnPoint = transform.Find("projectileSpawnPoint") ?? transform.Find("weaponSpawnPoint") ?? transform.Find("beamSpawnPoint");
        if (weaponSpawnPoint == null) weaponSpawnPoint = transform;
    }

    public void UpdateFirePower(float weaponsDistributedEnergy)
    {
        // For Beam weapons, fireRate might represent intensity or tick rate
        fireRate = initialFireRate * weaponsDistributedEnergy * 0.001f + (fireType == WeaponFireType.Continuous ? 0f : 2f);
    }

    protected virtual void InitializeWeapon() { }
    protected abstract void Fire(float aimQuality);

    // NEW: Called when trigger is released or weapon can no longer fire
    protected virtual void StopFiring() { }
}

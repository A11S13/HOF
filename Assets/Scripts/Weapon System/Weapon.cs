using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Base weapon class - fully decoupled and reusable
/// </summary>
public abstract class Weapon : MonoBehaviour
{
    // Dependencies (injected)
    protected ShipCore shipCore;
    protected IShipController shipController;
    protected ITargetProvider targetProvider;
    protected Rigidbody shipRb;
    public ShipCore ShipCore => shipCore; // Public accessor for control modes

    public int ID;

    // Control mode
    private IWeaponControlMode controlMode;
    public bool isAI { get; private set; }

    // Targeting state
    public float fireAccumulator { get; set; }
    float _phaseDriftTimer;
    const float PhaseDriftMinInterval = 0.2f;
    const float PhaseDriftMaxInterval = 1.2f;
    const float PhaseDriftAmount = 0.25f;

    public ShipTargetingSystem.TargetData[] CombinedTargets { get; private set; }
    public GameObject prevTarget;

    [Header("Rotation Settings")]
    public float localRotationDeadZone = 0.1f;
    public float rotationSpeed = 5f;

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

    /// <summary>
    /// Initialize weapon with ship dependencies
    /// </summary>
    public void Initialize(ShipCore core, IShipController controller, ITargetProvider targets, bool useAI)
    {
        if (isInitialized)
        {
            Debug.LogWarning($"Weapon {gameObject.name} already initialized!");
            return;
        }

        // Validate dependencies
        if (core == null || controller == null || targets == null)
        {
            Debug.LogError($"Weapon {gameObject.name}: Null dependency!");
            return;
        }

        shipCore = core;
        shipController = controller;
        targetProvider = targets;
        shipRb = controller.ShipRigidbody;

        if (shipRb == null)
        {
            Debug.LogError($"Weapon {gameObject.name}: Ship Rigidbody is null!");
            return;
        }

        // Setup control mode
        isAI = useAI;
        SetupControlMode();

        // Setup weapon
        firingRate = 1;
        if (initialFireRate <= 0f)
            initialFireRate = fireRate;

        fireAccumulator = Random.value;
        fireRate = fireRate * Random.Range(0.985f, 1.015f);
        _phaseDriftTimer = Random.Range(0f, PhaseDriftMaxInterval);

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
                Debug.LogWarning($"Weapon {gameObject.name}: TurretJoystick not found, defaulting to AI");
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

        UpdatePhaseDrift();

        float aimFactor = controlMode.UpdateControl(this);
        TryFire(aimFactor);

        audioDSP?.Drive();
    }

    void UpdatePhaseDrift()
    {
        _phaseDriftTimer -= Time.deltaTime;
        if (_phaseDriftTimer <= 0f)
        {
            fireAccumulator += Random.Range(-PhaseDriftAmount, PhaseDriftAmount);
            if (fireAccumulator < 0f) fireAccumulator = 0f;
            _phaseDriftTimer = Random.Range(PhaseDriftMinInterval, PhaseDriftMaxInterval);
        }
    }

    void TryFire(float aimFactor)
    {
        if (aimFactor <= 0f)
        {
            isFiring = false;
            fireAccumulator = Mathf.Min(fireAccumulator, 1f);
            return;
        }

        fireAccumulator += Time.deltaTime * fireRate;
        firingRate = fireRate;

        if (fireAccumulator >= 1f)
        {
            isFiring = true;
            fireAccumulator -= 1f;

            audioDSP?.NotifyShot();

            Fire(aimFactor);
        }
        else
        {
            isFiring = fireAccumulator >= 0.95f;
        }
    }

    public void ToggleAI()
    {
        isAI = !isAI;
        SetupControlMode();
    }

    // === PUBLIC HELPER METHODS FOR CONTROL MODES ===

    public void RotateTowardsTarget(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                Time.deltaTime * rotationSpeed
            );

            if (Time.frameCount % incrementSkipFrames == 0)
            {
                angularDistance = Quaternion.Angle(transform.rotation, targetRotation);
                if (angularDistance < 0.01f)
                    angularDistance = 0.01f;
            }
        }
    }

    public bool IsAimedAtTarget(Vector3 targetPosition)
    {
        Vector3 directionToTarget = targetPosition - transform.position;
        directionToTarget.y = 0f;
        Vector3 turretForward = transform.forward;
        float angleToTarget = Vector3.Angle(turretForward, directionToTarget);
        return angleToTarget <= shipCore.firingThresholdAngle;
    }

    public virtual Vector3 CalculateAimPosition(GameObject target, Vector3 targetVelocity)
    {
        return target != null ? target.transform.position : transform.position + transform.forward * 100f;
    }

    // === TARGET MANAGEMENT ===

    public void PrioritizeTargets()
    {
        ShipTargetingSystem.TargetData[] closestTargets = FindClosest();
        ShipTargetingSystem.TargetData[] angularClosestTargets = FindAngularClosest();

        Dictionary<GameObject, ShipTargetingSystem.TargetData> combinedDict = new Dictionary<GameObject, ShipTargetingSystem.TargetData>();

        foreach (var targetData in closestTargets)
        {
            if (targetData.TargetObject != null && !combinedDict.ContainsKey(targetData.TargetObject))
            {
                combinedDict[targetData.TargetObject] = targetData;
            }
        }

        foreach (var targetData in angularClosestTargets)
        {
            if (targetData.TargetObject != null)
            {
                if (!combinedDict.ContainsKey(targetData.TargetObject))
                {
                    combinedDict[targetData.TargetObject] = targetData;
                }
                else if (targetData.UnifiedDistance < combinedDict[targetData.TargetObject].UnifiedDistance)
                {
                    combinedDict[targetData.TargetObject] = targetData;
                }
            }
        }

        CombinedTargets = combinedDict.Values.ToArray();
        System.Array.Sort(CombinedTargets, (a, b) => a.UnifiedDistance.CompareTo(b.UnifiedDistance));
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

            if (d < 25f * 25f)
                return obj;

            float score = TargetScore(obj) + WeaponBias(obj);

            if (score < bestScore)
            {
                bestScore = score;
                best = obj;
            }
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

    protected virtual float ModifyTargetScore(GameObject target, float baseScore)
    {
        return baseScore;
    }

    float WeaponBias(GameObject t)
    {
        return Mathf.Abs((t.GetInstanceID() ^ ID) % 17) * 0.001f;
    }

    public bool ShouldSwitch(GameObject cur, GameObject best)
    {
        if (!cur) return true;

        float dCur = (cur.transform.position - transform.position).sqrMagnitude;
        float dBest = (best.transform.position - transform.position).sqrMagnitude;

        return dBest < dCur * 0.85f;
    }

    public void AddTargetAssignment(GameObject targetObj)
    {
        lock (lockObject)
        {
            if (targetWeaponCount.ContainsKey(targetObj))
                targetWeaponCount[targetObj]++;
            else
                targetWeaponCount[targetObj] = 1;
        }
    }

    public void RemoveTargetAssignment(GameObject targetObj)
    {
        if (targetObj == null) return;

        lock (lockObject)
        {
            if (targetWeaponCount.ContainsKey(targetObj))
            {
                targetWeaponCount[targetObj]--;
                if (targetWeaponCount[targetObj] <= 0)
                    targetWeaponCount.Remove(targetObj);
            }
        }
    }

    int GetTargetWeaponCount(GameObject targetObj)
    {
        lock (lockObject)
        {
            return targetWeaponCount.ContainsKey(targetObj) ? targetWeaponCount[targetObj] : 0;
        }
    }

    ShipTargetingSystem.TargetData[] FindClosest()
    {
        var src = targetProvider.GetTargetDataArray();
        if (src.Length == 0) return src;

        var list = new List<ShipTargetingSystem.TargetData>(src.Length);

        foreach (var t in src)
        {
            if (!t.TargetObject) continue;

            float d = (t.TargetObject.transform.position - transform.position).sqrMagnitude;
            list.Add(new ShipTargetingSystem.TargetData(t.TargetObject, d * shipCore.distanceThreshold));
        }

        list.Sort((a, b) => a.UnifiedDistance.CompareTo(b.UnifiedDistance));
        return list.ToArray();
    }

    ShipTargetingSystem.TargetData[] FindAngularClosest()
    {
        var src = targetProvider.GetTargetDataArray();
        if (src.Length == 0) return src;

        var list = new List<ShipTargetingSystem.TargetData>(src.Length);
        Vector3 fwd = transform.forward;

        foreach (var t in src)
        {
            if (!t.TargetObject) continue;

            Vector3 dir = t.TargetObject.transform.position - transform.position;
            dir.y = 0f;

            float a = Vector3.Angle(fwd, dir);
            list.Add(new ShipTargetingSystem.TargetData(
                t.TargetObject,
                a * shipCore.angularThreshold + shipCore.angularMarging
            ));
        }

        list.Sort((a, b) => a.UnifiedDistance.CompareTo(b.UnifiedDistance));
        return list.ToArray();
    }

    // === ABSTRACT/VIRTUAL METHODS ===

    protected virtual void FindWeaponSpawnPoint()
    {
        weaponSpawnPoint = transform.Find("projectileSpawnPoint")
                        ?? transform.Find("weaponSpawnPoint")
                        ?? transform.Find("beamSpawnPoint");

        if (weaponSpawnPoint == null)
            weaponSpawnPoint = transform;
    }

    public void UpdateFirePower(float weaponsDistributedEnergy)
    {
        fireRate = initialFireRate * weaponsDistributedEnergy * 0.001f + 2f;
    }

    protected virtual void InitializeWeapon() { }

    protected abstract void Fire(float aimQuality);
}

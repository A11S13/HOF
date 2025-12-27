using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class Weapon : MonoBehaviour
{
    ShipCore _sC;

    protected Rigidbody shipRb;

    public int ID;

    float reactionDelay;
    float nextReactionTime;
    float targetLostUntil;
    float fireAccumulator;
    public float FirePhase => fireAccumulator;

    float _phaseDriftTimer;
    const float PhaseDriftMinInterval = 0.2f;
    const float PhaseDriftMaxInterval = 1.2f;
    const float PhaseDriftAmount = 0.25f;

    public ShipCore.TargetData[] CombinedTargets { get; private set; }
    protected GameObject target;
    public GameObject prevTarget;
    public bool isAI;
    public float localRotationDeadZone = 0.1f;
    public float rotationSpeed = 5f;
    private FixedJoystick turretJoystick;
    protected Transform weaponSpawnPoint;

    Vector3 lastTargetPos;
    protected Vector3 targetVel;
    const float VelSampleDt = 0.05f;
    float velSampleTimer;

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

    void Awake()
    {
        fireAudio = GetComponent<AudioSource>();
    }

    void Start()
    {
        firingRate = 1;

        if (initialFireRate <= 0f)
            initialFireRate = fireRate;

        fireAccumulator = Random.value;

        fireRate = fireRate * Random.Range(0.985f, 1.015f);

        _phaseDriftTimer = Random.Range(0f, PhaseDriftMaxInterval);

        isAI = false;
        target = null;
        incrementSkipFrames = 3;
        audioDSP = GetComponent<WeaponAudioDSP>();
        shipRb = GameObject.Find("PLAYER").GetComponent<Rigidbody>();
        _sC = GameObject.Find("MOTHERSHIP").GetComponent<ShipCore>();
        _sC.SetWeaponsID();
        FindWeaponSpawnPoint();
        turretJoystick = GameObject
            .FindGameObjectWithTag("TurretJoystick")
            .GetComponent<FixedJoystick>();

        reactionDelay = Random.Range(0.0f, 0.75f);
        nextReactionTime = 0f;

        InitializeWeapon();
    }


    void OnDestroy()
    {
        if (target != null)
        {
            RemoveTargetAssignment(target);
        }
    }

    void Update()
    {
        _phaseDriftTimer -= Time.deltaTime;
        if (_phaseDriftTimer <= 0f)
        {
            fireAccumulator += Random.Range(-PhaseDriftAmount, PhaseDriftAmount);

            if (fireAccumulator < 0f) fireAccumulator = 0f;

            _phaseDriftTimer = Random.Range(PhaseDriftMinInterval, PhaseDriftMaxInterval);
        }

        isFiring = false;

        if (isAI) AIControl();
        else ManualControl();

        audioDSP?.Drive();  
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



    private void AIControl()
    {
        if (target)
        {
            var t = target.GetComponent<Targetable>();
            if (t == null || !t.IsAlive)
            {
                RemoveTargetAssignment(target);
                target = null;
                targetLostUntil = Time.time + reactionDelay;
                return;
            }
        }

        if (Time.time >= targetLostUntil)
            PickTarget();

        if (!target)
            return;

        velSampleTimer -= Time.deltaTime;
        if (velSampleTimer <= 0f)
        {
            var pos = target.transform.position;
            targetVel = (pos - lastTargetPos) / VelSampleDt;
            lastTargetPos = pos;
            velSampleTimer = VelSampleDt;
        }

        var aimPos = CalculateAimPosition();

        RotateTowardsTarget(aimPos);

        if (IsAimedAtTarget(aimPos))
        {
            float aim = 1f - angularDistance / _sC.firingThresholdAngle;
            TryFire(aim);
        }
    }

    protected virtual Vector3 CalculateAimPosition()
    {
        return target != null ? target.transform.position : transform.position + transform.forward * 100f;
    }

    bool ShouldSwitch(GameObject cur, GameObject best)
    {
        if (!cur) return true;

        float dCur = (cur.transform.position - transform.position).sqrMagnitude;
        float dBest = (best.transform.position - transform.position).sqrMagnitude;

        return dBest < dCur * 0.85f;
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

    private void PickTarget()
    {
        PrioritizeTargets();
        if (CombinedTargets.Length == 0)
        {
            target = null;
            return;
        }

        GameObject best = FindBestAvailableTarget();
        if (best == null)
        {
            target = CombinedTargets[0].TargetObject;
            AddTargetAssignment(target);
            return;
        }

        float jitter = 0.15f;
        float now = Time.time + Random.Range(-jitter, jitter);

        bool urgent =
            target &&
            (best.transform.position - transform.position).sqrMagnitude <
            (target.transform.position - transform.position).sqrMagnitude * 0.5f;

        if (best != target && (urgent || (ShouldSwitch(target, best) && now >= nextReactionTime)))
        {
            if (target) RemoveTargetAssignment(target);

            target = best;

            fireAccumulator += Random.Range(0f, 0.3f);

            lastTargetPos = target.transform.position;
            targetVel = Vector3.zero;
            AddTargetAssignment(target);

            nextReactionTime = now + reactionDelay;

        }
    }

    private GameObject FindBestAvailableTarget()
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

    private ShipCore.TargetData[] PrioritizeTargets()
    {
        ShipCore.TargetData[] closestTargets = FindClosest();
        ShipCore.TargetData[] angularClosestTargets = FindAngularClosest();

        Dictionary<GameObject, ShipCore.TargetData> combinedDict = new Dictionary<GameObject, ShipCore.TargetData>();

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

        return CombinedTargets;
    }

    private void AddTargetAssignment(GameObject targetObj)
    {
        lock (lockObject)
        {
            if (targetWeaponCount.ContainsKey(targetObj))
            {
                targetWeaponCount[targetObj]++;
            }
            else
            {
                targetWeaponCount[targetObj] = 1;
            }
        }
    }

    private void RemoveTargetAssignment(GameObject targetObj)
    {
        if (targetObj == null) return;

        lock (lockObject)
        {
            if (targetWeaponCount.ContainsKey(targetObj))
            {
                targetWeaponCount[targetObj]--;
                if (targetWeaponCount[targetObj] <= 0)
                {
                    targetWeaponCount.Remove(targetObj);
                }
            }
        }
    }

    private int GetTargetWeaponCount(GameObject targetObj)
    {
        lock (lockObject)
        {
            return targetWeaponCount.ContainsKey(targetObj) ? targetWeaponCount[targetObj] : 0;
        }
    }

    private void RotateTowardsTarget(Vector3 targetPosition)
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
                {
                    angularDistance = 0.01f;
                }
            }
        }
    }

    private bool IsAimedAtTarget(Vector3 targetPosition)
    {
        Vector3 directionToTarget = targetPosition - transform.position;
        directionToTarget.y = 0f;
        Vector3 turretForward = transform.forward;
        float angleToTarget = Vector3.Angle(turretForward, directionToTarget);
        return angleToTarget <= _sC.firingThresholdAngle;
    }

    public void ToggleAI()
    {
        isAI = !isAI;
    }

    protected virtual void FindWeaponSpawnPoint()
    {
        weaponSpawnPoint = transform.Find("projectileSpawnPoint")
                        ?? transform.Find("weaponSpawnPoint")
                        ?? transform.Find("beamSpawnPoint");

        if (weaponSpawnPoint == null)
        {
            weaponSpawnPoint = transform;
        }
    }

    public void UpdateFirePower(float weaponsDistributedEnergy)
    {
        float oldRate = fireRate;
        fireRate = initialFireRate * weaponsDistributedEnergy * 0.001f + 2f;
    }


    private void ManualControl()
    {
        HandleTurretRotation();
        HandleFire();
    }

    void HandleTurretRotation()
    {
        Vector3 turretDirection = new Vector3(
            turretJoystick.Direction.x,
            0,
            turretJoystick.Direction.y
        );

        if (turretDirection.magnitude > localRotationDeadZone)
        {
            float angle = Vector3.SignedAngle(Vector3.forward, turretDirection, Vector3.up);
            Quaternion targetRotation = Quaternion.Euler(0f, angle, 0f);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                Time.deltaTime * rotationSpeed
            );
        }
    }

    void HandleFire()
    {
        Vector3 fireDir = new Vector3(
            turretJoystick.Direction.x,
            0,
            turretJoystick.Direction.y
        );

        float mag = fireDir.magnitude;
        if (mag <= turretJoystick.DeadZone) return;

        TryFire(Mathf.Clamp01(mag));
    }

    private ShipCore.TargetData[] FindClosest()
    {
        var src = _sC.targetDataArray;
        if (src.Length == 0) return src;

        var list = new List<ShipCore.TargetData>(src.Length);

        foreach (var t in src)
        {
            if (!t.TargetObject) continue;

            float d = (t.TargetObject.transform.position - transform.position).sqrMagnitude;
            list.Add(new ShipCore.TargetData(t.TargetObject, d * _sC.distanceThreshold));
        }

        list.Sort((a, b) => a.UnifiedDistance.CompareTo(b.UnifiedDistance));
        return list.ToArray();
    }

    private ShipCore.TargetData[] FindAngularClosest()
    {
        var src = _sC.targetDataArray;
        if (src.Length == 0) return src;

        var list = new List<ShipCore.TargetData>(src.Length);
        Vector3 fwd = transform.forward;

        foreach (var t in src)
        {
            if (!t.TargetObject) continue;

            Vector3 dir = t.TargetObject.transform.position - transform.position;
            dir.y = 0f;

            float a = Vector3.Angle(fwd, dir);
            list.Add(new ShipCore.TargetData(
                t.TargetObject,
                a * _sC.angularThreshold + _sC.angularMarging
            ));
        }

        list.Sort((a, b) => a.UnifiedDistance.CompareTo(b.UnifiedDistance));
        return list.ToArray();
    }

    protected virtual void InitializeWeapon() { }

    protected abstract void Fire(float aimQuality);
}

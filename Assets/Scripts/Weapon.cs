using System.Linq;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Weapon : MonoBehaviour
{
    ShipCore _sC;

    public Rigidbody shipRb;

    public int ID;

    float reactionDelay;
    float nextReactionTime;
    float targetLostUntil;
    float fireAccumulator;
    public float FirePhase => fireAccumulator;

    // small randomized desync helpers
    float _phaseDriftTimer;
    const float PhaseDriftMinInterval = 0.2f;
    const float PhaseDriftMaxInterval = 1.2f;
    const float PhaseDriftAmount = 0.25f; // max amount to nudge accumulator


    public ShipCore.TargetData[] CombinedTargets { get; private set; }
    public GameObject target;
    public GameObject prevTarget;
    public GameObject indicatorRed;
    public GameObject indicatorGreen;
    public GameObject indicatorBlue;
    public bool isAI;
    public float localRotationDeadZone = 0.1f;
    public float rotationSpeed = 5f;
    private FixedJoystick turretJoystick;
    private Transform projectileSpawnPoint;
    float projectileSpeed;
    public float initialFireRate;

    Vector3 lastTargetPos;
    Vector3 targetVel;
    const float VelSampleDt = 0.05f; // 20 Hz perception
    float velSampleTimer;
    const float MinPredictTime = 0.05f; // seconds

    [SerializeField]
    private float fireRate;
    bool isFiring;
    float firingRate;
    private float nextFireTime;

    public GameObject projectilePrefab;

    public float angularDistance;
    public int incrementSkipFrames;

    private AudioSource fireAudio;
    WeaponAudioDSP audioDSP;

    // Target assignment tracking
    private static Dictionary<GameObject, int> targetWeaponCount = new Dictionary<GameObject, int>();
    private static System.Object lockObject = new System.Object();

    LineRenderer lr;

    void Awake()
    {
        fireAudio = GetComponent<AudioSource>();
    }

    void Start()
    {
        fireAccumulator = Random.value;
        // desync base rate a touch so identical weapons don't tick perfectly together
        fireRate *= Random.Range(0.985f, 1.015f);

        // initialize phase drift
        _phaseDriftTimer = Random.Range(0f, PhaseDriftMaxInterval);

        isAI = false;
        target = null;
        incrementSkipFrames = 3;
        projectileSpeed = projectilePrefab.GetComponent<Projectile>().speed;
        audioDSP = GetComponent<WeaponAudioDSP>();
        shipRb = GameObject.Find("PLAYER").GetComponent<Rigidbody>();
        _sC = GameObject.Find("MOTHERSHIP").GetComponent<ShipCore>();
        _sC.SetWeaponsID();
        FindProjectileSpawnPoint();
        turretJoystick = GameObject
            .FindGameObjectWithTag("TurretJoystick")
            .GetComponent<FixedJoystick>();

        // Desync the first shot
        nextFireTime = Time.time + Random.value * (1f / Mathf.Max(1f, fireRate));

        reactionDelay = Random.Range(0.0f, 0.75f);
        nextReactionTime = 0f;

        lr = gameObject.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.widthMultiplier = 0.05f;
    }

    void OnDestroy()
    {
        // Clean up target assignment when weapon is destroyed
        if (target != null)
        {
            RemoveTargetAssignment(target);
        }
    }

    void Update()
    {
        fireAccumulator += Time.deltaTime * fireRate;
        fireAccumulator -= Mathf.Floor(fireAccumulator);

        // occasional low-frequency phase drift to break phase lock
        _phaseDriftTimer -= Time.deltaTime;
        if (_phaseDriftTimer <= 0f)
        {
            // nudge the phase by a small random amount, clamp to [0,1)
            fireAccumulator = (fireAccumulator + Random.Range(-PhaseDriftAmount, PhaseDriftAmount)) % 1f;
            if (fireAccumulator < 0f) fireAccumulator += 1f;
            _phaseDriftTimer = Random.Range(PhaseDriftMinInterval, PhaseDriftMaxInterval);
        }


        isFiring = false;

        if (isAI) AIControl();
        else ManualControl();

        audioDSP.Drive(isFiring, fireAccumulator);
    }

    void TryFire(float aimFactor)
    {
        if (aimFactor <= 0f)
        {
            isFiring = false;
            return;
        }

        isFiring = true;
        firingRate = fireRate;

        if (fireAccumulator < Time.deltaTime * fireRate)
            Fire();
    }

    private void AIControl()
    {
        if (target && (target.GetComponent<Enemy>() == null || target.GetComponent<Enemy>().isDestroyed))
        {
            RemoveTargetAssignment(target);
            target = null;
            targetLostUntil = Time.time + reactionDelay;
            return;
        }

        if (Time.time >= targetLostUntil)
            PickTarget();   // <-- ALWAYS re-evaluate

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


        var to = target.transform.position - projectileSpawnPoint.position;

        var aimPos =
            to.magnitude / projectileSpeed < MinPredictTime
            ? target.transform.position
            : Predict(projectileSpawnPoint.position, target.transform.position, targetVel, projectileSpeed);



        RotateTowardsTarget(aimPos);

        if (IsAimedAtTarget(aimPos))
        {
            float aim = 1f - angularDistance / _sC.firingThresholdAngle;
            TryFire(aim);
        }

        lr.SetPosition(0, projectileSpawnPoint.position);
        lr.SetPosition(1, aimPos);
        Debug.DrawLine(transform.position, target.transform.position, Color.white);
        Debug.DrawLine(transform.position, aimPos, Color.green);

    }
    Vector3 Predict(Vector3 origin, Vector3 pos, Vector3 vel, float speed)
    {
        vel -= shipRb.linearVelocity; // critical fix

        var to = pos - origin;
        //var t = to.magnitude / speed;

        var t = Mathf.Min(to.magnitude / speed, 2f);
        return pos + vel * t;
    }

    bool ShouldSwitch(GameObject cur, GameObject best)
    {
        if (!cur) return true;

        float dCur = (cur.transform.position - transform.position).sqrMagnitude;
        float dBest = (best.transform.position - transform.position).sqrMagnitude;

        // switch early but stable (~15% closer)
        return dBest < dCur * 0.85f;
    }

    float TargetScore(GameObject t)
    {
        float d = (t.transform.position - transform.position).sqrMagnitude;
        float pressure = GetTargetWeaponCount(t);

        return d * (1f + pressure * 0.15f);
    }

    float WeaponBias(GameObject t)
    {
        return Mathf.Abs((t.GetInstanceID() ^ ID) % 17) * 0.001f;
    }

    private void PickTarget()
    {
        indicatorRed.SetActive(true);
        indicatorGreen.SetActive(false);
        indicatorBlue.SetActive(false);

        PrioritizeTargets();
        if (CombinedTargets.Length == 0)
        {
            target = null;
            UpdateTargetIndicators();
            return;
        }


        GameObject best = FindBestAvailableTarget();
        if (best == null)
        {
            target = CombinedTargets[0].TargetObject;
            AddTargetAssignment(target);
            UpdateTargetIndicators();
            return;
        }

        // -------------------------------------------------
        // Desync: apply a tiny per-turret jitter
        // -------------------------------------------------
        float jitter = 0.15f; // keep tiny; prevents sync without lagging turrets
        float now = Time.time + Random.Range(-jitter, jitter);

        // -------------------------------------------------
        // Change only when allowed
        // -------------------------------------------------
        bool urgent =
            target &&
            (best.transform.position - transform.position).sqrMagnitude <
            (target.transform.position - transform.position).sqrMagnitude * 0.5f;

        if (best != target && (urgent || (ShouldSwitch(target, best) && now >= nextReactionTime)))
        {
            if (target) RemoveTargetAssignment(target);

            target = best;
            // small phase kick on target switch to avoid synchronized re-firing when many turrets switch at once
            fireAccumulator = (fireAccumulator + Random.Range(0f, 0.5f)) % 1f;

            lastTargetPos = target.transform.position;
            targetVel = Vector3.zero;
            AddTargetAssignment(target);

            fireAccumulator += Time.deltaTime * fireRate * (1f + WeaponBias(target));
            //fireAccumulator = Random.value; // break phase lock
            nextReactionTime = now + reactionDelay;
        }

        UpdateTargetIndicators();
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

            // hard override: close threats always win
            if (d < 25f * 25f)
                return obj;

            float score = d * (1f + GetTargetWeaponCount(obj) * 0.15f)
              + WeaponBias(obj);

            if (score < bestScore)
            {
                bestScore = score;
                best = obj;
            }
        }

        return best;
    }

    private void UpdateTargetIndicators()
    {
        if (target == null)
        {
            indicatorRed.SetActive(true);
            indicatorGreen.SetActive(false);
            indicatorBlue.SetActive(false);
            return;
        }

        int weaponCount = GetTargetWeaponCount(target);

        if (weaponCount == 1)
        {
            // Only this weapon is targeting
            indicatorRed.SetActive(false);
            indicatorGreen.SetActive(true);
            indicatorBlue.SetActive(false);
        }
        else
        {
            // Multiple weapons targeting the same enemy
            indicatorRed.SetActive(false);
            indicatorGreen.SetActive(false);
            indicatorBlue.SetActive(true);
        }
    }

    private ShipCore.TargetData[] PrioritizeTargets()
    {
        ShipCore.TargetData[] closestTargets = FindClosest();
        ShipCore.TargetData[] angularClosestTargets = FindAngularClosest();

        // Combine and remove duplicates more efficiently
        Dictionary<GameObject, ShipCore.TargetData> combinedDict = new Dictionary<GameObject, ShipCore.TargetData>();

        // Add closest targets first
        foreach (var targetData in closestTargets)
        {
            if (targetData.TargetObject != null && !combinedDict.ContainsKey(targetData.TargetObject))
            {
                combinedDict[targetData.TargetObject] = targetData;
            }
        }

        // Add angular closest targets, keeping the best unified distance
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

        // Sort by unified distance
        System.Array.Sort(CombinedTargets, (a, b) => a.UnifiedDistance.CompareTo(b.UnifiedDistance));

        return CombinedTargets;
    }

    private ShipCore.TargetData[] RePrioritizeTarget()
    {
        if (CombinedTargets.Length > 0)
        {
            CombinedTargets = _sC.ArrangeTargets(CombinedTargets);
            if (CombinedTargets.Length > 0)
            {
                GameObject newTarget = CombinedTargets[0].TargetObject;
                if (newTarget != target)
                {
                    RemoveTargetAssignment(target);
                    target = newTarget;
                    AddTargetAssignment(target);
                }
                return CombinedTargets;
            }
        }
        return null;
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

    // Thread-safe target assignment methods
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

    // Rest of your existing methods remain the same...
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


    void FindProjectileSpawnPoint()
    {
        projectileSpawnPoint = transform.Find("projectileSpawnPoint");
    }

    public void UpdateFirePower(float weaponsDistributedEnergy)
    {
        fireRate = initialFireRate * weaponsDistributedEnergy * 0.001f + 2f;
    }

    void Fire()
    {
        float spread = 3f;
        float yaw = Random.Range(-spread, spread);
        Quaternion rot = projectileSpawnPoint.rotation * Quaternion.Euler(0, yaw, 0);

        var p = Instantiate(projectilePrefab, projectileSpawnPoint.position, rot);
        p.GetComponent<Projectile>().Initialize(shipRb.linearVelocity);
        Destroy(p, 3f);
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

        // Manual aim quality = stick deflection
        TryFire(Mathf.Clamp01(mag));
    }
}
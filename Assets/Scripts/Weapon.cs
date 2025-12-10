using System.Linq;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Weapon : MonoBehaviour
{
    ShipCore _sC;
    ChipSynth _cS;
    AudioSource audioSource;

    public Rigidbody shipRb;

    public int ID;

    float reactionDelay;
    float nextReactionTime;
    [SerializeField] float switchAggression = 3.75f;

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
    public float initialFireRate = 40f;

    [SerializeField]
    private float fireRate;
    private float nextFireTime;
    public Collider[] _enemyColliders;

    public GameObject projectilePrefab;
    public GameObject projectileSoundHolderPrefab;

    public float angularDistance;
    public int incrementSkipFrames;

    private AudioSource fireAudio;
    [SerializeField] float soundRate = 20f; // sounds/sec
    static float nextSoundTime; // shared across all Weapon instances

    // Target assignment tracking
    private static Dictionary<GameObject, int> targetWeaponCount = new Dictionary<GameObject, int>();
    private static System.Object lockObject = new System.Object();

    void Awake()
    {
        fireAudio = GetComponent<AudioSource>();
    }

    void Start()
    {
        isAI = false;
        target = null;
        incrementSkipFrames = 3;
        shipRb = GameObject.Find("PLAYER").GetComponent<Rigidbody>();
        _sC = GameObject.Find("MOTHERSHIP").GetComponent<ShipCore>();
        _sC.SetWeaponsID();
        FindProjectileSpawnPoint();
        turretJoystick = GameObject
            .FindGameObjectWithTag("TurretJoystick")
            .GetComponent<FixedJoystick>();

        // Desync the first shot
        nextFireTime = Random.Range(0f, 0.8f);

        reactionDelay = Random.Range(0.1f, 1.75f);
        nextReactionTime = 0f;
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
        if (isAI)
        {
            AIControl();
        }
        else
        {
            ManualControl();
        }
    }

    private void AIControl()
    {
        if (target == null || target.GetComponent<Enemy>() == null || target.GetComponent<Enemy>().isDestroyed)
        {
            if (target != null)
            {
                RemoveTargetAssignment(target);
            }
            target = null;
            _sC.ActualizeTargets();
            PickTarget();
            return;
        }

        if (target != null)
        {
            RotateTowardsTarget(target.transform.position);

            if (IsAimedAtTarget(target.transform.position))
            {
                float dist = Vector3.Distance(transform.position, target.transform.position);

                if (!target.GetComponent<Enemy>().isDestroyed && dist <= _sC.sonarRadius)
                {
                    if (CanFire())
                        Fire();
                }
                else
                {
                    RemoveTargetAssignment(target);
                    _sC.FormClosestEnemiesArray();
                    PickTarget();
                }
            }
        }
        else
        {
            if (_sC.targetDataArray.Length > 0)
                PickTarget();
        }
    }

    bool ShouldSwitch(GameObject cur, GameObject best)
    {
        if (cur == null) return true;

        float dCur = (cur.transform.position - transform.position).sqrMagnitude;
        float dBest = (best.transform.position - transform.position).sqrMagnitude;

        // switch sooner — new target only needs to be slightly closer
        return dBest * switchAggression < dCur;
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
        if (best != target && ShouldSwitch(target, best) && now >= nextReactionTime)
        {
            if (target != null) RemoveTargetAssignment(target);

            target = best;
            AddTargetAssignment(target);

            nextReactionTime = now + reactionDelay;
        }

        UpdateTargetIndicators();
    }

    private GameObject FindBestAvailableTarget()
    {
        foreach (var targetData in CombinedTargets)
        {
            if (targetData.TargetObject == null) continue;

            int weaponCount = GetTargetWeaponCount(targetData.TargetObject);

            // Use the Weapons array length directly from ShipCore
            if (weaponCount < _sC.Weapons.Length)
            {
                return targetData.TargetObject;
            }
        }

        // If all targets are at max weapon count, return the best one anyway
        return CombinedTargets.Length > 0 ? CombinedTargets[0].TargetObject : null;
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
        if (_sC.targetDataArray.Length == 0)
            return new ShipCore.TargetData[0];

        int length = Mathf.Min(_sC.Weapons.Length, _sC.targetDataArray.Length);
        List<ShipCore.TargetData> closestTargets = new List<ShipCore.TargetData>();

        for (int i = 0; i < length; i++)
        {
            if (_sC.targetDataArray[i].TargetObject != null)
            {
                GameObject enemy = _sC.targetDataArray[i].TargetObject;
                float distance = Vector3.Distance(transform.position, enemy.transform.position);

                closestTargets.Add(new ShipCore.TargetData(
                    enemy,
                    distance * _sC.distanceThreshold
                ));
            }
        }

        return closestTargets.ToArray();
    }

    private ShipCore.TargetData[] FindAngularClosest()
    {
        if (_sC.targetDataArray.Length == 0)
            return new ShipCore.TargetData[0];

        int length = Mathf.Min(_sC.Weapons.Length, _sC.targetDataArray.Length);
        List<ShipCore.TargetData> angularTargets = new List<ShipCore.TargetData>();

        for (int i = 0; i < length; i++)
        {
            if (_sC.targetDataArray[i].TargetObject != null)
            {
                GameObject enemy = _sC.targetDataArray[i].TargetObject;
                Vector3 directionToEnemy = enemy.transform.position - transform.position;
                directionToEnemy.y = 0f;
                Vector3 turretForward = transform.forward;
                float angularDistance = Vector3.Angle(turretForward, directionToEnemy);

                angularTargets.Add(new ShipCore.TargetData(
                    enemy,
                    angularDistance * _sC.angularThreshold + _sC.angularMarging
                ));
            }
        }

        return angularTargets.ToArray();
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

    bool CanFire()
    {
        return Time.fixedTime >= nextFireTime && Time.fixedDeltaTime > 0;
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

        float jitter = Random.Range(-0.05f, 0.05f);   // tiny desync, per-shot
        nextFireTime = Time.fixedTime + 1f / fireRate + jitter;

        GetComponent<WeaponAudioPool>().PlayShot();
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
        Vector3 fireDirection = new Vector3(
            turretJoystick.Direction.x,
            0,
            turretJoystick.Direction.y
        );

        if (fireDirection.magnitude > turretJoystick.DeadZone)
        {
            if (CanFire())
            {
                Fire();
            }
        }
    }
}
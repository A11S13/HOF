using System.Linq;
using UnityEngine;
using System.Collections;

public class Weapon : MonoBehaviour
{
    ShipCore _sC;
    ChipSynth _cS;
    AudioSource audioSource;

    public Rigidbody shipRb;

    public int ID;

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
        if (target != null)
        {
            target.GetComponent<Enemy>().isTargeted = true;
            //target.transform.Find("IndicatorRed").gameObject.SetActive(true);
            //target.transform.Find("IndicatorBlue").gameObject.SetActive(false);
            // 2. Rotate Towards Target
            RotateTowardsTarget(target.transform.position);
            // 3. Fire if Aimed
            if (IsAimedAtTarget(target.transform.position))
            {
                if (!target.GetComponent<Enemy>().isDestroyed)
                {
                    if (CanFire())
                    {
                        Fire();
                    }
                }
                else
                {
                    //Debug.Log("+ FRAG!");
                    _sC.FormClosestEnemiesArray();
                    if (_sC.targetDataArray.Length > 0)
                        PickTarget();
                }
            }
        }
        else
        {
            if (_sC.targetDataArray.Length > 0)
                PickTarget();
        }
        if (
            target != null
            && Vector3.Distance(transform.position, target.transform.position)
                > _sC.sonarRadius
            && !target.GetComponent<Enemy>().isDestroyed
        )
        {
            //Debug.Log("Target is too far away");
            target.GetComponent<Enemy>().isTargeted = false;
            //target.transform.Find("IndicatorRed").gameObject.SetActive(false);
            _sC.ActualizeTargets();
            PickTarget();
        }
    }

    private void PickTarget()
    {
        indicatorRed.SetActive(true);
        indicatorGreen.SetActive(false);
        indicatorBlue.SetActive(false);

        PrioritizeTargets();
        if (CombinedTargets.Length != 0)
        {
            prevTarget = CombinedTargets[0].TargetObject;
        }
        else
        {
            prevTarget = null;
        }

        RePrioritizeTarget();
        if (target == prevTarget && target != null)
        {
            indicatorRed.SetActive(false);
            indicatorGreen.SetActive(true);
            indicatorBlue.SetActive(false);
        }

        if (target != prevTarget && target != null && !target.GetComponent<Enemy>().isDestroyed)
        {
            indicatorRed.SetActive(false);
            indicatorGreen.SetActive(false);
            indicatorBlue.SetActive(true);
        }
    }

    private ShipCore.TargetData[] PrioritizeTargets()
    {
        ShipCore.TargetData[] closestTargets = FindClosest();
        ShipCore.TargetData[] angularClosestTargets = FindAngularClosest();

        int length = angularClosestTargets.Length + closestTargets.Length;
        CombinedTargets = new ShipCore.TargetData[length];

        for (int i = 0, j = 0; j < length; i++, j++)
        {
            CombinedTargets[j] = closestTargets[i];
            CombinedTargets[j + 1] = angularClosestTargets[i];
            j++;
        }

        // Loop through the array to filter duplicates based on unifiedDistance
        for (int i = 0; i < CombinedTargets.Length; i++)
        {
            for (int j = i + 1; j < CombinedTargets.Length; j++)
            {
                if (CombinedTargets[i].TargetObject == CombinedTargets[j].TargetObject)
                {
                    // If the current target has a smaller unifiedDistance, keep it
                    if (CombinedTargets[j].UnifiedDistance < CombinedTargets[i].UnifiedDistance)
                    {
                        CombinedTargets[i] = CombinedTargets[j];
                    }
                    // Remove the duplicate
                    CombinedTargets[j] = new ShipCore.TargetData(); // Use the default constructor
                }
            }
        }

        // Remove default entries from the array
        CombinedTargets = CombinedTargets
            .Where(target => !target.Equals(default(ShipCore.TargetData)))
            .ToArray();

        // Remove elements with isTargeted == true
        CombinedTargets = CombinedTargets
            .Where(targetData => !targetData.TargetObject.GetComponent<Enemy>().isTargeted)
            .ToArray();

        System.Array.Sort(
            CombinedTargets,
            (a, b) => a.UnifiedDistance.CompareTo(b.UnifiedDistance)
        );
        return CombinedTargets;
    }

    private ShipCore.TargetData[] RePrioritizeTarget()
    {
        if (CombinedTargets.Length > 0)
        {
            CombinedTargets = _sC.ArrangeTargets(CombinedTargets);
            if (CombinedTargets.Length > 0)
            {
                target = CombinedTargets[0].TargetObject;
                return CombinedTargets;
            }
        }
        return null;
    }

    private ShipCore.TargetData[] FindClosest()
    {
        int length = Mathf.Min(_sC.Weapons.Length, _sC.targetDataArray.Length);
        ShipCore.TargetData[] closestTargetsArray = new ShipCore.TargetData[length];

        if (_sC.targetDataArray.Length > 0)
        {
            for (int i = 0; i < length; i++)
            {
                if (_sC.targetDataArray[i].GameObject != null)
                {
                    GameObject enemy = _sC.targetDataArray[i].GameObject; // Use index i to get the current enemy
                    float distance = Vector3.Distance(transform.position, enemy.transform.position);

                    closestTargetsArray[i] = new ShipCore.TargetData(
                        enemy,
                        distance * _sC.distanceThreshold
                    );
                }

            }
        }
        return closestTargetsArray;
    }

    private ShipCore.TargetData[] FindAngularClosest()
    {
        int length = Mathf.Min(_sC.Weapons.Length, _sC.targetDataArray.Length);
        ShipCore.TargetData[] closestTargetsArray = new ShipCore.TargetData[length];

        if (_sC.targetDataArray.Length > 0)
        {
            for (int i = 0; i < length; i++)
            {
                if (_sC.targetDataArray[i].GameObject != null)
                {
                    GameObject enemy = _sC.targetDataArray[i].GameObject;
                    Vector3 directionToEnemy = enemy.transform.position - transform.position;
                    directionToEnemy.y = 0f;
                    Vector3 turretForward = transform.forward;
                    float angularDistance = Vector3.Angle(turretForward, directionToEnemy);

                    closestTargetsArray[i] = new ShipCore.TargetData(
                        enemy,
                        angularDistance * _sC.angularThreshold + _sC.angularMarging
                    );
                }
            }
        }
        return closestTargetsArray;
    }

    // Method to rotate towards a target position
    private void RotateTowardsTarget(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f; // Keep the rotation only in the horizontal plane

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
                // Calculate the angular distance in degrees
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
        directionToTarget.y = 0f; // Keep the check only in the horizontal plane

        Vector3 turretForward = transform.forward;

        float angleToTarget = Vector3.Angle(turretForward, directionToTarget);

        // Set your firing threshold angle (adjust as needed)
        return angleToTarget <= _sC.firingThresholdAngle;
    }

    public void ToggleAI()
    {
        isAI = !isAI;
    }

    bool CanFire()
    {
        // Check if enough time has passed since the last firing, adjusted by Time.fixedDeltaTime
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
        float randomYaw = Random.Range(-spread, spread);
        Quaternion spreadRot = projectileSpawnPoint.rotation * Quaternion.Euler(0, randomYaw, 0);

        GameObject p = Instantiate(projectilePrefab, projectileSpawnPoint.position, spreadRot);
        p.GetComponent<Projectile>().Initialize(shipRb.linearVelocity);
        nextFireTime = Time.fixedTime + 1.0f / fireRate;
        Destroy(p, 3f);

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

        // Check if the joystick is touched outside the dead zone for rotation
        if (turretDirection.magnitude > localRotationDeadZone)
        {
            float angle = Vector3.SignedAngle(Vector3.forward, turretDirection, Vector3.up);
            Quaternion targetRotation = Quaternion.Euler(0f, angle, 0f);

            // Smoothly rotate the turret
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

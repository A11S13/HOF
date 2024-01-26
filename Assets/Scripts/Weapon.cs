using System.Linq;
using UnityEngine;

public class Weapon : MonoBehaviour
{
    ShipCore _sC;
    ChipSynth _cS;
    AudioSource audioSource;

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

    public float initialPitch;
    public float finalPitch;
    public float angularDistance;
    public float pitchIncrement;
    public float arpeggioRange;
    public int incrementSkipFrames;

    void Awake()
    {
        //_enemyColliders = _sC.enemyColliders;
    }

    void Start()
    {
        isAI = true;
        target = null;
        audioSource = gameObject.GetComponent<AudioSource>();
        audioSource.spatialBlend = 1.0f; //force 2D sound
        initialPitch = 0.18f + ID / 100;
        finalPitch = 0.3f + ID / 100;
        incrementSkipFrames = 3;
        _sC = GameObject.Find("MOTHERSHIP").GetComponent<ShipCore>();
        _cS = GetComponent<ChipSynth>();
        _sC.SetWeaponsID();
        _cS.frequency1 = _cS.frequency1 + ID * 2;
        _cS.frequency2 = _cS.frequency2 + ID * 2;
        FindProjectileSpawnPoint();
        turretJoystick = GameObject
            .FindGameObjectWithTag("TurretJoystick")
            .GetComponent<FixedJoystick>();
    }

    void FixedUpdate()
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
            target.transform.Find("IndicatorRed").gameObject.SetActive(true);
            target.transform.Find("IndicatorBlue").gameObject.SetActive(false);
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
                    _sC.ActualizeTargets();
                    if (_sC.targetDataArray.Length > 0)
                        PickTarget();
                }
            }
        }
        else
        {
            //audioSource.pitch = 0.02f;
            //audioSource.Play();
            if (_sC.targetDataArray.Length > 0)
                PickTarget();
            //Debug.Log("no fokin target");
        }
        if (
            target != null
            && Vector3.Distance(transform.position, target.transform.position)
                > _sC.sonarDetectionRadius
            && !target.GetComponent<Enemy>().isDestroyed
        )
        {
            //Debug.Log("Target is too far away");
            target.GetComponent<Enemy>().isTargeted = false;
            target.transform.Find("IndicatorRed").gameObject.SetActive(false);
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
            //target = CombinedTargets[0].TargetObject;
            //Debug.Log("am i first? " + CombinedTargets[0].UnifiedDistance);
            indicatorRed.SetActive(false);
            indicatorGreen.SetActive(true);
            indicatorBlue.SetActive(false);
        }

        if (target != prevTarget && target != null && !target.GetComponent<Enemy>().isDestroyed)
        {
            //target = CombinedTargets[0].TargetObject;
            //Debug.Log("now my target is fokin unique! " + CombinedTargets[0].UnifiedDistance);
            indicatorRed.SetActive(false);
            indicatorGreen.SetActive(false);
            indicatorBlue.SetActive(true);
        }
    }

    private ShipCore.TargetData[] RePrioritizeTarget()
    {
        if (CombinedTargets.Length > 0)
        {
            CombinedTargets = _sC.ArrangeTargets(CombinedTargets);
            if (CombinedTargets.Length > 0)
            {
                target = CombinedTargets[0].TargetObject;
                //Debug.Log("SC reduced to: " + CombinedTargets.Length);
                //Debug.Log("target ACCEPTED! " + CombinedTargets[0].UnifiedDistance);
                return CombinedTargets;
            }
        }
        return null;
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

    private ShipCore.TargetData[] FindClosest()
    {
        int length = Mathf.Min(_sC.Weapons.Length, _sC.targetDataArray.Length);
        ShipCore.TargetData[] closestTargetsArray = new ShipCore.TargetData[length];

        if (_sC.targetDataArray.Length > 0)
        {
            for (int i = 0; i < length; i++)
            {
                GameObject enemy = _sC.targetDataArray[i].GameObject; // Use index i to get the current enemy
                float distance = Vector3.Distance(transform.position, enemy.transform.position);

                closestTargetsArray[i] = new ShipCore.TargetData(
                    enemy,
                    distance * _sC.distanceThreshold
                );
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
                GameObject enemy = _sC.targetDataArray[i].GameObject;
                Vector3 directionToEnemy = enemy.transform.position - transform.position;
                directionToEnemy.y = 0f;
                Vector3 turretForward = transform.forward;
                float angularDistance = Vector3.Angle(turretForward, directionToEnemy);

                closestTargetsArray[i] = new ShipCore.TargetData(
                    enemy,
                    angularDistance * _sC.angularThreshold + _sC.angularMarging
                );
                //Debug.Log($"ATA {i}: {closestTargetsArray[i].UnifiedDistance}");
            }
            //Debug.Log("Colliders Length 10: " + _sC.targetDataArray.Length);
        }
        //Debug.Log("LOoooooooooL");
        //Debug.Log("ATA: " + closestTargetsArray.Length);
        //Debug.Log($"ATA {0}: {closestTargetsArray[0].UnifiedDistance}");
        //Debug.Log("Colliders Length 11: " + _sC.targetDataArray.Length);
        /*for (int i = 0; i < closestTargetsArray.Length; i++)
        {
            Debug.Log($"ATA {i}: {closestTargetsArray[i].UnifiedDistance}");
        }*/
        //Debug.Log("GOT angular closes targets: " + closestTargetsArray.Length);
        return closestTargetsArray;
    }

    // Method to rotate towards a target position
    private void RotateTowardsTarget(Vector3 targetPosition)
    {
        audioSource.Play();

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
                arpeggioRange = finalPitch - audioSource.pitch;
                // Calculate the angular distance in degrees
                angularDistance = Quaternion.Angle(transform.rotation, targetRotation);

                // Calculate the pitch increment
                pitchIncrement = (arpeggioRange / angularDistance) * rotationSpeed;

                audioSource.pitch = audioSource.pitch + pitchIncrement;
            }
        }
        if (angularDistance < _sC.firingThresholdAngle)
        {
            audioSource.pitch = initialPitch;
            audioSource.Stop();
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
        fireRate = initialFireRate * weaponsDistributedEnergy * 0.01f + 2f;
    }

    void Fire()
    {
        // Instantiate the projectile at the spawn point
        GameObject newProjectile = Instantiate(
            projectilePrefab,
            projectileSpawnPoint.position,
            projectileSpawnPoint.rotation
        );

        // Instantiate the ProjectileSoundHolder as a child of the turret
        GameObject soundHolder = Instantiate(
            projectileSoundHolderPrefab,
            transform.position,
            Quaternion.identity,
            transform
        );

        // Get the AudioSource component from the ProjectileSoundHolder
        AudioSource soundHolderAudioSource = soundHolder.GetComponent<AudioSource>();

        // Play the firing sound on ProjectileSoundHolder using PlayScheduled
        if (soundHolderAudioSource != null)
        {
            double playTime = AudioSettings.dspTime + 0.1; // Adjust the initial play time
            soundHolderAudioSource.PlayScheduled(playTime);
        }

        // Update the nextFireTime for the firing rate control
        nextFireTime = Time.fixedTime + 1.0f / fireRate;

        // Set a time delay for destroying the projectile and sound holder
        float projectileLifetime = 3.0f; // Adjust as needed
        Destroy(newProjectile, projectileLifetime);
        Destroy(soundHolder, projectileLifetime);
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

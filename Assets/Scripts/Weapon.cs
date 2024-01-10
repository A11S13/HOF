using UnityEngine;

public class Weapon : MonoBehaviour
{
    ShipCore _sC;

    public bool isAI;
    public float localRotationDeadZone = 0.1f;
    public float rotationSpeed = 5f;

    // Private variables to store references
    private FixedJoystick turretJoystick;
    private Transform projectileSpawnPoint;

    // Adjustable parameters for firing rate
    public float initialFireRate = 40f;

    [SerializeField]
    private float fireRate; // Adjust as needed
    private float nextFireTime;

    // Projectile prefab reference
    public GameObject projectilePrefab;
    public GameObject projectileSoundHolderPrefab;

    void Start()
    {
        _sC = GameObject.Find("MOTHERSHIP").GetComponent<ShipCore>();
        FindProjectileSpawnPoint();
        turretJoystick = GameObject
            .FindGameObjectWithTag("TurretJoystick")
            .GetComponent<FixedJoystick>();
    }

    void Update() { }

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
        // 1. Pick Target
        GameObject target = PrioritizeTarget();

        if (target != null)
        {
            // 2. Rotate Towards Target
            RotateTowardsTarget(target.transform.position);

            // 3. Fire if Aimed
            if (IsAimedAtTarget(target.transform.position))
            {
                if (CanFire())
                {
                    Fire();
                }
            }
        }
    }

    private GameObject PrioritizeTarget()
    {
        var (closestEnemy, closestDistance) = FindClosest();
        var (angularClosestEnemy, angularClosestDistance) = FindAngularClosest();

        if (!(closestEnemy == null && angularClosestEnemy == null))
        {
            closestDistance = closestDistance * _sC.distanceThreshold;
            angularClosestDistance = angularClosestDistance * _sC.angularThreshold;

            // Prioritize target based on both distance and angular distance
            if (angularClosestDistance < closestDistance)
            {
                return angularClosestEnemy;
            }
            else
            {
                return closestEnemy;
            }
        }

        return null;
    }

    private (GameObject, float) FindAngularClosest()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");

        if (enemies.Length > 0)
        {
            GameObject closestEnemy = null;
            float closestAngularDistance = float.MaxValue;

            foreach (GameObject enemy in enemies)
            {
                Vector3 directionToEnemy = enemy.transform.position - transform.position;
                directionToEnemy.y = 0f;

                Vector3 turretForward = transform.forward;

                float angularDistance = Vector3.Angle(turretForward, directionToEnemy);

                if (angularDistance < closestAngularDistance)
                {
                    closestAngularDistance = angularDistance;
                    closestEnemy = enemy;
                }
            }

            return (closestEnemy, closestAngularDistance);
        }

        return (null, float.MaxValue); // Return a large angular distance if no enemy found
    }

    private (GameObject, float) FindClosest()
    {
        // Implement your logic to find a target here, for example, find the closest enemy
        // You can use tags or layers to identify enemy game objects
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");

        if (enemies.Length > 0)
        {
            GameObject closestEnemy = enemies[0];
            float closestDistance = Vector3.Distance(
                transform.position,
                closestEnemy.transform.position
            );

            foreach (GameObject enemy in enemies)
            {
                float distance = Vector3.Distance(transform.position, enemy.transform.position);

                if (distance < closestDistance)
                {
                    closestEnemy = enemy;
                    closestDistance = distance;
                }
            }

            return (closestEnemy, closestDistance);
        }

        return (null, float.MaxValue); // Return a large distance if no enemy found
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
        }
    }

    private bool IsAimedAtTarget(Vector3 targetPosition)
    {
        Vector3 directionToTarget = targetPosition - transform.position;
        directionToTarget.y = 0f; // Keep the check only in the horizontal plane

        Vector3 turretForward = transform.forward;

        float angleToTarget = Vector3.Angle(turretForward, directionToTarget);

        // Set your firing threshold angle (adjust as needed)
        float firingThresholdAngle = 5f;

        return angleToTarget <= firingThresholdAngle;
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

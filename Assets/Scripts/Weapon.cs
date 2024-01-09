using UnityEngine;

public class Weapon : MonoBehaviour
{
    // Public variables for rotation settings

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
        //
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

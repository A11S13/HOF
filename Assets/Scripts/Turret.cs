using UnityEngine;

public class Turret : MonoBehaviour
{
    // Public variables for rotation settings
    public float rotationDeadZone = 0.1f;
    public float rotationSpeed = 5f;

    // Private variables to store references
    private FixedJoystick turretJoystick;
    private Transform projectileSpawnPoint;

    // Adjustable parameters for firing rate
    public float fireRate = 1.0f;  // Adjust as needed
    private float nextFireTime;

    // Projectile prefab reference
    public GameObject projectilePrefab;
    public GameObject projectileSoundHolderPrefab;

    void Start()
    {
        FindProjectileSpawnPoint();

        // Find the FixedJoystick using the tag
        GameObject joystickObject = GameObject.FindGameObjectWithTag("TurretJoystick");

        // Check if the joystickObject is found and has the FixedJoystick component
        if (joystickObject != null)
        {
            turretJoystick = joystickObject.GetComponent<FixedJoystick>();

            if (turretJoystick == null)
            {
                Debug.LogError("FixedJoystick component not found on the GameObject with tag 'TurretJoystick'");
            }
        }
        else
        {
            Debug.LogError("GameObject with tag 'TurretJoystick' not found!");
        }
    }

    void Update()
    {
        HandleTurretRotation();
        
        // Add other turret-related functionality here if needed
    }

    void FixedUpdate()
    {
        HandleFire();
    }

    public void UpdateFirePower(float weaponsDistributedEnergy)
    {
        fireRate = weaponsDistributedEnergy * 0.1f;
    }

    void FindProjectileSpawnPoint()
    {
        // Find the projectileSpawnPoint as a child of this tower
        projectileSpawnPoint = transform.Find("projectileSpawnPoint");

        // Check if the spawnPointObject is found
        if (projectileSpawnPoint == null)
        {
            Debug.LogError("ProjectileSpawnPoint not found as a child of the turret. Make sure to set up the hierarchy correctly.");
        }
    }

    void HandleTurretRotation()
{
    Vector3 turretDirection = new Vector3(turretJoystick.Direction.x, 0, turretJoystick.Direction.y);

    // Check if the turret joystick is touched outside the dead zone
    if (turretDirection.magnitude > turretJoystick.DeadZone)
    {
        // Check if the joystick is touched outside the dead zone for rotation
        if (turretDirection.magnitude > rotationDeadZone)
        {
            float angle = Vector3.SignedAngle(Vector3.forward, turretDirection, Vector3.up);
            Quaternion targetRotation = Quaternion.Euler(0f, angle, 0f);

            // Smoothly rotate the turret
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }
    }
}

    void HandleFire()
    {
        // Assuming FixedJoystick is a Unity Input System Joystick
        Vector3 fireDirection = new Vector3(turretJoystick.Direction.x, 0, turretJoystick.Direction.y);

        // Check if the fire joystick is touched within the dead zone
        if (fireDirection.magnitude > turretJoystick.DeadZone)
        {
            // Fire only if the firing conditions are met
            if (CanFire())
            {
                Fire();
            }
        }
    }

    void Fire()
    {
        // Check if enough time has passed since the last firing
        if (CanFire())
        {
            // Instantiate the projectile at the spawn point
            GameObject newProjectile = Instantiate(projectilePrefab, projectileSpawnPoint.position, projectileSpawnPoint.rotation);

            // Instantiate the ProjectileSoundHolder as a child of the turret
            GameObject soundHolder = Instantiate(projectileSoundHolderPrefab, transform.position, Quaternion.identity, transform);

            // Get the AudioSource component from the ProjectileSoundHolder
            AudioSource soundHolderAudioSource = soundHolder.GetComponent<AudioSource>();

            // Play the firing sound on ProjectileSoundHolder using PlayScheduled
            if (soundHolderAudioSource != null)
            {
                double playTime = AudioSettings.dspTime + 0.1; // Adjust the initial play time
                soundHolderAudioSource.PlayScheduled(playTime);
            }

            // Add any additional logic to the projectile (e.g., applying force or damage)
            // ...

            // Update the nextFireTime for the firing rate control
            nextFireTime = Time.time + 1.0f / fireRate;

            // Set a time delay for destroying the projectile and sound holder
            float projectileLifetime = 3.0f; // Adjust as needed
            Destroy(newProjectile, projectileLifetime);
            Destroy(soundHolder, projectileLifetime);
        }
    }

    bool CanFire()
    {
        // Check if enough time has passed since the last firing, adjusted by Time.deltaTime
        return Time.time >= nextFireTime && Time.deltaTime > 0;
    }
}

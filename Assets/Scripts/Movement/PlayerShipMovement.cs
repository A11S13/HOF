using UnityEngine;

/// <summary>
/// Player-controlled ship movement using joystick input.
/// Maintains the classic MovementControl feeling with energy distribution.
/// </summary>
public class PlayerShipMovement : ShipMovementBase
{
    [Header("Player Input")]
    public Joystick joystick;
    public Camera attachedCamera;

    [Header("Dead Zones")]
    public float rotationDeadZone = 1f;
    public float throttleDeadZone = 10f;

    [Header("Energy Configuration")]
    [Tooltip("Total energy pool available (should match shipData.overallEnergy)")]
    public float totalEnergyPool = 400f;

    private Vector3 targetVelocity;
    private bool isJoystickInUse;
    private Quaternion targetRotation = Quaternion.identity;
    private Vector3 baseVelocity;

    // Store the base max speed from inspector/base class
    private float baseMaxSpeed;

    // Effective max speed after energy scaling
    private float effectiveMaxSpeed;

    // NEW: Store the speed percentage when joystick was released (0-1)
    private float releaseSpeedPercent = 1f;

    void Start()
    {
        cruiseControlLevel = 1f;
        releaseSpeedPercent = 1f;

        // Store the original max speed as baseline
        baseMaxSpeed = maxSpeed;
        effectiveMaxSpeed = maxSpeed;
    }

    void FixedUpdate()
    {
        UpdateMovement();
    }

    void Update()
    {
        FollowPlayerWithCamera();
    }

    public override void UpdateMovement()
    {
        HandleThrottle();
        HandleRotation();
        ApplyEngineSpeedModification();
    }

    /// <summary>
    /// Called by ShipCore when engine energy distribution changes.
    /// This replicates the old UpdateMaxSpeed() behavior with full-range scaling.
    /// </summary>
    /// <param name="distributedEnergy">Amount of energy allocated to engines (0-400)</param>
    public override void SetMaxSpeed(float distributedEnergy)
    {
        // Normalize to 0-1 range
        float normalizedSliderValue = Mathf.Clamp01(distributedEnergy / totalEnergyPool);

        // Apply 5% dead zone like the original
        if (normalizedSliderValue < 0.05f)
        {
            cruiseControlLevel = 0f;
            acceleration = 0f;
            effectiveMaxSpeed = 0f;
        }
        else
        {
            // Scale from 0.05-1.0 range to 0-1 range for full gradient
            cruiseControlLevel = (normalizedSliderValue - 0.05f) / 0.95f;

            // Scale acceleration across full range
            acceleration = defaultAcceleration * cruiseControlLevel;

            // Scale max speed across full range
            effectiveMaxSpeed = baseMaxSpeed * cruiseControlLevel;
        }

        // If ship is in cruise control and moving, update velocity immediately
        if (!isJoystickInUse && baseVelocity.magnitude > 0.01f)
        {
            ApplyEngineSpeedModification();
        }

        Debug.Log($"{gameObject.name} - Cruise: {cruiseControlLevel:F2}, Accel: {acceleration:F2}, MaxSpeed: {effectiveMaxSpeed:F2}");
    }

    void HandleThrottle()
    {
        float rawThrottle = joystick.Direction.magnitude;

        if (rawThrottle > joystick.DeadZone * throttleDeadZone)
        {
            isJoystickInUse = true;

            // Scale throttle based on dead zone
            float throttle = Mathf.Clamp01((rawThrottle - joystick.DeadZone * throttleDeadZone) / (1 - joystick.DeadZone * throttleDeadZone));

            // Calculate target velocity using EFFECTIVE max speed (scaled by energy)
            targetVelocity = transform.forward * throttle * effectiveMaxSpeed;

            // Apply force using base class method
            ApplyForce(targetVelocity);
        }
        else if (isJoystickInUse)
        {
            // Just released joystick - store current velocity
            isJoystickInUse = false;
            baseVelocity = rb.linearVelocity;

            // Store the speed percentage at release (0-1) - this stays constant until joystick is used again
            if (effectiveMaxSpeed > 0.01f)
            {
                releaseSpeedPercent = Mathf.Clamp01(rb.linearVelocity.magnitude / effectiveMaxSpeed);
            }
        }
        else
        {
            targetVelocity = rb.linearVelocity;
        }
    }

    void ApplyEngineSpeedModification()
    {
        // Apply cruise control when not using joystick
        if (!isJoystickInUse && baseVelocity.magnitude > 0.01f && effectiveMaxSpeed > 0.01f)
        {
            // Use the stored release speed percentage (doesn't change with slider)
            // Multiply by cruiseControlLevel (energy scaling from slider)
            float targetSpeed = effectiveMaxSpeed * releaseSpeedPercent;

            // Keep direction from base velocity
            Vector3 direction = baseVelocity.normalized;
            Vector3 targetVel = direction * targetSpeed;

            // Safety check for NaN
            if (!float.IsNaN(targetVel.x) && !float.IsNaN(targetVel.y) && !float.IsNaN(targetVel.z))
            {
                // Smooth transition
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVel, Time.fixedDeltaTime * 10f);
            }
        }
    }

    void HandleRotation()
    {
        Vector3 joystickDirection = new Vector3(joystick.Direction.x, 0, joystick.Direction.y);

        if (joystickDirection.magnitude > joystick.DeadZone * rotationDeadZone)
        {
            float angle = Vector3.SignedAngle(Vector3.forward, joystickDirection, Vector3.up);
            targetRotation = Quaternion.Euler(0f, angle, 0f);
        }

        RotateTowards(targetRotation);
    }

    void FollowPlayerWithCamera()
    {
        if (attachedCamera != null)
        {
            Vector3 cameraPosition = new Vector3(transform.position.x, attachedCamera.transform.position.y, transform.position.z);
            attachedCamera.transform.position = cameraPosition;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Asteroid"))
        {
            rb.AddForce(collision.impulse, ForceMode.Impulse);
        }
    }
}

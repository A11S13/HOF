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
    [Tooltip("Throttle dead zone multiplier (relative to joystick.DeadZone). Unchanged.")]
    public float throttleDeadZone = 10f;

    [Tooltip("Separate rotation dead zone on RAW joystick input (0-1, absolute). " +
             "0 = any deflection rotates (spin in place); raise to suppress small noise.")]
    [Range(0f, 1f)]
    public float rotationDeadZone = 0f;

    [Header("Energy Configuration")]
    [Tooltip("Total energy pool available (should match shipData.overallEnergy)")]
    public float totalEnergyPool = 400f;

    [Header("Debug")]
    [Tooltip("Log raw joystick values each FixedUpdate while handling rotation.")]
    [SerializeField] private bool logRotationInput = false;

    private Vector3 targetVelocity;
    private bool isJoystickInUse;
    private Quaternion targetRotation = Quaternion.identity;
    private Vector3 baseVelocity;

    private float baseMaxSpeed;
    private float effectiveMaxSpeed;
    private float releaseSpeedPercent = 1f;

    void Start()
    {
        cruiseControlLevel = 1f;
        releaseSpeedPercent = 1f;
        baseMaxSpeed = maxSpeed;
        effectiveMaxSpeed = maxSpeed;

        if (joystick == null)
            Debug.LogError("[PlayerShipMovement] Joystick not assigned.");
        else if (!(joystick is DynamicJoystick) && rotationDeadZone < joystick.DeadZone)
            Debug.LogWarning("[PlayerShipMovement] rotationDeadZone is below joystick.DeadZone, but joystick is not a DynamicJoystick — spin-in-place inside the dead zone is not possible. Assign a DynamicJoystick.");
    }

    void FixedUpdate() => UpdateMovement();
    void Update() => FollowPlayerWithCamera();

    public override void UpdateMovement()
    {
        HandleThrottle();
        HandleRotation();
        ApplyEngineSpeedModification();
    }

    public override void SetMaxSpeed(float distributedEnergy)
    {
        float normalizedSliderValue = Mathf.Clamp01(distributedEnergy / totalEnergyPool);

        if (normalizedSliderValue < 0.05f)
        {
            cruiseControlLevel = 0f;
            acceleration = 0f;
            effectiveMaxSpeed = 0f;
        }
        else
        {
            cruiseControlLevel = (normalizedSliderValue - 0.05f) / 0.95f;
            acceleration = defaultAcceleration * cruiseControlLevel;
            effectiveMaxSpeed = baseMaxSpeed * cruiseControlLevel;
        }

        if (!isJoystickInUse && baseVelocity.magnitude > 0.01f)
            ApplyEngineSpeedModification();
    }

    Vector2 GetRawJoystickInput()
    {
        if (joystick is DynamicJoystick dj)
            return dj.RawDirection;
        return joystick != null ? joystick.Direction : Vector2.zero;
    }

    void HandleThrottle()
    {
        // Throttle uses Direction (respects joystick.DeadZone). Unchanged from before.
        float rawThrottle = joystick.Direction.magnitude;

        if (rawThrottle > joystick.DeadZone * throttleDeadZone)
        {
            isJoystickInUse = true;

            float throttle = Mathf.Clamp01(
                (rawThrottle - joystick.DeadZone * throttleDeadZone) /
                (1f - joystick.DeadZone * throttleDeadZone));

            targetVelocity = transform.forward * throttle * effectiveMaxSpeed;
            ApplyForce(targetVelocity);
        }
        else if (isJoystickInUse)
        {
            isJoystickInUse = false;
            baseVelocity = rb.linearVelocity;

            if (effectiveMaxSpeed > 0.01f)
                releaseSpeedPercent = Mathf.Clamp01(rb.linearVelocity.magnitude / effectiveMaxSpeed);
        }
        else
        {
            targetVelocity = rb.linearVelocity;
        }
    }

    void ApplyEngineSpeedModification()
    {
        if (!isJoystickInUse && baseVelocity.magnitude > 0.01f && effectiveMaxSpeed > 0.01f)
        {
            float targetSpeed = effectiveMaxSpeed * releaseSpeedPercent;
            Vector3 direction = baseVelocity.normalized;
            Vector3 targetVel = direction * targetSpeed;

            if (!float.IsNaN(targetVel.x) && !float.IsNaN(targetVel.y) && !float.IsNaN(targetVel.z))
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVel, Time.fixedDeltaTime * 10f);
        }
    }

    void HandleRotation()
    {
        Vector2 raw = GetRawJoystickInput();
        Vector3 joystickDirection = new Vector3(raw.x, 0f, raw.y);

        if (logRotationInput)
            Debug.Log($"[Rotation] RawDir={raw} mag={joystickDirection.magnitude:F3} " +
                      $"threshold={rotationDeadZone:F3} Direction={joystick.Direction} " +
                      $"type={joystick.GetType().Name}");

        if (joystickDirection.magnitude > rotationDeadZone)
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
            Vector3 cameraPosition = new Vector3(
                transform.position.x, attachedCamera.transform.position.y, transform.position.z);
            attachedCamera.transform.position = cameraPosition;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Asteroid"))
            rb.AddForce(collision.impulse, ForceMode.Impulse);
    }
}
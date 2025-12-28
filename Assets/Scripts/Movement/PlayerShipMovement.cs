using UnityEngine;

/// <summary>
/// Player-controlled ship movement using joystick input.
/// This replaces MovementControl for player ships.
/// </summary>
public class PlayerShipMovement : ShipMovementBase
{
    [Header("Player Input")]
    public Joystick joystick;
    public Camera attachedCamera;

    [Header("Dead Zones")]
    public float rotationDeadZone = 1f;
    public float throttleDeadZone = 10f;

    private Vector3 targetVelocity;
    private bool isJoystickInUse;
    private Quaternion targetRotation = Quaternion.identity;
    private Vector3 baseVelocity;

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

    void HandleThrottle()
    {
        float rawThrottle = joystick.Direction.magnitude;

        if (rawThrottle > joystick.DeadZone * throttleDeadZone)
        {
            isJoystickInUse = true;

            float throttle = Mathf.Clamp01((rawThrottle - joystick.DeadZone * throttleDeadZone) / (1 - joystick.DeadZone * throttleDeadZone));
            targetVelocity = transform.forward * throttle * maxSpeed;

            ApplyForce(targetVelocity);
        }
        else if (isJoystickInUse)
        {
            isJoystickInUse = false;
            baseVelocity = rb.linearVelocity;

            float currentSpeedPercent = rb.linearVelocity.magnitude / maxSpeed;
            cruiseControlLevel = Mathf.Clamp01(currentSpeedPercent);
        }
        else
        {
            targetVelocity = rb.linearVelocity;
        }
    }

    void ApplyEngineSpeedModification()
    {
        if (!isJoystickInUse && baseVelocity.magnitude > 0.01f)
        {
            float baseSpeedPercent = baseVelocity.magnitude / maxSpeed;
            float targetSpeedPercent = baseSpeedPercent * cruiseControlLevel;
            float targetSpeed = maxSpeed * targetSpeedPercent;

            Vector3 direction = baseVelocity.normalized;
            Vector3 targetVel = direction * targetSpeed;

            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVel, Time.fixedDeltaTime * 10f);
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

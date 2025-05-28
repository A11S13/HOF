using UnityEngine;

public class MovementControl : MonoBehaviour
{
    // Attached camera for following the player
    public Camera _attachedCamera;

    private Rigidbody rb;

    // Movement parameters
    public float maxSpeed = 10f;
    public float acceleration = 5f;
    public float deceleration = 0.1f;
    public float defaultAcceleration = 5f;
    public float rotationSpeed = 5f;
    public float rotationDeadZone = 1f;
    public float throttleDeadZone = 10f;

    // Joystick for player input
    public Joystick joystick;

    // Variables for handling movement and rotation
    private Vector3 targetVelocity;
    private bool isJoystickInUse;
    private Quaternion targetRotation = Quaternion.identity;

    // Cruise control from slider (0-1 range)
    private float cruiseControlLevel = 1f;

    // Store the base velocity when joystick is released
    private Vector3 baseVelocity;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (rb == null)
        {
            Debug.LogError("Rigidbody component not found. Make sure to attach a Rigidbody to the same GameObject as the MovementControl script.");
        }
    }

    void FixedUpdate()
    {
        HandleThrottle();
        HandleRotation();
        ApplyEngineSpeedModification();
    }

    void Update()
    {
        FollowPlayer();
    }

    public void UpdateMaxSpeed(float enginesDistributedEnergy)
    {
        float normalizedSliderValue = Mathf.Clamp01(enginesDistributedEnergy / 400f);

        if (normalizedSliderValue < 0.05f)
        {
            cruiseControlLevel = 0f;
            acceleration = 0f;
        }
        else
        {
            cruiseControlLevel = (normalizedSliderValue - 0.05f) / 0.95f;
            acceleration = defaultAcceleration * cruiseControlLevel;
        }
    }

    void HandleThrottle()
    {
        float rawThrottle = joystick.Direction.magnitude;

        if (rawThrottle > joystick.DeadZone * throttleDeadZone)
        {
            isJoystickInUse = true;

            // Scale the rawThrottle value to be within the range [0, 1] based on the dead zone
            float throttle = Mathf.Clamp01((rawThrottle - joystick.DeadZone * throttleDeadZone) / (1 - joystick.DeadZone * throttleDeadZone));

            // Calculate the desired velocity based on input
            targetVelocity = transform.forward * throttle * maxSpeed;

            // Apply force to the Rigidbody to reach the target velocity
            Vector3 velocityChange = targetVelocity - rb.linearVelocity;
            Vector3 force = velocityChange * acceleration;
            rb.AddForce(force, ForceMode.Acceleration);
        }
        else if (isJoystickInUse)
        {
            // Just released joystick - store the current velocity as base velocity
            isJoystickInUse = false;
            baseVelocity = rb.linearVelocity;

            // Store the current speed as a percentage of maxSpeed for proper scaling
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
        // Apply cruise control when not actively using joystick
        if (!isJoystickInUse && baseVelocity.magnitude > 0.01f)
        {
            // Calculate what percentage of maxSpeed the baseVelocity was when joystick was released
            float baseSpeedPercent = baseVelocity.magnitude / maxSpeed;

            // Scale this base speed percentage by the current cruise control level
            // This way, slider acts as a multiplier of the speed you had when you released joystick
            float targetSpeedPercent = baseSpeedPercent * cruiseControlLevel;
            float targetSpeed = maxSpeed * targetSpeedPercent;

            // Keep the direction from base velocity
            Vector3 direction = baseVelocity.normalized;
            Vector3 targetVelocity = direction * targetSpeed;

            // Directly set the velocity (or use smooth transition if preferred)
            //rb.linearVelocity = targetVelocity;

            // Alternative smooth transition (uncomment if you prefer gradual changes):
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVelocity, Time.fixedDeltaTime * 10f);
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

        rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime));
    }

    void FollowPlayer()
    {
        if (_attachedCamera != null)
        {
            Vector3 cameraPosition = new Vector3(transform.position.x, _attachedCamera.transform.position.y, transform.position.z);
            _attachedCamera.transform.position = cameraPosition;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.tag == "Asteroid")
        {
            Vector3 collisionImpulse = collision.impulse;
            rb.AddForce(collisionImpulse, ForceMode.Impulse);
        }
    }
}
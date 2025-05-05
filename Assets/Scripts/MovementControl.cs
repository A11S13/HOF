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
    private Quaternion targetRotation = Quaternion.identity; // Initialize to identity

    void Start()
    {
        // Get the Rigidbody component attached to the same GameObject
        rb = GetComponent<Rigidbody>();

        // Check if the Rigidbody component is found
        if (rb == null)
        {
            Debug.LogError("Rigidbody component not found. Make sure to attach a Rigidbody to the same GameObject as the MovementControl script.");
        }
    }

    void FixedUpdate()
    {
        // Handle player input and movement using physics
        HandleThrottle();
        HandleRotation();
    }

    void Update()
    {
        FollowPlayer();
    }

    public void UpdateMaxSpeed(float enginesDistributedEnergy)
    {
        // Update acceleration based on the engine slider value
        acceleration = defaultAcceleration * enginesDistributedEnergy / 100;
    }

    void HandleThrottle()
    {
        // Calculate throttle from joystick direction magnitude
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
            rb.AddForce(force, ForceMode.Acceleration); // Using Acceleration mode for smoother control
        }
        else if (isJoystickInUse)
        {
            // If joystick was in use but released, do NOT set targetVelocity to zero.
            //  Instead, maintain the last targetVelocity, so the ship continues with its current momentum.
            isJoystickInUse = false;
        }
        else
        {
            //When joystick is not in use, set targetVelocity to current velocity, to stop acceleration.
            targetVelocity = rb.linearVelocity;
        }
    }

    void HandleRotation()
    {
        // Use the joystick's direction to calculate the angle in screen space
        Vector3 joystickDirection = new Vector3(joystick.Direction.x, 0, joystick.Direction.y);

        if (joystickDirection.magnitude > joystick.DeadZone * rotationDeadZone)
        {
            float angle = Vector3.SignedAngle(Vector3.forward, joystickDirection, Vector3.up);

            // Calculate the target rotation based on the angle
            targetRotation = Quaternion.Euler(0f, angle, 0f);
        }

        // Apply rotation using Slerp for smooth rotation
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

    // This function will be called automatically by Unity when a collision occurs.
    void OnCollisionEnter(Collision collision)
    {
        // Check if the collision involves an asteroid (you might need to tag your asteroids)
        if (collision.gameObject.tag == "Asteroid")
        {
            // Get the collision impulse (the change in momentum).
            Vector3 collisionImpulse = collision.impulse;

            // Apply the impulse to the player's Rigidbody.
            rb.AddForce(collisionImpulse, ForceMode.Impulse);
        }
    }
}

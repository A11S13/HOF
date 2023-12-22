// MovementControl.cs
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
    private Vector3 currentThrottleVelocity;
    private bool isJoystickInUse;
    private Vector3 lastThrottleVector;
    private Vector3 shipVelocity;
    private Quaternion targetRotation;
    private Vector3 impulseToApply;

    void Start()
    {
        // Get the Rigidbody component attached to the same GameObject
        rb = GetComponent<Rigidbody>();

        // Check if the Rigidbody component is found
        if (rb == null)
        {
            Debug.LogError("Rigidbody component not found. Make sure to attach a Rigidbody to the same GameObject as the MovementControl script.");
        }

        // Your existing Start method code...
    }

    void Update()
    {
        // Handle player input and movement
        HandleThrottle();
        HandleRotation();
        MovePlayer();
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

            // Update lastThrottleVector based on the rawThrottle value
            lastThrottleVector = transform.forward * throttle;

            // Gradually change the shipVelocity towards the target velocity
            shipVelocity = Vector3.SmoothDamp(shipVelocity, lastThrottleVector * maxSpeed, ref currentThrottleVelocity, deceleration);
            
        }
        else if (isJoystickInUse)
        {
            // If joystick was in use but released, smoothly decelerate the ship
            shipVelocity = Vector3.SmoothDamp(shipVelocity, lastThrottleVector * maxSpeed, ref currentThrottleVelocity, deceleration);
            isJoystickInUse = false;
        }
    }

    void MovePlayer()
    {
        // Update the position based on the ship's velocity
        transform.position += shipVelocity * Time.deltaTime * acceleration;
    }

    void HandleRotation()
    {
        // Use the joystick's direction to calculate the angle in screen space
        Vector3 joystickDirection = new Vector3(joystick.Direction.x, 0, joystick.Direction.y);

        if (joystickDirection.magnitude > joystick.DeadZone * rotationDeadZone)
        {
            float angle = Vector3.SignedAngle(Vector3.forward, joystickDirection, Vector3.up);

            // Calculate the rotation based on the angle
            targetRotation = Quaternion.Euler(0f, angle, 0f);
        }
    }

    void FollowPlayer()
    {
        if (_attachedCamera != null)
        {
            // Placeholder for camera follow logic
            // This method could be extended to smoothly follow the player's position
            Vector3 cameraPosition = new Vector3(transform.position.x, _attachedCamera.transform.position.y, transform.position.z);
            _attachedCamera.transform.position = cameraPosition;
        }
    }
public void ApplyImpulse(Vector3 impulseForce)
    {
        // Queue the impulse to be applied in LateUpdate
        impulseToApply += impulseForce;
    }

    private void ApplyQueuedImpulse()
    {
        // Apply the queued impulse
        shipVelocity += impulseToApply;
        impulseToApply = Vector3.zero; // Reset the queued impulse
    }


    void LateUpdate()
    {
        // Apply rotation using Slerp for smooth rotation
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
         ApplyQueuedImpulse();
        
    }
}

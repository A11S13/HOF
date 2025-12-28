using UnityEngine;

/// <summary>
/// Base movement system for all ships.
/// Inherit from this for specific movement types.
/// </summary>
public abstract class ShipMovementBase : MonoBehaviour, IShipController
{
    protected Rigidbody rb;

    [Header("Movement Parameters")]
    public float maxSpeed = 10f;
    public float acceleration = 5f;
    public float defaultAcceleration = 5f;
    public float rotationSpeed = 5f;

    protected float cruiseControlLevel = 1f;

    public Rigidbody ShipRigidbody => rb;
    public Transform ShipTransform => transform;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError($"Rigidbody not found on {gameObject.name}");
        }
    }

    public virtual void SetMaxSpeed(float enginesDistributedEnergy)
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

    public abstract void UpdateMovement();

    protected void ApplyForce(Vector3 targetVelocity)
    {
        Vector3 velocityChange = targetVelocity - rb.linearVelocity;
        Vector3 force = velocityChange * acceleration;
        rb.AddForce(force, ForceMode.Acceleration);
    }

    protected void RotateTowards(Quaternion targetRotation)
    {
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime));
    }
}

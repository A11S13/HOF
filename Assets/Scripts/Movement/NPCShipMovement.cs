using UnityEngine;

/// <summary>
/// AI-controlled ship movement for NPCs with improved pathfinding
/// </summary>
public class NPCShipMovement : ShipMovementBase
{
    [Header("AI Behavior")]
    public float waypointReachDistance = 5f;
    public float obstacleAvoidanceDistance = 10f;
    public float pathUpdateRate = 0.5f;

    [Header("Movement Style")]
    public float dampingFactor = 0.9f; // How quickly to stop when reaching waypoint
    public float turnSmoothness = 2f; // How smoothly to turn

    private Vector3 currentWaypoint;
    private bool hasWaypoint = false;
    private float lastPathUpdate = 0f;

    void FixedUpdate()
    {
        UpdateMovement();
    }

    public override void UpdateMovement()
    {
        if (!hasWaypoint)
        {
            // Idle behavior - maintain current velocity with deceleration
            if (rb.linearVelocity.magnitude > 0.1f)
            {
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, Time.fixedDeltaTime * dampingFactor);
            }
            return;
        }

        MoveTowardsWaypoint();
    }

    public void SetWaypoint(Vector3 position)
    {
        currentWaypoint = position;
        hasWaypoint = true;
    }

    public void ClearWaypoint()
    {
        hasWaypoint = false;
    }

    void MoveTowardsWaypoint()
    {
        Vector3 direction = (currentWaypoint - rb.position).normalized;
        float distance = Vector3.Distance(rb.position, currentWaypoint);

        // Reached waypoint
        if (distance < waypointReachDistance)
        {
            hasWaypoint = false;
            return;
        }

        // Rotate towards waypoint
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        RotateTowards(targetRotation);

        // Calculate speed based on distance (slow down when close)
        float speedMultiplier = Mathf.Clamp01(distance / (waypointReachDistance * 3f));

        // Move forward
        Vector3 targetVelocity = rb.rotation * Vector3.forward * maxSpeed * speedMultiplier;
        ApplyForce(targetVelocity);
    }

    protected new void RotateTowards(Quaternion targetRotation)
    {
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * turnSmoothness * Time.fixedDeltaTime));
    }

    public Vector3 GetCurrentWaypoint() => currentWaypoint;
    public bool HasWaypoint() => hasWaypoint;
}

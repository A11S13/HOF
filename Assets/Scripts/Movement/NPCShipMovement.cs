using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class NPCShipMovement : ShipMovementBase
{
    [Header("Moving Field Settings")]
    [Tooltip("How far ahead (in seconds) to predict obstacle movement.")]
    public float predictionTime = 1.5f;
    [Tooltip("If the field moves fast, we need a larger safety margin.")]
    public float dynamicClearance = 2.0f;
    [Tooltip("Base distance to look ahead. Increases with speed.")]
    public float baseLookAhead = 60f;

    [Header("Physical Constraints")]
    public float shipCollisionRadius = 5f;
    public bool enableStrafing = true;

    [Header("Combat & Stuck")]
    public ShipTargetingSystem targetingSystem;
    public float stuckThreshold = 0.1f;
    [Tooltip("Force applied when physically touching a rock to push off.")]
    public float contactRepulsionForce = 50f;

    [Header("Sensors")]
    public LayerMask obstacleLayer;
    public int resolution = 72; // High res for fast moving gaps

    // Flow Field
    public NPCFlowFieldNavigator flowFieldNavigator;
    public bool useFlowFieldNavigation = true;

    // Internal
    private Vector3 currentGoal;
    private bool hasGoal = false;
    private WaypointPriority currentPriority = WaypointPriority.None;

    // Smoothing & State
    private Vector3 smoothedDir;
    private Vector3 steerVelocity;
    private float stuckTimer = 0f;
    private bool isMining = false;
    private bool isPhysicallyColliding = false;

    // Arrays
    private float[] interestMap;
    private float[] dangerMap;
    private Vector3[] rayDirections;
    private Collider[] nearbyObstacles = new Collider[60];

    protected override void Awake()
    {
        base.Awake();

        // Auto-size radius
        if (shipCollisionRadius <= 0.1f)
        {
            var col = GetComponent<Collider>();
            if (col != null) shipCollisionRadius = col.bounds.extents.magnitude;
            else shipCollisionRadius = 5f;
        }

        interestMap = new float[resolution];
        dangerMap = new float[resolution];
        rayDirections = new Vector3[resolution];

        // Generate flattened XZ rays
        for (int i = 0; i < resolution; i++)
        {
            float angle = i * 2 * Mathf.PI / resolution;
            rayDirections[i] = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
        }

        smoothedDir = transform.forward;
    }

    void Start()
    {
        if (flowFieldNavigator == null) flowFieldNavigator = GetComponent<NPCFlowFieldNavigator>();

        // If the field moves at 40, we need speed to dodge
        if (maxSpeed < 30) maxSpeed = 45f;
        if (acceleration < 10) acceleration = 20f;
    }

    void FixedUpdate()
    {
        isPhysicallyColliding = false; // Reset flag, OnCollisionStay will set it true if hitting
        UpdateMovement();
    }

    public override void UpdateMovement()
    {
        if (!hasGoal)
        {
            ApplyForce(Vector3.MoveTowards(rb.linearVelocity, Vector3.zero, Time.fixedDeltaTime * 10f));
            return;
        }

        // 1. Calculate Relative Velocity and LookAhead
        // If we are moving 30 and rock is moving 40 towards us, closing speed is 70.
        // We need to look far ahead.
        float currentSpeed = rb.linearVelocity.magnitude;
        float activeLookAhead = baseLookAhead + (currentSpeed * 0.5f);

        // 2. Build Context Maps
        Vector3 globalDir = GetGlobalNavigationDirection();
        globalDir.y = 0; globalDir.Normalize();

        System.Array.Clear(interestMap, 0, resolution);
        System.Array.Clear(dangerMap, 0, resolution);

        // Interest
        for (int i = 0; i < resolution; i++)
        {
            float dot = Vector3.Dot(rayDirections[i], globalDir);
            interestMap[i] = Mathf.Max(0, dot);
        }

        // Danger (Predictive)
        PopulatePredictiveDangerMap(activeLookAhead);

        // 3. Evaluate Best Direction
        Vector3 bestDir = transform.forward;
        float bestScore = -1f;

        for (int i = 0; i < resolution; i++)
        {
            // Hard Veto: If danger > 0.6, the path is DEAD.
            if (dangerMap[i] > 0.6f) interestMap[i] = 0f;
            else interestMap[i] *= (1.0f - dangerMap[i]);

            if (interestMap[i] > bestScore)
            {
                bestScore = interestMap[i];
                bestDir = rayDirections[i];
            }
        }

        // 4. Stuck / Combat Logic
        HandleCombat(bestScore);

        // 5. Final Safety Validation & Physics
        // If we are touching a rock, ignore steering and push away
        if (isPhysicallyColliding)
        {
            // Physics handled in OnCollisionStay, just dampen velocity here
            smoothedDir = transform.forward; // Maintain heading
        }
        else
        {
            // Normal Flight
            float throttle = 1.0f;

            // If the best path is blocked or we are trapped
            if (bestScore < stuckThreshold)
            {
                // We are trapped. Don't stop. Strafing is life.
                // Try to find ANY open direction (lowest danger)
                throttle = 0.5f;
                bestDir = FindEscapeDirection();
            }

            smoothedDir = Vector3.SmoothDamp(smoothedDir, bestDir, ref steerVelocity, 0.25f);
            ApplyXZPhysics(smoothedDir, throttle);
        }
    }

    // --- UPDATED PREDICTIVE AVOIDANCE ---
    void PopulatePredictiveDangerMap(float lookAhead)
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, lookAhead, nearbyObstacles, obstacleLayer);
        int checks = Mathf.Min(count, 60);

        for (int k = 0; k < checks; k++)
        {
            Collider obs = nearbyObstacles[k];
            if (obs == null || obs.transform.root == transform.root) continue;

            Vector3 obsVel = Vector3.zero;
            if (obs.attachedRigidbody != null) obsVel = obs.attachedRigidbody.linearVelocity;

            Vector3 currentPos = obs.bounds.center;
            float radius = obs.bounds.extents.magnitude;

            // If moving, we process the whole "Tube" of time
            if (obsVel.sqrMagnitude > 1.0f)
            {
                Vector3 futurePos = currentPos + (obsVel * predictionTime);
                ProcessCapsuleObstacle(currentPos, futurePos, radius, lookAhead);
            }
            else
            {
                // Static object
                ProcessCapsuleObstacle(currentPos, currentPos, radius, lookAhead);
            }
        }
    }

    void ProcessCapsuleObstacle(Vector3 start, Vector3 end, float radius, float lookAhead)
    {
        // 1. Expanded Safety Radius
        float safeRadius = radius + shipCollisionRadius + dynamicClearance;
        float safeRadiusSq = safeRadius * safeRadius;

        // 2. Iterate over all Ray Directions
        for (int i = 0; i < resolution; i++)
        {
            Vector3 rayDir = rayDirections[i];

            // MATH: Ray-Capsule Intersection Estimation
            // We check if the ray passes close to the segment (start->end)

            // Simple approach: Check distance from Segment to Ray
            // Because our rays start at 0,0, we project the segment onto the ray direction.

            // Find the point on the obstacle path (start->end) that is closest to the ship
            float t_seg = DistancePointToSegmentParam(transform.position, start, end);
            Vector3 closestObstaclePoint = Vector3.Lerp(start, end, t_seg);

            Vector3 toObs = closestObstaclePoint - transform.position;
            toObs.y = 0;
            float dist = toObs.magnitude;

            if (dist > lookAhead) continue;

            // Optimization: Skip rays pointing away from this specific point on the path
            if (Vector3.Dot(rayDir, toObs.normalized) < 0.2f) continue;

            // Check angular intersection
            float angleRad = Mathf.Asin(Mathf.Clamp01(safeRadius / dist));
            float angleDeg = angleRad * Mathf.Rad2Deg;

            float angleToRay = Vector3.Angle(rayDir, toObs.normalized);

            if (angleToRay < angleDeg)
            {
                // We are looking at the "tube"
                float weight = 1.0f;
                // Cost increases if the intersection point is close
                weight *= (1f - (dist / lookAhead) * 0.5f);

                if (weight > dangerMap[i]) dangerMap[i] = weight;
            }
        }
    }

    // Helper to find T on segment closest to point P
    float DistancePointToSegmentParam(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float magSq = ab.sqrMagnitude;
        if (magSq < 0.001f) return 0f;
        return Mathf.Clamp01(Vector3.Dot(p - a, ab) / magSq);
    }


    void ProcessObstacle(Vector3 pos, float radius, float lookAhead)
    {
        Vector3 toObs = pos - transform.position;
        toObs.y = 0; // Flatten
        float dist = toObs.magnitude;

        if (dist > lookAhead) return;

        // Expanded radius for moving fields
        float safeRadius = radius + shipCollisionRadius + dynamicClearance;

        // If we are inside, Panic
        if (dist < safeRadius)
        {
            for (int i = 0; i < resolution; i++) dangerMap[i] = 1.0f;
            return;
        }

        // Angular calculations
        float angleRad = Mathf.Asin(Mathf.Clamp01(safeRadius / dist));
        float angleDeg = angleRad * Mathf.Rad2Deg;
        Vector3 obsDir = toObs.normalized;

        for (int i = 0; i < resolution; i++)
        {
            // Optimization
            if (Vector3.Dot(rayDirections[i], obsDir) < 0.5f) continue;

            float angleToRay = Vector3.Angle(rayDirections[i], obsDir);
            if (angleToRay < angleDeg)
            {
                // Danger is extremely high for moving obstacles
                float weight = 1.0f;
                // Falloff slightly with distance
                weight *= (1f - (dist / lookAhead) * 0.5f);

                if (weight > dangerMap[i]) dangerMap[i] = weight;
            }
        }
    }

    // --- PHYSICS & COLLISION ---

    void ApplyXZPhysics(Vector3 moveDir, float throttle)
    {
        base.RotateTowards(Quaternion.LookRotation(moveDir));
        Vector3 targetVel = moveDir * (maxSpeed * throttle);

        if (enableStrafing) base.ApplyForce(targetVel);
        else
        {
            float align = Vector3.Dot(transform.forward, moveDir);
            if (align > 0) base.ApplyForce(transform.forward * (maxSpeed * throttle * align));
        }
    }

    // EMERGENCY OVERRIDE: If we physically touch, PUSH AWAY.
    void OnCollisionStay(Collision collision)
    {
        // Check if it's an obstacle
        if (((1 << collision.gameObject.layer) & obstacleLayer) != 0)
        {
            isPhysicallyColliding = true;

            // Calculate push direction (Average of contacts)
            Vector3 pushDir = Vector3.zero;
            foreach (ContactPoint contact in collision.contacts)
            {
                pushDir += contact.normal;
            }
            pushDir.Normalize();
            pushDir.y = 0; // Keep flat

            // Apply immediate impulse to slide off
            // We use AddForce directly on RB for instant reaction, bypassing the smoothing logic
            rb.AddForce(pushDir * contactRepulsionForce, ForceMode.Acceleration);
        }
    }

    // --- UTILS ---

    Vector3 FindEscapeDirection()
    {
        // Find the direction with the absolute lowest danger
        float minDanger = 1.0f;
        Vector3 escapeDir = transform.forward;

        for (int i = 0; i < resolution; i++)
        {
            if (dangerMap[i] < minDanger)
            {
                minDanger = dangerMap[i];
                escapeDir = rayDirections[i];
            }
        }
        return escapeDir;
    }

    Vector3 GetGlobalNavigationDirection()
    {
        // If dangerous, ignore flow field and go straight for goal (or away from danger)
        // Flow fields often fail in moving environments
        if (useFlowFieldNavigation && flowFieldNavigator != null)
        {
            flowFieldNavigator.SetGoal(currentGoal);
            return flowFieldNavigator.GetNavigationDirection(transform.position);
        }
        return (currentGoal - transform.position).normalized;
    }

    void HandleCombat(float bestScore)
    {
        if (targetingSystem == null) return;
        bool stuck = bestScore < stuckThreshold;

        if (stuck)
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer > 0.5f && !isMining)
            {
                isMining = true;
                targetingSystem.SetAsteroidTargeting(true);
            }
        }
        else
        {
            if (isMining)
            {
                stuckTimer -= Time.fixedDeltaTime;
                if (stuckTimer < -1.0f)
                {
                    isMining = false;
                    targetingSystem.SetAsteroidTargeting(false);
                    stuckTimer = 0f;
                }
            }
            else stuckTimer = 0f;
        }
    }

    public void SetWaypoint(Vector3 position, WaypointPriority priority = WaypointPriority.Low)
    {
        if (priority >= currentPriority) { currentGoal = position; hasGoal = true; currentPriority = priority; }
    }

    public void ClearWaypoint(WaypointPriority priority = WaypointPriority.High)
    {
        if (priority >= currentPriority) { hasGoal = false; currentPriority = WaypointPriority.None; }
    }

    void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, transform.position + smoothedDir * 10f);
        }
    }
}

using UnityEngine;

/// <summary>
/// Ship movement with automatic asteroid targeting when stuck
/// </summary>
public class NPCShipMovement : ShipMovementBase
{
    [Header("AI Behavior")]
    public float waypointReachDistance = 5f;

    [Header("Obstacle Avoidance")]
    public LayerMask obstacleLayer;
    public float minObstacleScanRadius = 15f;
    public float maxObstacleScanRadius = 25f;
    public float minObstacleAvoidWeight = 1.5f;
    public float maxObstacleAvoidWeight = 3f;
    public float obstacleCheckRadius = 50f;
    public int maxObstaclesToCheck = 15;

    [Header("Course Correction")]
    public float directApproachDistance = 30f;
    public float aggressiveCorrectionAngle = 45f;
    public float correctionStrength = 10f;

    [Header("Asteroid Combat")]
    public ShipTargetingSystem targetingSystem;
    public float stuckThreshold = 0.6f;
    public float clearThreshold = 0.2f;
    public float toggleCooldown = 3f;
    [Space]
    public int minObstaclesForStuck = 3; // NEW: Need at least this many obstacles to be "stuck"
    public float influenceSmoothness = 5f; // NEW: Higher = smoother (prevents spikes)

    [Header("Movement")]
    public float turnSmoothness = 2f;
    public float slowDownDistance = 15f;
    public float minSpeedMultiplier = 0.3f;

    [Header("Debug")]
    public bool showDebugRays = true;

    private Vector3 currentGoal;
    private bool hasGoal = false;
    private Collider[] nearbyObstacles = new Collider[30];
    private System.Random shipRandom;

    // Asteroid targeting state
    private bool asteroidTargetingActive = false;
    private float lastToggleTime = -999f;

    // NEW: Smoothed influence tracking
    private float smoothedObstacleInfluence = 0f;
    private int consecutiveHighInfluenceFrames = 0;
    private int consecutiveLowInfluenceFrames = 0;

    void Start()
    {
        shipRandom = new System.Random(GetInstanceID());

        if (targetingSystem == null)
        {
            targetingSystem = GetComponentInChildren<ShipTargetingSystem>();
            if (targetingSystem == null)
            {
                Debug.LogWarning($"[{gameObject.name}] No ShipTargetingSystem found! Asteroid targeting will not work.");
            }
        }
    }

    void FixedUpdate()
    {
        UpdateMovement();
    }

    public override void UpdateMovement()
    {
        if (!hasGoal)
        {
            if (rb.linearVelocity.magnitude > 0.1f)
            {
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, Time.fixedDeltaTime * 0.9f);
            }
            return;
        }

        Vector3 toGoal = currentGoal - rb.position;
        float distanceToGoal = toGoal.magnitude;

        if (distanceToGoal < waypointReachDistance)
        {
            hasGoal = false;
            return;
        }

        // === OBSTACLE DETECTION ===
        float rawObstacleInfluence = 0f;
        Vector3 obstacleAvoidanceSum = Vector3.zero;
        int relevantObstacleCount = 0; // NEW: Count obstacles actually affecting us

        int obstacleCount = Physics.OverlapSphereNonAlloc(
            rb.position,
            obstacleCheckRadius,
            nearbyObstacles,
            obstacleLayer
        );

        int checkCount = Mathf.Min(obstacleCount, maxObstaclesToCheck);

        for (int i = 0; i < checkCount; i++)
        {
            Collider obstacle = nearbyObstacles[i];
            if (obstacle == null) continue;

            Vector3 obstacleCenter = obstacle.bounds.center;
            float obstacleRadius = obstacle.bounds.extents.magnitude;

            Vector3 toObstacle = obstacleCenter - rb.position;
            float distance = toObstacle.magnitude;

            // ✅ FIX: Use deterministic scan radius based on obstacle position
            // This prevents influence from jumping when obstacles are destroyed
            float randomValue = Mathf.PerlinNoise(
                obstacleCenter.x * 0.1f,
                obstacleCenter.z * 0.1f
            );

            float scanRadius = Mathf.Lerp(
                minObstacleScanRadius,
                maxObstacleScanRadius,
                randomValue
            );

            if (distance < scanRadius + obstacleRadius)
            {
                float weight = ((scanRadius + obstacleRadius) - distance) / (scanRadius + obstacleRadius);
                obstacleAvoidanceSum -= toObstacle.normalized * weight;
                rawObstacleInfluence += weight;
                relevantObstacleCount++; // NEW: Count this obstacle

                if (showDebugRays)
                {
                    Debug.DrawLine(rb.position, obstacleCenter, Color.red);
                }
            }
        }

        // ✅ NEW: SMOOTH THE INFLUENCE (prevents spikes from single asteroid destruction)
        smoothedObstacleInfluence = Mathf.Lerp(
            smoothedObstacleInfluence,
            rawObstacleInfluence,
            Time.fixedDeltaTime * influenceSmoothness
        );

        // ✅ NEW: Use smoothed value + obstacle count for targeting decision
        float effectiveInfluence = smoothedObstacleInfluence;

        // Boost influence if many obstacles nearby (density matters!)
        if (relevantObstacleCount >= minObstaclesForStuck)
        {
            effectiveInfluence *= 1.2f; // 20% boost when crowded
        }

        // ✅ NEW: UPDATE ASTEROID TARGETING WITH STABLE INFLUENCE
        UpdateAsteroidTargeting(effectiveInfluence, relevantObstacleCount);

        // === CALCULATE ANGLE TO GOAL ===
        Vector3 goalDirection = toGoal.normalized;
        Vector3 currentDirection = transform.forward;
        float angleToGoal = Vector3.Angle(currentDirection, goalDirection);

        // === DECISION TREE: STEERING MODE ===
        Vector3 desiredDirection;
        bool isAvoiding = smoothedObstacleInfluence > 0.1f; // Use smoothed value
        bool isCloseToGoal = distanceToGoal < directApproachDistance;
        bool isOffCourse = angleToGoal > aggressiveCorrectionAngle;

        if (isAvoiding)
        {
            // OBSTACLE AVOIDANCE MODE
            float avoidWeight = Mathf.Lerp(
                minObstacleAvoidWeight,
                maxObstacleAvoidWeight,
                (float)shipRandom.NextDouble()
            );

            Vector3 avoidanceDirection = obstacleAvoidanceSum.normalized;
            desiredDirection = Vector3.Lerp(
                currentDirection,
                avoidanceDirection,
                avoidWeight * Time.fixedDeltaTime
            );

            if (showDebugRays)
            {
                Debug.DrawRay(rb.position, avoidanceDirection * 10f, Color.yellow);
                Debug.DrawRay(rb.position, Vector3.up * 10f, Color.red);
            }
        }
        else if (isCloseToGoal || isOffCourse)
        {
            // AGGRESSIVE CORRECTION MODE
            desiredDirection = Vector3.Slerp(
                currentDirection,
                goalDirection,
                correctionStrength * Time.fixedDeltaTime
            );

            if (showDebugRays)
            {
               // Debug.DrawRay(rb.position, Vector3.up * 12f, Color.green);
            }
        }
        else
        {
            // NORMAL STEERING
            desiredDirection = Vector3.Lerp(
                currentDirection,
                goalDirection,
                3f * Time.fixedDeltaTime
            );

            if (showDebugRays)
            {
                Debug.DrawRay(rb.position, Vector3.up * 5f, Color.cyan);
            }
        }

        desiredDirection = desiredDirection.normalized;

        // === SPEED CALCULATION ===
        float speedMultiplier = 1f;

        if (distanceToGoal < slowDownDistance)
        {
            speedMultiplier = Mathf.Clamp01(distanceToGoal / slowDownDistance);
        }

        if (isAvoiding)
        {
            speedMultiplier = Mathf.Max(speedMultiplier, 0.8f);
        }

        if (!isAvoiding && (isCloseToGoal || isOffCourse))
        {
            speedMultiplier = Mathf.Max(speedMultiplier, 0.9f);
        }

        speedMultiplier = Mathf.Max(speedMultiplier, minSpeedMultiplier);

        // === ROTATION ===
        // ✅ FIX: Prevent zero-vector error
        if (desiredDirection.sqrMagnitude < 0.001f)
        {
            desiredDirection = transform.forward; // Fallback to current direction
        }

        Quaternion targetRotation = Quaternion.LookRotation(desiredDirection);
        float rotationMultiplier = (!isAvoiding && (isCloseToGoal || isOffCourse)) ? 3f : 1f;

        rb.MoveRotation(Quaternion.Slerp(
            rb.rotation,
            targetRotation,
            rotationSpeed * turnSmoothness * rotationMultiplier * Time.fixedDeltaTime
        ));


        // === MOVEMENT ===
        float targetSpeed = maxSpeed * speedMultiplier;
        Vector3 targetVelocity = desiredDirection * targetSpeed;
        ApplyForce(targetVelocity);

        // === DEBUG VISUALIZATION ===
        if (showDebugRays)
        {
            Debug.DrawRay(rb.position, desiredDirection * 15f, Color.cyan);
            Debug.DrawLine(rb.position, currentGoal, Color.blue);

            if (angleToGoal > aggressiveCorrectionAngle)
            {
                Debug.DrawRay(rb.position, Vector3.down * 5f, Color.magenta);
            }

            if (isAvoiding)
            {
                Debug.DrawRay(rb.position, Vector3.up * (5f + smoothedObstacleInfluence * 5f), Color.red);
            }

            if (asteroidTargetingActive)
            {
                Debug.DrawRay(rb.position, Vector3.right * 10f, Color.yellow);
            }

            // NEW: Show obstacle count
            if (relevantObstacleCount > 0)
            {
                Debug.DrawRay(rb.position, Vector3.left * relevantObstacleCount, Color.black);
            }
        }
    }

    /// <summary>
    /// ✅ FIXED: Properly respect clearThreshold setting
    /// </summary>
    private void UpdateAsteroidTargeting(float effectiveInfluence, int obstacleCount)
    {
        if (targetingSystem == null) return;

        // ✅ FIX: Use AND instead of OR to respect clearThreshold
        bool isReallyStuck = effectiveInfluence > stuckThreshold && obstacleCount >= minObstaclesForStuck;
        bool isReallyClear = effectiveInfluence < clearThreshold && obstacleCount < 2;

        // ⚠️ Special case: If clearThreshold is 0, ONLY check obstacle count
        if (clearThreshold <= 0.001f)
        {
            isReallyClear = obstacleCount == 0; // Must be completely clear
        }

        // Track consecutive frames to prevent single-frame spikes
        if (isReallyStuck)
        {
            consecutiveHighInfluenceFrames++;
            consecutiveLowInfluenceFrames = 0;
        }
        else if (isReallyClear)
        {
            consecutiveLowInfluenceFrames++;
            consecutiveHighInfluenceFrames = 0;
        }
        else
        {
            // In hysteresis zone - maintain current state
            consecutiveHighInfluenceFrames = 0;
            consecutiveLowInfluenceFrames = 0;
        }

        // ENABLE when stuck for multiple frames
        if (consecutiveHighInfluenceFrames >= 3 && !asteroidTargetingActive)
        {
            if (Time.time - lastToggleTime < toggleCooldown)
                return;

            targetingSystem.SetAsteroidTargeting(true);
            asteroidTargetingActive = true;
            lastToggleTime = Time.time;

            if (showDebugRays)
            {
                Debug.Log($"[{gameObject.name}] 🔴 STUCK! Influence: {effectiveInfluence:F2}, Obstacles: {obstacleCount} → Asteroid targeting ON");
            }
        }
        // DISABLE when clear for multiple frames
        else if (consecutiveLowInfluenceFrames >= 5 && asteroidTargetingActive)
        {
            if (Time.time - lastToggleTime < toggleCooldown)
                return;

            targetingSystem.SetAsteroidTargeting(false);
            asteroidTargetingActive = false;
            lastToggleTime = Time.time;

            if (showDebugRays)
            {
                Debug.Log($"[{gameObject.name}] 🟢 CLEARED! Influence: {effectiveInfluence:F2}, Obstacles: {obstacleCount} → Asteroid targeting OFF");
            }
        }
        // MAINTAIN current state
        else if (asteroidTargetingActive && effectiveInfluence >= clearThreshold)
        {
            if (showDebugRays && Time.frameCount % 60 == 0)
            {
                Debug.Log($"[{gameObject.name}] ⚠️ STILL STUCK - Influence: {effectiveInfluence:F2}, Obstacles: {obstacleCount}");
            }
        }
    }


    public void SetWaypoint(Vector3 position)
    {
        currentGoal = position;
        hasGoal = true;
    }

    public void ClearWaypoint()
    {
        hasGoal = false;
    }

    public Vector3 GetCurrentWaypoint() => currentGoal;
    public bool HasWaypoint() => hasGoal;

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, obstacleCheckRadius);

        if (hasGoal)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, currentGoal);
            Gizmos.DrawWireSphere(currentGoal, waypointReachDistance);

            Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
            Gizmos.DrawWireSphere(currentGoal, directApproachDistance);

            Vector3 toGoal = currentGoal - transform.position;
            float angleToGoal = Vector3.Angle(transform.forward, toGoal.normalized);

            if (angleToGoal > aggressiveCorrectionAngle)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawRay(transform.position, toGoal.normalized * 20f);
            }
        }

        int count = Physics.OverlapSphereNonAlloc(
            transform.position,
            obstacleCheckRadius,
            nearbyObstacles,
            obstacleLayer
        );

        Gizmos.color = Color.red;
        for (int i = 0; i < Mathf.Min(count, maxObstaclesToCheck); i++)
        {
            if (nearbyObstacles[i] != null)
            {
                Gizmos.DrawLine(transform.position, nearbyObstacles[i].bounds.center);
            }
        }

        if (asteroidTargetingActive)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 5f, Vector3.one * 3f);
        }

        // NEW: Show smoothed influence level
        if (Application.isPlaying)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(transform.position, smoothedObstacleInfluence * 10f);
        }
    }
}

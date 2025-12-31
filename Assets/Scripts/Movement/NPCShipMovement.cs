using UnityEngine;

public class NPCShipMovement : ShipMovementBase
{
    [Header("AI Behavior")]
    public float waypointReachDistance = 10f;
    public float waypointSlowdownDistance = 25f;

    [Header("Obstacle Avoidance")]
    public LayerMask obstacleLayer;
    public float minObstacleScanRadius = 10f;
    public float maxObstacleScanRadius = 20f;
    public float minObstacleAvoidWeight = 2f;
    public float maxObstacleAvoidWeight = 5f;
    public float obstacleCheckRadius = 80f;
    public int maxObstaclesToCheck = 15;

    [Header("Collision Prevention")]
    public float emergencyBrakeDistance = 8f;
    public float criticalDistance = 15f;
    public bool useVelocityPrediction = true;
    public float collisionLookAhead = 2f;

    [Header("Gap Detection")]
    public float avoidanceConeAngle = 160f;
    public float pathCheckDistance = 30f;
    public float shipRadius = 5f;
    public float minGapWidth = 8f;
    public float obstaclePredictionTime = 1.5f;

    [Header("Course Correction")]
    public float directApproachDistance = 30f;
    public float aggressiveCorrectionAngle = 45f;
    public float correctionStrength = 10f;

    [Header("Steering Smoothness")] 
    [Tooltip("How quickly desired direction can change (lower = smoother, higher = more responsive)")]
    public float steeringDampening = 3f;
    [Tooltip("Smooth obstacle avoidance vector over time")]
    public float avoidanceVectorSmoothing = 8f;
    [Tooltip("Minimum angle change before accepting new direction")]
    public float directionChangeThreshold = 5f;
    [Tooltip("How long to commit to a direction before changing (seconds)")]
    public float directionCommitmentTime = 0.3f;

    [Header("Asteroid Combat")]
    public ShipTargetingSystem targetingSystem;
    public float stuckThreshold = 0.6f;
    public float clearThreshold = 0.2f;
    public float toggleCooldown = 3f;
    [Space]
    public int minObstaclesForStuck = 3;
    public float influenceSmoothness = 5f;

    [Header("Movement")]
    public float turnSmoothness = 2f;
    public float slowDownDistance = 15f;
    public float minSpeedMultiplier = 0.3f;

    private Vector3 currentGoal;
    private bool hasGoal = false;
    private WaypointPriority currentPriority = WaypointPriority.None;

    private Collider[] nearbyObstacles = new Collider[30];
    private System.Random shipRandom;

    private bool asteroidTargetingActive = false;
    private float lastToggleTime = -999f;

    private float smoothedObstacleInfluence = 0f;
    private int consecutiveHighInfluenceFrames = 0;
    private int consecutiveLowInfluenceFrames = 0;

    private bool isThreadingGap = false;
    private Vector3 lastGapDirection = Vector3.zero;
    private bool isEmergencyBraking = false;
    private float closestObstacleDistance = float.MaxValue;

    private Vector3 smoothedDesiredDirection;
    private Vector3 smoothedAvoidanceVector = Vector3.zero;
    private float lastDirectionChangeTime = 0f;
    private bool isInitialized = false;

    void Start()
    {
        shipRandom = new System.Random(GetInstanceID());

        if (targetingSystem == null)
        {
            targetingSystem = GetComponentInChildren<ShipTargetingSystem>();
        }

        if (shipRadius <= 0.1f)
        {
            var collider = GetComponent<Collider>();
            if (collider != null)
            {
                shipRadius = collider.bounds.extents.magnitude;
                Debug.Log($"[{gameObject.name}] Auto-detected ship radius: {shipRadius}");
            }
        }

        smoothedDesiredDirection = transform.forward;
        isInitialized = true;
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

        float effectiveReachDistance = waypointReachDistance + rb.linearVelocity.magnitude * 0.5f;

        if (distanceToGoal < effectiveReachDistance)
        {
            hasGoal = false;
            currentPriority = WaypointPriority.None;
            return;
        }

        float rawObstacleInfluence = 0f;
        Vector3 rawObstacleAvoidanceSum = Vector3.zero; 
        int relevantObstacleCount = 0;
        closestObstacleDistance = float.MaxValue;
        bool hasEmergencyObstacle = false;

        Vector3 goalDirection = toGoal.normalized;
        Vector3 currentDirection = transform.forward;

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
            Rigidbody obstacleRb = obstacle.attachedRigidbody;

            if (obstacleRb != null && obstacleRb.linearVelocity.sqrMagnitude > 0.01f)
            {
                obstacleCenter += obstacleRb.linearVelocity * obstaclePredictionTime;
            }

            float obstacleRadius = obstacle.bounds.extents.magnitude;
            Vector3 toObstacle = obstacleCenter - rb.position;
            float distance = toObstacle.magnitude;

            closestObstacleDistance = Mathf.Min(closestObstacleDistance, distance - obstacleRadius);

            float emergencyRadius = shipRadius + emergencyBrakeDistance;
            if (distance < emergencyRadius + obstacleRadius)
            {
                hasEmergencyObstacle = true;

                float emergencyWeight = 10f * (1f - (distance / (emergencyRadius + obstacleRadius)));
                rawObstacleAvoidanceSum -= toObstacle.normalized * emergencyWeight;
                rawObstacleInfluence += emergencyWeight;
                relevantObstacleCount++;
                continue;
            }

            float criticalRadius = shipRadius + criticalDistance;
            float angleToObstacle = Vector3.Angle(currentDirection, toObstacle);

            if (distance < criticalRadius + obstacleRadius && angleToObstacle < 135f)
            {
                float criticalWeight = 5f * (1f - (distance / (criticalRadius + obstacleRadius)));
                rawObstacleAvoidanceSum -= toObstacle.normalized * criticalWeight;
                rawObstacleInfluence += criticalWeight;
                relevantObstacleCount++;

                continue;
            }

            if (angleToObstacle > avoidanceConeAngle * 0.5f)
            {
                continue;
            }

            float randomValue = Mathf.PerlinNoise(
                obstacle.bounds.center.x * 0.1f,
                obstacle.bounds.center.z * 0.1f
            );

            float scanRadius = Mathf.Lerp(
                minObstacleScanRadius,
                maxObstacleScanRadius,
                randomValue
            );

            float effectiveScanRadius = scanRadius + shipRadius;

            if (distance < effectiveScanRadius + obstacleRadius)
            {
                float goalBlockage = 1f - Vector3.Dot(toObstacle.normalized, goalDirection);
                float proximityWeight = ((effectiveScanRadius + obstacleRadius) - distance) / (effectiveScanRadius + obstacleRadius);

                float weight = proximityWeight * (0.5f + goalBlockage * 0.5f);

                rawObstacleAvoidanceSum -= toObstacle.normalized * weight;
                rawObstacleInfluence += weight;
                relevantObstacleCount++;
            }
        }

        smoothedAvoidanceVector = Vector3.Lerp(
            smoothedAvoidanceVector,
            rawObstacleAvoidanceSum,
            Time.fixedDeltaTime * avoidanceVectorSmoothing
        );

        Vector3 obstacleAvoidanceSum = smoothedAvoidanceVector;

        if (useVelocityPrediction && rb.linearVelocity.magnitude > 1f)
        {
            Vector3 futurePosition = rb.position + rb.linearVelocity * collisionLookAhead;

            if (Physics.CheckSphere(futurePosition, shipRadius * 1.5f, obstacleLayer))
            {
                hasEmergencyObstacle = true;


            }
        }

        isEmergencyBraking = hasEmergencyObstacle;

        bool hasGapAhead = CheckGapAhead(goalDirection, out Vector3 gapDirection);
        isThreadingGap = hasGapAhead;
        if (hasGapAhead) lastGapDirection = gapDirection;

        smoothedObstacleInfluence = Mathf.Lerp(
            smoothedObstacleInfluence,
            rawObstacleInfluence,
            Time.fixedDeltaTime * influenceSmoothness
        );

        float effectiveInfluence = smoothedObstacleInfluence;

        if (relevantObstacleCount >= minObstaclesForStuck)
        {
            effectiveInfluence *= 1.2f;
        }

        UpdateAsteroidTargeting(effectiveInfluence, relevantObstacleCount);

        float angleToGoal = Vector3.Angle(currentDirection, goalDirection);

        Vector3 rawDesiredDirection; // ✅ NEW: Calculate target direction first
        bool isAvoiding = smoothedObstacleInfluence > 0.1f || hasEmergencyObstacle;
        bool isCloseToGoal = distanceToGoal < directApproachDistance;
        bool isOffCourse = angleToGoal > aggressiveCorrectionAngle;

        if (hasEmergencyObstacle)
        {
            if (obstacleAvoidanceSum.sqrMagnitude > 0.001f)
            {
                rawDesiredDirection = obstacleAvoidanceSum.normalized;
            }
            else
            {
                rawDesiredDirection = -currentDirection;
            }
        }
        else if (isAvoiding)
        {
            if (hasGapAhead)
            {
                rawDesiredDirection = gapDirection;
            }
            else
            {
                Vector3 avoidanceDirection = obstacleAvoidanceSum.normalized;

                rawDesiredDirection = Vector3.Lerp(
                    avoidanceDirection,
                    goalDirection,
                    0.3f 
                ).normalized;
            }
        }
        else if (isCloseToGoal || isOffCourse)
        {
            rawDesiredDirection = goalDirection;
        }
        else
        {
            rawDesiredDirection = goalDirection;
        }

        rawDesiredDirection = rawDesiredDirection.normalized;

        float dampening = hasEmergencyObstacle ? steeringDampening * 2f : steeringDampening;

        float angleDelta = Vector3.Angle(smoothedDesiredDirection, rawDesiredDirection);
        bool shouldChangeDirection = angleDelta > directionChangeThreshold;
        bool commitmentExpired = (Time.time - lastDirectionChangeTime) > directionCommitmentTime;

        if (shouldChangeDirection && commitmentExpired)
        {
            lastDirectionChangeTime = Time.time;
        }

        smoothedDesiredDirection = Vector3.Lerp(
            smoothedDesiredDirection,
            rawDesiredDirection,
            Time.fixedDeltaTime * dampening
        ).normalized;

        Vector3 desiredDirection = smoothedDesiredDirection;

        float speedMultiplier = 1f;

        if (hasEmergencyObstacle)
        {
            speedMultiplier = 0.1f;
        }
        else if (closestObstacleDistance < criticalDistance)
        {
            speedMultiplier = Mathf.Clamp01(closestObstacleDistance / criticalDistance) * 0.5f;
        }
        else if (distanceToGoal < waypointSlowdownDistance)
        {
            speedMultiplier = Mathf.Clamp01(distanceToGoal / waypointSlowdownDistance);
            speedMultiplier = Mathf.Max(speedMultiplier, 0.3f);
        }

        if (isAvoiding && !hasEmergencyObstacle)
        {
            if (hasGapAhead)
            {
                speedMultiplier = Mathf.Max(speedMultiplier, 0.6f);
            }
            else
            {
                speedMultiplier = Mathf.Max(speedMultiplier, 0.4f);
            }
        }

        speedMultiplier = Mathf.Max(speedMultiplier, minSpeedMultiplier);

        if (desiredDirection.sqrMagnitude < 0.001f)
        {
            desiredDirection = transform.forward;
        }

        Quaternion targetRotation = Quaternion.LookRotation(desiredDirection);
        float rotationMultiplier = hasEmergencyObstacle ? 5f : ((!isAvoiding && (isCloseToGoal || isOffCourse)) ? 3f : 1f);

        rb.MoveRotation(Quaternion.Slerp(
            rb.rotation,
            targetRotation,
            rotationSpeed * turnSmoothness * rotationMultiplier * Time.fixedDeltaTime
        ));

        float targetSpeed = maxSpeed * speedMultiplier;
        Vector3 targetVelocity = desiredDirection * targetSpeed;
        ApplyForce(targetVelocity);
    }

    private bool CheckGapAhead(Vector3 goalDirection, out Vector3 gapDirection)
    {
        gapDirection = goalDirection;

        int rayCount = 9;
        float maxAngle = 60f;
        float bestGapScore = 0f;
        Vector3 bestGapDir = goalDirection;

        for (int i = 0; i < rayCount; i++)
        {
            float angle = Mathf.Lerp(-maxAngle, maxAngle, i / (float)(rayCount - 1));
            Vector3 rayDir = Quaternion.Euler(0f, angle, 0f) * goalDirection;

            bool hitObstacle = Physics.SphereCast(
                rb.position,
                shipRadius * 1.2f,
                rayDir,
                out RaycastHit hit,
                pathCheckDistance,
                obstacleLayer
            );

            if (!hitObstacle)
            {
                float clearanceScore = 1f / (1f + Mathf.Abs(angle) * 0.01f);

                if (clearanceScore > bestGapScore)
                {
                    bestGapScore = clearanceScore;
                    bestGapDir = rayDir;
                }


            }
        }

        if (bestGapScore > 0f)
        {
            gapDirection = bestGapDir;
            return true;
        }

        return false;
    }

    private void UpdateAsteroidTargeting(float effectiveInfluence, int obstacleCount)
    {
        if (targetingSystem == null) return;

        bool isReallyStuck = effectiveInfluence > stuckThreshold && obstacleCount >= minObstaclesForStuck;
        bool isReallyClear = effectiveInfluence < clearThreshold && obstacleCount < 2;

        if (clearThreshold <= 0.001f)
        {
            isReallyClear = obstacleCount == 0;
        }

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
            consecutiveHighInfluenceFrames = 0;
            consecutiveLowInfluenceFrames = 0;
        }

        if (consecutiveHighInfluenceFrames >= 3 && !asteroidTargetingActive)
        {
            if (Time.time - lastToggleTime < toggleCooldown)
                return;

            targetingSystem.SetAsteroidTargeting(true);
            asteroidTargetingActive = true;
            lastToggleTime = Time.time;
        }
        else if (consecutiveLowInfluenceFrames >= 5 && asteroidTargetingActive)
        {
            if (Time.time - lastToggleTime < toggleCooldown)
                return;

            targetingSystem.SetAsteroidTargeting(false);
            asteroidTargetingActive = false;
            lastToggleTime = Time.time;
        }
    }

    public void SetWaypoint(Vector3 position, WaypointPriority priority = WaypointPriority.Low)
    {
        if (priority >= currentPriority)
        {
            currentGoal = position;
            hasGoal = true;
            currentPriority = priority;
        }
    }

    public void ClearWaypoint(WaypointPriority priority = WaypointPriority.High)
    {
        if (priority >= currentPriority)
        {
            hasGoal = false;
            currentPriority = WaypointPriority.None;
        }
    }

    public void ForceClearWaypoint()
    {
        hasGoal = false;
        currentPriority = WaypointPriority.None;
    }

    public Vector3 GetCurrentWaypoint() => currentGoal;
    public bool HasWaypoint() => hasGoal;
    public WaypointPriority GetCurrentPriority() => currentPriority;
   
}

public enum WaypointPriority
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

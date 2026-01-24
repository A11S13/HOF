using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class NPCShipMovement : ShipMovementBase
{
    [Header("Moving Field Settings")]
    public float predictionTime = 3.0f;
    public float dynamicClearance = 8.0f;
    public float baseLookAhead = 150f;

    [Header("Ship Avoidance (Separation)")]
    public LayerMask shipAvoidanceLayer;
    public float separationRadius = 80f;
    public float separationForce = 3.5f;

    [Header("Waypoint Smoothing")]
    public float goalSmoothingTime = 0.3f;
    public float passThroughRadius = 40f;
    public float passThroughDistance = 150f;

    [Header("Inertia & Physics")]
    public float aiAccelerationScale = 8.0f;
    [Tooltip("Drag when moving normally.")]
    public float standardDrag = 0.5f;
    [Tooltip("Drag when boosting.")]
    public float boostDrag = 0.1f;

    [Header("Strafing Settings")]
    public bool enableStrafing = true;
    [Range(0.0f, 1.0f)]
    public float normalStrafeFactor = 1.0f;
    [Range(0.0f, 1.0f)]
    public float boostStrafeFactor = 0.2f;

    [Header("Physical Constraints")]
    public float shipCollisionRadius = 15f;

    [Header("Combat & Stuck Detection")]
    public ShipTargetingSystem targetingSystem;
    public float stuckThreshold = 0.1f;
    public float contactRepulsionForce = 100f;

    [Header("Sensors")]
    public LayerMask obstacleLayer;
    public int resolution = 72;

    [Header("Flow Field (Optional)")]
    public NPCFlowFieldNavigator flowFieldNavigator;
    public bool useFlowFieldNavigation = true;
    [Tooltip("Distance to switch from flow field to direct path")]
    public float directApproachDistance = 60f;

    // --- State Variables ---
    private Vector3 currentGoal;
    private Vector3 smoothedGoal;
    private Vector3 smoothedGoalVel;
    private bool hasGoal = false;
    private WaypointPriority currentPriority = WaypointPriority.None;

    // NEW: Multi-waypoint system for smooth paths
    private Vector3 nextGoal; // Second waypoint for path smoothing
    private bool hasNextGoal = false;
    private WaypointPriority nextPriority = WaypointPriority.None;

    // NEW: Combat mode flag (set by NPCBehavior)
    private bool isInCombatManeuver = false;

    // --- Rotation / Targeting State ---
    private Transform focusTarget;
    private Vector3? focusPoint;

    // --- Boost State ---
    private bool boostRequested = false;
    private float targetBoostMultiplier = 1f;

    // --- Physics Internal ---
    private Vector3 smoothedDir;
    private Vector3 steerVelocity;
    private float stuckTimer = 0f;
    private bool isMining = false;
    private bool isPhysicallyColliding = false;

    // --- Context Arrays ---
    private float[] interestMap;
    private float[] dangerMap;
    private Vector3[] rayDirections;
    private Collider[] nearbyObstacles = new Collider[60];
    private Collider[] avoidanceColliders = new Collider[20];
    private int countedObstacles = 0;

    protected override void Awake()
    {
        base.Awake();

        if (shipCollisionRadius <= 1f)
        {
            var col = GetComponent<Collider>();
            shipCollisionRadius = (col != null) ? col.bounds.extents.magnitude : 10f;
        }

        interestMap = new float[resolution];
        dangerMap = new float[resolution];
        rayDirections = new Vector3[resolution];

        for (int i = 0; i < resolution; i++)
        {
            float angle = i * 2 * Mathf.PI / resolution;
            rayDirections[i] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }

        smoothedDir = transform.forward;
        smoothedGoal = transform.position;
        rb.linearDamping = standardDrag;
    }

    void Start()
    {
        if (flowFieldNavigator == null) flowFieldNavigator = GetComponent<NPCFlowFieldNavigator>();
    }

    void FixedUpdate()
    {
        isPhysicallyColliding = false;
        rb.linearDamping = boostRequested ? boostDrag : standardDrag;
        UpdateMovement();
    }

    public override void UpdateMovement()
    {
        if (!hasGoal)
        {
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, Time.fixedDeltaTime * 0.5f);
            return;
        }

        // --- FIX: Logic to prevent smoothedGoal from dragging behind ---
        // If the goal is far, we smooth. If we snapped it (Critical/High), it jumps instantly.
        smoothedGoal = Vector3.SmoothDamp(smoothedGoal, currentGoal, ref smoothedGoalVel, goalSmoothingTime);

        float currentSpeed = rb.linearVelocity.magnitude;
        float boostMultiplier = boostRequested ? targetBoostMultiplier : 1.0f;
        float activeLookAhead = baseLookAhead * boostMultiplier + (currentSpeed * 1.0f);

        // 1. Get Ideal Navigation Direction
        Vector3 globalDir = GetGlobalNavigationDirection();
        globalDir.y = 0f;
        if (globalDir.sqrMagnitude < 0.0001f) globalDir = transform.forward;
        globalDir.Normalize();

        // 2. Build Context Maps
        System.Array.Clear(interestMap, 0, resolution);
        System.Array.Clear(dangerMap, 0, resolution);

        for (int i = 0; i < resolution; i++)
        {
            float dot = Vector3.Dot(rayDirections[i], globalDir);
            interestMap[i] = Mathf.Max(0f, dot);
        }

        PopulatePredictiveDangerMap(activeLookAhead);

        // 3. Process Maps
        Vector3 bestDir = transform.forward;
        float bestScore = -1f;

        for (int i = 0; i < resolution; i++)
        {
            if (dangerMap[i] > 0.5f)
            {
                interestMap[i] = 0f;
            }
            else
            {
                interestMap[i] *= (1.0f - dangerMap[i]);
            }

            if (interestMap[i] > bestScore)
            {
                bestScore = interestMap[i];
                bestDir = rayDirections[i];
            }
        }

        // 4. Ship Separation
        Vector3 separation = ComputeSeparationSteer();
        if (separation.sqrMagnitude > 0.1f)
        {
            float separationStrength = separation.magnitude;
            if (separationStrength > 0.8f)
            {
                bestDir = Vector3.Lerp(bestDir, separation.normalized, 0.85f).normalized;
            }
            else
            {
                bestDir = (bestDir + separation * separationForce).normalized;
            }
        }

        // 5. Combat Logic
        HandleCombat(bestScore);

        if (isPhysicallyColliding)
        {
            smoothedDir = transform.forward;
            return;
        }

        // 6. THROTTLE - Different for combat vs normal
        float throttle = isInCombatManeuver ? 1.0f : CalculateNavigationThrottle();

        // CRITICAL: Even in combat mode, respect danger
        if (bestScore < stuckThreshold)
        {
            throttle = 0.5f;
            bestDir = FindEscapeDirection();

            // Emergency obstacle detected during combat - break combat mode temporarily
            if (isInCombatManeuver)
            {
                throttle = 0.3f; // Slow down more
            }
        }

        // Additional safety: reduce throttle if high danger ahead
        float maxDangerAhead = GetMaxDangerInDirection(bestDir);
        if (maxDangerAhead > 0.7f)
        {
            throttle *= (1.0f - maxDangerAhead * 0.5f); // Reduce by up to 50%
        }

        smoothedDir = Vector3.SmoothDamp(smoothedDir, bestDir, ref steerVelocity, 0.28f);

        // 7. Apply Forces
        float currentMaxSpeed = maxSpeed * targetBoostMultiplier;
        ApplyInertialMotion(smoothedDir, throttle, currentMaxSpeed);
    }

    /// <summary>
    /// Throttle for normal navigation (not combat maneuvers)
    /// </summary>
    float CalculateNavigationThrottle()
    {
        float distanceToGoal = Vector3.Distance(transform.position, smoothedGoal);

        // Simple braking for waypoints
        if (distanceToGoal < 80f)
        {
            return Mathf.Clamp01(distanceToGoal / 80f);
        }

        return 1.0f;
    }

    void ApplyInertialMotion(Vector3 moveDir, float throttle, float speedLimit)
    {
        // --- A. ROTATION LOGIC ---
        Quaternion desiredRotation;

        if (focusTarget != null)
        {
            Vector3 lookDir = focusTarget.position - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.1f)
                desiredRotation = Quaternion.LookRotation(lookDir);
            else
                desiredRotation = transform.rotation;
        }
        else if (focusPoint.HasValue)
        {
            Vector3 lookDir = focusPoint.Value - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.1f)
                desiredRotation = Quaternion.LookRotation(lookDir);
            else
                desiredRotation = Quaternion.LookRotation(moveDir);
        }
        else
        {
            desiredRotation = Quaternion.LookRotation(moveDir);
        }

        base.RotateTowards(desiredRotation);

        // --- B. VELOCITY / STRAFE LOGIC ---
        Vector3 desiredVel = moveDir.normalized * (speedLimit * throttle);
        Vector3 v = rb.linearVelocity;
        v.y = 0f;

        float baseAccel = Mathf.Max(0.1f, acceleration);
        float effectiveAccel = baseAccel * Mathf.Max(0.1f, aiAccelerationScale);

        float strafeFactor = enableStrafing ? normalStrafeFactor : 0.1f;
        if (boostRequested) strafeFactor *= boostStrafeFactor;

        Vector3 f = transform.forward;
        f.y = 0f;
        if (f.sqrMagnitude < 0.0001f) f = Vector3.forward;
        f.Normalize();

        Vector3 dv = desiredVel - v;
        float dt = Time.fixedDeltaTime;
        Vector3 aReq = (dt > 0f) ? (dv / dt) : Vector3.zero;

        Vector3 aFwd = Vector3.Project(aReq, f);
        Vector3 aLat = aReq - aFwd;

        float latLimit = effectiveAccel * Mathf.Clamp01(strafeFactor);
        if (aLat.magnitude > latLimit)
        {
            aLat = aLat.normalized * latLimit;
        }

        Vector3 a = aFwd + aLat;

        if (a.magnitude > effectiveAccel)
        {
            a = a.normalized * effectiveAccel;
        }

        Vector3 newVel = v + a * dt;
        rb.linearVelocity = new Vector3(newVel.x, rb.linearVelocity.y, newVel.z);
    }

    Vector3 GetGlobalNavigationDirection()
    {
        Vector3 goalForNav = smoothedGoal;

        // NEW: Look-ahead blending for smooth paths
        if (hasNextGoal)
        {
            float distToCurrent = Vector3.Distance(transform.position, currentGoal);
            float distToNext = Vector3.Distance(transform.position, nextGoal);
            float totalDist = distToCurrent + distToNext;

            // Start blending to next waypoint when close to current
            if (distToCurrent < 150f && totalDist > 0.1f)
            {
                float blendFactor = 1.0f - (distToCurrent / 150f);
                goalForNav = Vector3.Lerp(currentGoal, nextGoal, blendFactor);

                // Auto-advance to next waypoint when very close
                if (distToCurrent < 50f)
                {
                    currentGoal = nextGoal;
                    currentPriority = nextPriority;
                    hasNextGoal = false;
                }
            }
        }

        Vector3 toGoal = goalForNav - transform.position;
        toGoal.y = 0f;

        // ALWAYS use flow field when available - it knows about ALL obstacles
        if (useFlowFieldNavigation && flowFieldNavigator != null)
        {
            flowFieldNavigator.SetGoal(goalForNav);
            Vector3 flowDir = flowFieldNavigator.GetNavigationDirection(transform.position);

            // Flow field returns safe path - ALWAYS use it as primary direction
            if (flowDir.sqrMagnitude > 0.1f)
            {
                return flowDir;
            }
        }

        // Fallback only if flow field fails
        if (toGoal.magnitude < passThroughRadius)
        {
            Vector3 f = transform.forward;
            f.y = 0f;
            goalForNav = transform.position + f.normalized * passThroughDistance;
        }

        return (goalForNav - transform.position).normalized;
    }

    void PopulatePredictiveDangerMap(float lookAhead)
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, lookAhead, nearbyObstacles, obstacleLayer);
        int checks = Mathf.Min(count, nearbyObstacles.Length);
        countedObstacles = checks;

        Vector3 ourFuturePos = transform.position + rb.linearVelocity * predictionTime;

        for (int k = 0; k < checks; k++)
        {
            Collider obs = nearbyObstacles[k];
            if (obs == null || obs.transform.root == transform.root) continue;

            Vector3 obsVel = Vector3.zero;
            if (obs.attachedRigidbody != null) obsVel = obs.attachedRigidbody.linearVelocity;

            Vector3 currentPos = obs.bounds.center;
            float radius = obs.bounds.extents.magnitude;

            if (obsVel.sqrMagnitude > 1.0f)
            {
                Vector3 futurePos = currentPos + (obsVel * predictionTime);
                ProcessCapsuleObstacle(currentPos, futurePos, radius, lookAhead);
            }
            else
            {
                ProcessCapsuleObstacle(currentPos, currentPos, radius, lookAhead);
            }

            float futureCollisionDist = Vector3.Distance(ourFuturePos, currentPos + obsVel * predictionTime);
            if (futureCollisionDist < radius + shipCollisionRadius + dynamicClearance)
            {
                Vector3 toObs = (currentPos - transform.position).normalized;
                for (int i = 0; i < resolution; i++)
                {
                    if (Vector3.Dot(rayDirections[i], toObs) > 0.5f)
                    {
                        dangerMap[i] = Mathf.Max(dangerMap[i], 0.95f);
                    }
                }
            }
        }
    }

    void ProcessCapsuleObstacle(Vector3 start, Vector3 end, float radius, float lookAhead)
    {
        float boostClearanceMultiplier = boostRequested ? 1.5f : 1.0f;
        float safeRadius = (radius + shipCollisionRadius + dynamicClearance) * boostClearanceMultiplier;

        for (int i = 0; i < resolution; i++)
        {
            Vector3 rayDir = rayDirections[i];

            float tSeg = DistancePointToSegmentParam(transform.position, start, end);
            Vector3 closestObstaclePoint = Vector3.Lerp(start, end, tSeg);

            Vector3 toObs = closestObstaclePoint - transform.position;
            toObs.y = 0f;
            float dist = toObs.magnitude;

            if (dist > lookAhead || dist <= 0.001f) continue;
            if (Vector3.Dot(rayDir, toObs.normalized) < 0f) continue;

            float angleRad = Mathf.Asin(Mathf.Clamp01(safeRadius / dist));
            float angleDeg = angleRad * Mathf.Rad2Deg;
            float angleToRay = Vector3.Angle(rayDir, toObs.normalized);

            if (angleToRay < angleDeg)
            {
                float distFactor = 1.0f - (dist / lookAhead);
                float weight = Mathf.Pow(distFactor, 0.5f);

                if (weight > dangerMap[i]) dangerMap[i] = weight;
            }
        }
    }

    Vector3 ComputeSeparationSteer()
    {
        if (shipAvoidanceLayer == 0) return Vector3.zero;

        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, separationRadius, avoidanceColliders, shipAvoidanceLayer);
        if (hitCount == 0) return Vector3.zero;

        Vector3 steer = Vector3.zero;
        int count = 0;
        float accumulatedWeight = 0f;

        for (int i = 0; i < hitCount; i++)
        {
            Collider h = avoidanceColliders[i];
            if (h == null) continue;
            if (h.transform.root == transform.root) continue;

            Vector3 closestPoint = h.ClosestPoint(transform.position);

            Vector3 away = transform.position - closestPoint;
            if (away.sqrMagnitude < 0.001f)
            {
                away = transform.position - h.bounds.center;
            }

            away.y = 0f;
            float distToSurface = away.magnitude;

            if (distToSurface < separationRadius)
            {
                float pct = Mathf.Clamp01(distToSurface / separationRadius);
                float weight = (1f - pct) * (1f - pct);

                steer += away.normalized * weight;
                accumulatedWeight += weight;
                count++;
            }
        }

        if (count == 0) return Vector3.zero;
        return steer.normalized * Mathf.Clamp01(accumulatedWeight);
    }

    Vector3 FindEscapeDirection()
    {
        float minDanger = 1.0f;
        Vector3 escapeDir = -transform.forward;
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

    float GetMaxDangerInDirection(Vector3 direction)
    {
        float maxDanger = 0f;
        direction.y = 0f;
        direction.Normalize();

        for (int i = 0; i < resolution; i++)
        {
            float angle = Vector3.Angle(rayDirections[i], direction);
            if (angle < 30f)
            {
                if (dangerMap[i] > maxDanger)
                {
                    maxDanger = dangerMap[i];
                }
            }
        }

        return maxDanger;
    }

    float DistancePointToSegmentParam(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float magSq = ab.sqrMagnitude;
        if (magSq < 0.001f) return 0f;
        return Mathf.Clamp01(Vector3.Dot(p - a, ab) / magSq);
    }

    void OnCollisionStay(Collision collision)
    {
        int combinedMask = obstacleLayer | shipAvoidanceLayer;

        if (((1 << collision.gameObject.layer) & combinedMask) != 0)
        {
            isPhysicallyColliding = true;
            Vector3 pushDir = Vector3.zero;
            foreach (ContactPoint contact in collision.contacts)
            {
                pushDir += contact.normal;
            }
            pushDir.Normalize();
            pushDir.y = 0f;
            rb.AddForce(pushDir * contactRepulsionForce, ForceMode.Acceleration);
        }
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

    // ==========================================================
    // PUBLIC API
    // ==========================================================

    public void SetWaypoint(Vector3 position, WaypointPriority priority = WaypointPriority.Low)
    {
        if (priority >= currentPriority)
        {
            currentGoal = position;
            hasGoal = true;
            currentPriority = priority;

            // FIX: Explicitly clear pathing data so we don't try to blend to an old 'nextGoal'
            hasNextGoal = false;

            // FIX: If this is urgent (High/Critical), snap immediately to prevent smoothing lag
            // This prevents the "braking" when switching targets rapidly behind/ahead of ship
            if (priority >= WaypointPriority.High || smoothedGoal == Vector3.zero || !Application.isPlaying)
            {
                smoothedGoal = currentGoal;
                smoothedGoalVel = Vector3.zero; // Reset velocity of the smoother
            }
        }
    }

    public void SetWaypointPath(Vector3 waypoint1, Vector3 waypoint2, WaypointPriority priority = WaypointPriority.Low)
    {
        if (priority >= currentPriority)
        {
            currentGoal = waypoint1;
            nextGoal = waypoint2;
            hasGoal = true;
            hasNextGoal = true;
            currentPriority = priority;
            nextPriority = priority;

            // FIX: Also snap for paths if urgent
            if (priority >= WaypointPriority.High || smoothedGoal == Vector3.zero || !Application.isPlaying)
            {
                smoothedGoal = currentGoal;
                smoothedGoalVel = Vector3.zero;
            }
        }
    }

    public void ClearWaypoint(WaypointPriority priority = WaypointPriority.High)
    {
        if (priority >= currentPriority)
        {
            hasGoal = false;
            hasNextGoal = false;
            currentPriority = WaypointPriority.None;
            nextPriority = WaypointPriority.None;
        }
    }

    public void SetFocusTarget(Transform t)
    {
        focusTarget = t;
        focusPoint = null;
    }

    public void SetFocusPoint(Vector3 p)
    {
        focusTarget = null;
        focusPoint = p;
    }

    public void ClearFocusTarget()
    {
        focusTarget = null;
        focusPoint = null;
    }

    public void SetBoost(bool active, float multiplier = 1.0f)
    {
        boostRequested = active;
        targetBoostMultiplier = active ? Mathf.Max(1f, multiplier) : 1f;
    }

    public void SetCombatManeuverMode(bool active)
    {
        isInCombatManeuver = active;
    }

    public int GetObstacleCount() => countedObstacles;

    public void ClearFlankingTarget() { }
}
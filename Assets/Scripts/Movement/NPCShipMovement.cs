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
    public float contactRepulsionForce = 150f; // Increased for better bounce

    [Header("Sensors")]
    public LayerMask obstacleLayer;
    public int resolution = 72;

    [Header("Flow Field (Optional)")]
    public NPCFlowFieldNavigator flowFieldNavigator;
    public bool useFlowFieldNavigation = true;

    // --- State Variables ---
    private Vector3 currentGoal;
    private Vector3 smoothedGoal;
    private Vector3 smoothedGoalVel;
    private bool hasGoal = false;
    private WaypointPriority currentPriority = WaypointPriority.None;

    // Multi-waypoint system
    private Vector3 nextGoal;
    private bool hasNextGoal = false;
    private WaypointPriority nextPriority = WaypointPriority.None;

    // Combat mode flag
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

    // NEW: Proper Stuck Detection
    private float lowSpeedTimer = 0f;
    private bool isReversing = false;
    private float reverseTimer = 0f;

    private bool isMining = false;
    private bool isPhysicallyColliding = false;
    private Vector3 lastCollisionNormal = Vector3.forward;

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
        // Reset collision flag every frame; it will be set true in OnCollisionStay if still touching
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

        // --- 1. GOAL SMOOTHING ---
        if (currentPriority >= WaypointPriority.High)
        {
            smoothedGoal = currentGoal;
            smoothedGoalVel = Vector3.zero;
        }
        else
        {
            smoothedGoal = Vector3.SmoothDamp(smoothedGoal, currentGoal, ref smoothedGoalVel, goalSmoothingTime);
        }

        float currentSpeed = rb.linearVelocity.magnitude;
        float boostMultiplier = boostRequested ? targetBoostMultiplier : 1.0f;
        float activeLookAhead = baseLookAhead * boostMultiplier + (currentSpeed * 1.0f);

        // --- 2. GET DIRECTION ---
        Vector3 globalDir = GetOrganicDirection();
        globalDir.y = 0f;
        if (globalDir.sqrMagnitude < 0.0001f) globalDir = transform.forward;
        globalDir.Normalize();

        // --- 3. CONTEXT SENSORS ---
        System.Array.Clear(interestMap, 0, resolution);
        System.Array.Clear(dangerMap, 0, resolution);

        for (int i = 0; i < resolution; i++)
        {
            float dot = Vector3.Dot(rayDirections[i], globalDir);
            interestMap[i] = Mathf.Max(0f, dot);
        }

        PopulatePredictiveDangerMap(activeLookAhead);

        Vector3 bestDir = transform.forward;
        float bestScore = -1f;

        for (int i = 0; i < resolution; i++)
        {
            if (dangerMap[i] > 0.6f) interestMap[i] = 0f;
            else interestMap[i] *= (1.0f - dangerMap[i]);

            if (interestMap[i] > bestScore)
            {
                bestScore = interestMap[i];
                bestDir = rayDirections[i];
            }
        }

        // --- 4. SEPARATION ---
        Vector3 separation = ComputeSeparationSteer();
        if (separation.sqrMagnitude > 0.1f)
        {
            if (separation.magnitude > 0.8f)
                bestDir = Vector3.Lerp(bestDir, separation.normalized, 0.85f).normalized;
            else
                bestDir = (bestDir + separation * separationForce).normalized;
        }

        // --- 5. THROTTLE & STUCK LOGIC ---
        float throttle = isInCombatManeuver ? 1.0f : CalculateNavigationThrottle();

        // FIX 1: Handle "Pushing" vs "Real Stuck"
        // If we are pushing hard (throttle > 0.5) but moving slow (< 5), we are stuck.
        if (throttle > 0.5f && currentSpeed < 5f)
        {
            lowSpeedTimer += Time.fixedDeltaTime;
        }
        else
        {
            lowSpeedTimer = Mathf.Max(0f, lowSpeedTimer - Time.fixedDeltaTime);
        }

        // Trigger Reverse?
        if (lowSpeedTimer > 1.0f) // Stuck for 1 second
        {
            isReversing = true;
            reverseTimer = 1.5f; // Reverse for 1.5 seconds
            lowSpeedTimer = 0f;
        }

        if (isReversing)
        {
            reverseTimer -= Time.fixedDeltaTime;
            throttle = -0.8f; // Go backwards!
            // Steer randomly to break the lock
            bestDir = (transform.right * (Random.value > 0.5f ? 1f : -1f) + transform.forward * -0.5f).normalized;

            if (reverseTimer <= 0f) isReversing = false;
        }
        else
        {
            // Standard safety brakes (non-reverse)
            if (bestScore < stuckThreshold)
            {
                if (isInCombatManeuver) throttle = 0.6f;
                else
                {
                    throttle = 0.5f;
                    bestDir = FindEscapeDirection();
                }
            }
            else if (!isInCombatManeuver)
            {
                float maxDanger = GetMaxDangerInDirection(bestDir);
                if (maxDanger > 0.7f) throttle *= (1.0f - maxDanger * 0.5f);
            }
        }

        // --- FIX 2: COLLISION OVERRIDE ---
        // If we are touching a rock, override all logic to SLIDE off it.
        // Don't just return! Use the contact info to steer.
        if (isPhysicallyColliding && !isReversing)
        {
            // Calculate a "Slide" vector: Forward projected onto the wall plane
            Vector3 slideDir = Vector3.ProjectOnPlane(transform.forward, lastCollisionNormal).normalized;

            // Blend: 40% bounce away (normal), 60% slide along (slideDir)
            bestDir = Vector3.Lerp(lastCollisionNormal, slideDir, 0.6f).normalized;

            // Keep throttle high to power through the slide
            throttle = Mathf.Max(throttle, 0.8f);
        }

        // --- 6. EXECUTE ---
        float turnRate = (currentPriority >= WaypointPriority.Critical || isPhysicallyColliding) ? 0.1f : 0.28f;
        smoothedDir = Vector3.SmoothDamp(smoothedDir, bestDir, ref steerVelocity, turnRate);

        float currentMaxSpeed = maxSpeed * targetBoostMultiplier;
        ApplyInertialMotion(smoothedDir, throttle, currentMaxSpeed);
    }

    // --- Helper Methods ---

    Vector3 GetOrganicDirection()
    {
        if (useFlowFieldNavigation && flowFieldNavigator != null && !isInCombatManeuver)
        {
            flowFieldNavigator.SetGoal(smoothedGoal);
            Vector3 flowDir = flowFieldNavigator.GetNavigationDirection(transform.position);
            if (flowDir.sqrMagnitude > 0.1f) return flowDir;
        }

        if (hasNextGoal)
        {
            float distToCurrent = Vector3.Distance(transform.position, currentGoal);
            float t = 1.0f - Mathf.Clamp01(distToCurrent / 300f);
            Vector3 targetPointOnCurve = EvaluateQuadraticBezier(transform.position, currentGoal, nextGoal, t + 0.1f);

            if (distToCurrent < passThroughRadius)
            {
                currentGoal = nextGoal;
                currentPriority = nextPriority;
                hasNextGoal = false;
                return (currentGoal - transform.position).normalized;
            }
            return (targetPointOnCurve - transform.position).normalized;
        }

        Vector3 toGoal = smoothedGoal - transform.position;
        toGoal.y = 0f;
        if (toGoal.magnitude < passThroughRadius) return transform.forward;
        return toGoal.normalized;
    }

    Vector3 EvaluateQuadraticBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        t = Mathf.Clamp01(t);
        float u = 1f - t;
        return (u * u * p0) + (2f * u * t * p1) + (t * t * p2);
    }

    float CalculateNavigationThrottle()
    {
        float distanceToGoal = Vector3.Distance(transform.position, smoothedGoal);
        if (distanceToGoal < 80f) return Mathf.Clamp01(distanceToGoal / 80f);
        return 1.0f;
    }

    void ApplyInertialMotion(Vector3 moveDir, float throttle, float speedLimit)
    {
        Quaternion desiredRotation;
        if (focusTarget != null)
        {
            Vector3 lookDir = focusTarget.position - transform.position; lookDir.y = 0f;
            desiredRotation = (lookDir.sqrMagnitude > 0.1f) ? Quaternion.LookRotation(lookDir) : transform.rotation;
        }
        else if (focusPoint.HasValue)
        {
            Vector3 lookDir = focusPoint.Value - transform.position; lookDir.y = 0f;
            desiredRotation = (lookDir.sqrMagnitude > 0.1f) ? Quaternion.LookRotation(lookDir) : Quaternion.LookRotation(moveDir);
        }
        else desiredRotation = Quaternion.LookRotation(moveDir);

        base.RotateTowards(desiredRotation);

        Vector3 desiredVel = moveDir.normalized * (speedLimit * throttle);
        Vector3 v = rb.linearVelocity; v.y = 0f;

        float baseAccel = Mathf.Max(0.1f, acceleration);
        float effectiveAccel = baseAccel * Mathf.Max(0.1f, aiAccelerationScale);

        float strafeFactor = enableStrafing ? normalStrafeFactor : 0.1f;
        if (boostRequested) strafeFactor *= boostStrafeFactor;

        Vector3 f = transform.forward; f.y = 0f; f.Normalize();
        Vector3 dv = desiredVel - v;
        float dt = Time.fixedDeltaTime;
        Vector3 aReq = (dt > 0f) ? (dv / dt) : Vector3.zero;

        Vector3 aFwd = Vector3.Project(aReq, f);
        Vector3 aLat = aReq - aFwd;

        float latLimit = effectiveAccel * Mathf.Clamp01(strafeFactor);
        if (aLat.magnitude > latLimit) aLat = aLat.normalized * latLimit;

        Vector3 a = aFwd + aLat;
        if (a.magnitude > effectiveAccel) a = a.normalized * effectiveAccel;

        Vector3 newVel = v + a * dt;
        rb.linearVelocity = new Vector3(newVel.x, rb.linearVelocity.y, newVel.z);
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

            // Small asteroid fix: Ensure minimal radius for detection so small rocks aren't ignored
            if (radius < 2.0f) radius = 2.0f;

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
                    if (Vector3.Dot(rayDirections[i], toObs) > 0.5f) dangerMap[i] = Mathf.Max(dangerMap[i], 0.95f);
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

            Vector3 toObs = closestObstaclePoint - transform.position; toObs.y = 0f;
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
        float accumulatedWeight = 0f;
        for (int i = 0; i < hitCount; i++)
        {
            Collider h = avoidanceColliders[i];
            if (h == null || h.transform.root == transform.root) continue;
            Vector3 closestPoint = h.ClosestPoint(transform.position);
            Vector3 away = transform.position - closestPoint;
            if (away.sqrMagnitude < 0.001f) away = transform.position - h.bounds.center;
            away.y = 0f;

            float distToSurface = away.magnitude;
            if (distToSurface < separationRadius)
            {
                float pct = Mathf.Clamp01(distToSurface / separationRadius);
                float weight = (1f - pct) * (1f - pct);
                steer += away.normalized * weight;
                accumulatedWeight += weight;
            }
        }
        return steer.normalized * Mathf.Clamp01(accumulatedWeight);
    }

    Vector3 FindEscapeDirection()
    {
        float minDanger = 1.0f;
        Vector3 escapeDir = -transform.forward;
        for (int i = 0; i < resolution; i++)
        {
            if (dangerMap[i] < minDanger) { minDanger = dangerMap[i]; escapeDir = rayDirections[i]; }
        }
        return escapeDir;
    }

    float GetMaxDangerInDirection(Vector3 direction)
    {
        float maxDanger = 0f;
        direction.y = 0f; direction.Normalize();
        for (int i = 0; i < resolution; i++)
        {
            if (Vector3.Angle(rayDirections[i], direction) < 30f)
                if (dangerMap[i] > maxDanger) maxDanger = dangerMap[i];
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

            // Capture normal for sliding logic
            Vector3 avgNormal = Vector3.zero;
            foreach (ContactPoint contact in collision.contacts) avgNormal += contact.normal;
            if (avgNormal.sqrMagnitude > 0.001f) lastCollisionNormal = avgNormal.normalized;
            lastCollisionNormal.y = 0f;

            // Apply standard physics bounce
            rb.AddForce(lastCollisionNormal * contactRepulsionForce, ForceMode.Acceleration);
        }
    }

    // Unused but required
    void HandleCombat(float bestScore) { }

    // ================= PUBLIC API =================
    public void SetWaypoint(Vector3 position, WaypointPriority priority = WaypointPriority.Low)
    {
        if (priority >= currentPriority)
        {
            currentGoal = position; hasGoal = true; currentPriority = priority; hasNextGoal = false;
            if (priority >= WaypointPriority.High) { smoothedGoal = currentGoal; smoothedGoalVel = Vector3.zero; }
        }
    }
    public void SetWaypointPath(Vector3 waypoint1, Vector3 waypoint2, WaypointPriority priority = WaypointPriority.Low)
    {
        if (priority >= currentPriority)
        {
            currentGoal = waypoint1; nextGoal = waypoint2; hasGoal = true; hasNextGoal = true; currentPriority = priority; nextPriority = priority;
            if (priority >= WaypointPriority.High) { smoothedGoal = currentGoal; smoothedGoalVel = Vector3.zero; }
        }
    }
    public void ClearWaypoint(WaypointPriority priority = WaypointPriority.High)
    {
        if (priority >= currentPriority) { hasGoal = false; hasNextGoal = false; currentPriority = WaypointPriority.None; nextPriority = WaypointPriority.None; }
    }
    public void SetFocusTarget(Transform t) { focusTarget = t; focusPoint = null; }
    public void SetFocusPoint(Vector3 p) { focusTarget = null; focusPoint = p; }
    public void ClearFocusTarget() { focusTarget = null; focusPoint = null; }
    public void SetBoost(bool active, float multiplier = 1.0f) { boostRequested = active; targetBoostMultiplier = active ? Mathf.Max(1f, multiplier) : 1f; }
    public void SetCombatManeuverMode(bool active) { isInCombatManeuver = active; }
    public int GetObstacleCount() => countedObstacles;
    public void ClearFlankingTarget() { }
}
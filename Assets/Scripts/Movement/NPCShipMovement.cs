using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class NPCShipMovement : ShipMovementBase
{
    [Header("Moving Field Settings")]
    public float predictionTime = 1.5f;
    public float dynamicClearance = 2.0f;
    public float baseLookAhead = 60f;

    [Header("Waypoint Smoothing (prevents stop near waypoint)")]
    public float goalSmoothingTime = 0.20f;
    public float passThroughRadius = 55f;
    public float passThroughDistance = 220f;
    [Range(0.1f, 1.0f)]
    public float minThrottleNearGoal = 0.80f;

    [Header("Inertia Feel (WITHOUT becoming slow)")]
    [Tooltip("Multiplies ShipMovementBase.acceleration for AI steering only. Example: accel=8, scale=8.3 => ~66 accel.")]
    public float aiAccelerationScale = 8.3f;

    [Range(0.1f, 1.0f)]
    public float boostStrafeFactor = 0.35f;

    [Range(0.1f, 1.0f)]
    public float normalStrafeFactor = 1.0f;

    [Header("Ship Avoidance / Separation (prevents ramming enemy during maneuvers)")]
    [Tooltip("Layers that represent ships (enemy + friendly). Not asteroids.")]
    public LayerMask shipAvoidanceLayer;

    [Tooltip("Within this radius, we add a repulsion steer away from nearby ships.")]
    public float separationRadius = 35f;

    [Tooltip("How far ahead we treat ships as obstacles in the danger map.")]
    public float separationLookAhead = 65f;

    [Tooltip("How strongly separation steer biases the chosen direction.")]
    public float separationForce = 1.2f;

    [Header("Boost Smoothing")]
    public float boostRampUpRate = 1.6f;
    public float boostRampDownRate = 2.2f;
    public float dragLerpSpeed = 5f;

    [Header("Physical Constraints")]
    public float shipCollisionRadius = 5f;
    public bool enableStrafing = true;

    public float standardDrag = 0.25f;
    public float boostDrag = 0.10f;

    [Header("Combat & Stuck")]
    public ShipTargetingSystem targetingSystem;
    public float stuckThreshold = 0.1f;
    public float contactRepulsionForce = 50f;

    [Header("Sensors")]
    public LayerMask obstacleLayer;
    public int resolution = 72;

    [Header("Flanking Settings (unused by boom&zoom but kept)")]
    public bool lockFacingDuringFlank = true;

    // Flow Field
    public NPCFlowFieldNavigator flowFieldNavigator;
    public bool useFlowFieldNavigation = true;

    // Internal goal
    private Vector3 currentGoal;
    private Vector3 smoothedGoal;
    private Vector3 smoothedGoalVel;
    private bool hasGoal = false;
    private WaypointPriority currentPriority = WaypointPriority.None;

    // Flanking (compat)
    private GameObject flankingTarget;
    private bool isFlankingMode = false;

    // Boost request + smoothed application
    private bool boostRequested = false;
    private float targetBoostMultiplier = 1f;
    private float currentBoostMultiplier = 1f;

    // Keep old fields in case other code reads them
    private bool isBoosting = false;
    private float boostMultiplier = 1f;

    // Smoothed drag
    private float currentDrag;

    // Steering smoothing
    private Vector3 smoothedDir;
    private Vector3 steerVelocity;
    private float stuckTimer = 0f;
    private bool isMining = false;
    private bool isPhysicallyColliding = false;

    // Arrays
    private float[] interestMap;
    private float[] dangerMap;
    private Vector3[] rayDirections;
    private Collider[] nearbyObstacles = new Collider[80]; // slightly larger since we scan ships too

    protected override void Awake()
    {
        base.Awake();

        if (shipCollisionRadius <= 0.1f)
        {
            var col = GetComponent<Collider>();
            shipCollisionRadius = (col != null) ? col.bounds.extents.magnitude : 5f;
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

        currentDrag = standardDrag;
        rb.linearDamping = currentDrag;

        boostRequested = false;
        targetBoostMultiplier = 1f;
        currentBoostMultiplier = 1f;
    }

    void Start()
    {
        if (flowFieldNavigator == null) flowFieldNavigator = GetComponent<NPCFlowFieldNavigator>();

        // Respect your scale if left low in inspector
        if (maxSpeed < 30f) maxSpeed = 100f;
        if (acceleration < 1f) acceleration = 8f;
    }

    void FixedUpdate()
    {
        isPhysicallyColliding = false;

        // Smooth drag transition (prevents abrupt braking feel)
        float targetDrag = boostRequested ? boostDrag : standardDrag;
        currentDrag = Mathf.Lerp(currentDrag, targetDrag, 1f - Mathf.Exp(-dragLerpSpeed * Time.fixedDeltaTime));
        rb.linearDamping = currentDrag;

        UpdateMovement();
    }

    public override void UpdateMovement()
    {
        // Smooth boost multiplier (prevents speed cap jumping instantly)
        float rate = (currentBoostMultiplier < targetBoostMultiplier) ? boostRampUpRate : boostRampDownRate;
        currentBoostMultiplier = Mathf.MoveTowards(currentBoostMultiplier, targetBoostMultiplier, rate * Time.fixedDeltaTime);

        if (!hasGoal)
        {
            // No thrust when no goal; drag will bleed speed.
            return;
        }

        // Smooth goal updates so steering doesn't "snap" at waypoint changes
        smoothedGoal = Vector3.SmoothDamp(smoothedGoal, currentGoal, ref smoothedGoalVel, goalSmoothingTime);

        float currentSpeed = rb.linearVelocity.magnitude;
        float activeLookAhead = baseLookAhead + (currentSpeed * 0.5f);

        Vector3 globalDir = GetGlobalNavigationDirection();
        globalDir.y = 0f;
        if (globalDir.sqrMagnitude < 0.0001f) globalDir = transform.forward;
        globalDir.Normalize();

        // Context maps
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

        // Extra close-range separation steer to prevent ramming ships in merges
        Vector3 sep = ComputeSeparationSteer();
        if (sep != Vector3.zero)
        {
            bestDir = (bestDir + sep * separationForce).normalized;
        }

        HandleCombat(bestScore);

        if (isPhysicallyColliding)
        {
            smoothedDir = transform.forward;
            return;
        }

        float throttle = 1.0f;

        // Near-goal: keep throttle up so it passes through (no "arrive brake")
        float distToGoal = Vector3.Distance(transform.position, smoothedGoal);
        if (distToGoal < passThroughRadius)
            throttle = Mathf.Max(throttle, minThrottleNearGoal);

        if (bestScore < stuckThreshold)
        {
            throttle = 0.5f;
            bestDir = FindEscapeDirection();
        }

        // Smooth heading changes (visual inertia)
        smoothedDir = Vector3.SmoothDamp(smoothedDir, bestDir, ref steerVelocity, 0.28f);

        float currentMaxSpeed = maxSpeed * currentBoostMultiplier;

        if (isFlankingMode && lockFacingDuringFlank && flankingTarget != null && !boostRequested)
        {
            ApplyFlankingMotion(smoothedDir, throttle, currentMaxSpeed);
        }
        else
        {
            ApplyInertialMotion(smoothedDir, throttle, currentMaxSpeed);
        }
    }

    void ApplyFlankingMotion(Vector3 moveDir, float throttle, float speedLimit)
    {
        Vector3 toTarget = flankingTarget.transform.position - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude > 0.1f)
        {
            Quaternion lookRotation = Quaternion.LookRotation(toTarget);
            base.RotateTowards(lookRotation);
        }

        ApplyInertialMotion(moveDir, throttle, speedLimit);
    }

    void ApplyInertialMotion(Vector3 moveDir, float throttle, float speedLimit)
    {
        base.RotateTowards(Quaternion.LookRotation(moveDir));

        // Desired velocity in XZ
        Vector3 desiredVel = moveDir.normalized * (speedLimit * throttle);

        // Current velocity in XZ (Y locked)
        Vector3 v = rb.linearVelocity;
        v.y = 0f;

        float baseAccel = Mathf.Max(0.1f, acceleration);
        float effectiveAccel = baseAccel * Mathf.Max(0.1f, aiAccelerationScale);

        // Reduce lateral authority if boosting (fighter main-engine feel)
        float strafeFactor = enableStrafing ? normalStrafeFactor : 0.2f;
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
            aLat = aLat.normalized * latLimit;

        Vector3 a = aFwd + aLat;
        if (a.magnitude > effectiveAccel)
            a = a.normalized * effectiveAccel;

        Vector3 newVel = v + a * dt;

        rb.linearVelocity = new Vector3(newVel.x, rb.linearVelocity.y, newVel.z);
    }

    Vector3 ComputeSeparationSteer()
    {
        if (shipAvoidanceLayer == 0) return Vector3.zero;

        Collider[] hits = Physics.OverlapSphere(transform.position, separationRadius, shipAvoidanceLayer);
        if (hits == null || hits.Length == 0) return Vector3.zero;

        Vector3 steer = Vector3.zero;
        int count = 0;

        foreach (var h in hits)
        {
            if (h == null) continue;
            if (h.transform.root == transform.root) continue;

            Vector3 away = transform.position - h.bounds.center;
            away.y = 0f;
            float d = away.magnitude;
            if (d < 0.001f) continue;

            float w = 1f - Mathf.Clamp01(d / separationRadius);
            steer += away.normalized * w;
            count++;
        }

        if (count == 0) return Vector3.zero;
        steer /= count;
        return (steer.sqrMagnitude < 0.0001f) ? Vector3.zero : steer.normalized;
    }

    Vector3 GetGlobalNavigationDirection()
    {
        // Pass-through behavior near goal: instead of "arriving", push the goal forward
        Vector3 goalForNav = smoothedGoal;
        Vector3 toGoal = goalForNav - transform.position;
        toGoal.y = 0f;

        if (toGoal.magnitude < passThroughRadius)
        {
            Vector3 f = transform.forward;
            f.y = 0f;
            if (f.sqrMagnitude < 0.0001f) f = Vector3.forward;
            f.Normalize();
            goalForNav = transform.position + f * passThroughDistance;
        }

        if (useFlowFieldNavigation && flowFieldNavigator != null)
        {
            flowFieldNavigator.SetGoal(goalForNav);
            return flowFieldNavigator.GetNavigationDirection(transform.position);
        }

        Vector3 dir = goalForNav - transform.position;
        dir.y = 0f;
        return (dir.sqrMagnitude < 0.0001f) ? transform.forward : dir.normalized;
    }

    void PopulatePredictiveDangerMap(float lookAhead)
    {
        // Scan obstacles + ships (ships get extra “danger” treatment)
        float scanRadius = Mathf.Max(lookAhead, separationLookAhead);
        LayerMask combined = obstacleLayer | shipAvoidanceLayer;

        int count = Physics.OverlapSphereNonAlloc(transform.position, scanRadius, nearbyObstacles, combined);
        int checks = Mathf.Min(count, nearbyObstacles.Length);

        for (int k = 0; k < checks; k++)
        {
            Collider obs = nearbyObstacles[k];
            if (obs == null || obs.transform.root == transform.root) continue;

            bool isShip = ((1 << obs.gameObject.layer) & shipAvoidanceLayer) != 0;

            Vector3 obsVel = Vector3.zero;
            if (obs.attachedRigidbody != null) obsVel = obs.attachedRigidbody.linearVelocity;

            Vector3 currentPos = obs.bounds.center;

            // Base radius from bounds
            float radius = obs.bounds.extents.magnitude;

            // Ships: pretend they're “fatter” and start avoiding sooner
            if (isShip)
            {
                // separationRadius is a *gameplay* avoidance size (not physical collider size)
                radius = Mathf.Max(radius, separationRadius);
            }

            float useLookAhead = isShip ? Mathf.Max(lookAhead, separationLookAhead) : lookAhead;

            if (obsVel.sqrMagnitude > 1.0f && !isShip)
            {
                // Predict moving obstacles (asteroids etc.)
                Vector3 futurePos = currentPos + (obsVel * predictionTime);
                ProcessCapsuleObstacle(currentPos, futurePos, radius, useLookAhead);
            }
            else
            {
                // Ships: treat as “present position” (they’ll be updated each tick anyway)
                ProcessCapsuleObstacle(currentPos, currentPos, radius, useLookAhead);
            }
        }
    }

    void ProcessCapsuleObstacle(Vector3 start, Vector3 end, float radius, float lookAhead)
    {
        float safeRadius = radius + shipCollisionRadius + dynamicClearance;

        for (int i = 0; i < resolution; i++)
        {
            Vector3 rayDir = rayDirections[i];

            float tSeg = DistancePointToSegmentParam(transform.position, start, end);
            Vector3 closestObstaclePoint = Vector3.Lerp(start, end, tSeg);

            Vector3 toObs = closestObstaclePoint - transform.position;
            toObs.y = 0f;
            float dist = toObs.magnitude;

            if (dist > lookAhead) continue;
            if (dist <= 0.001f) continue;
            if (Vector3.Dot(rayDir, toObs.normalized) < 0.2f) continue;

            float angleRad = Mathf.Asin(Mathf.Clamp01(safeRadius / dist));
            float angleDeg = angleRad * Mathf.Rad2Deg;
            float angleToRay = Vector3.Angle(rayDir, toObs.normalized);

            if (angleToRay < angleDeg)
            {
                float weight = 1.0f;
                weight *= (1f - (dist / lookAhead) * 0.5f);
                if (weight > dangerMap[i]) dangerMap[i] = weight;
            }
        }
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
        if (((1 << collision.gameObject.layer) & (obstacleLayer | shipAvoidanceLayer)) != 0)
        {
            isPhysicallyColliding = true;
            Vector3 pushDir = Vector3.zero;
            foreach (ContactPoint contact in collision.contacts) pushDir += contact.normal;
            pushDir.Normalize();
            pushDir.y = 0f;
            rb.AddForce(pushDir * contactRepulsionForce, ForceMode.Acceleration);
        }
    }

    Vector3 FindEscapeDirection()
    {
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

    // === PUBLIC API ===

    public void SetWaypoint(Vector3 position, WaypointPriority priority = WaypointPriority.Low)
    {
        if (priority >= currentPriority)
        {
            currentGoal = position;
            hasGoal = true;
            currentPriority = priority;

            if (smoothedGoal == Vector3.zero || !Application.isPlaying)
                smoothedGoal = currentGoal;
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

    public void SetFlankingTarget(GameObject target)
    {
        flankingTarget = target;
        isFlankingMode = (target != null);
    }

    public void ClearFlankingTarget()
    {
        flankingTarget = null;
        isFlankingMode = false;
    }

    public void SetBoost(bool active, float multiplier = 1.0f)
    {
        boostRequested = active;
        targetBoostMultiplier = active ? Mathf.Max(1f, multiplier) : 1f;

        // compat fields
        isBoosting = active;
        boostMultiplier = multiplier;
    }
}

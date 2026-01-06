using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class NPCShipMovement : ShipMovementBase
{
    [Header("Moving Field Settings")]
    public float predictionTime = 2.5f;
    public float dynamicClearance = 8.0f;
    public float baseLookAhead = 120f;

    [Header("Ship Avoidance (Collision Fix)")]
    [Tooltip("Select layers for Player and other Enemies here.")]
    public LayerMask shipAvoidanceLayer;
    public float separationRadius = 60f;
    public float separationForce = 1.5f;

    [Header("Waypoint Smoothing")]
    public float goalSmoothingTime = 0.20f;

    public float passThroughRadius = 55f;
    public float passThroughDistance = 220f;
    [Range(0.1f, 1.0f)]
    public float minThrottleNearGoal = 0.80f;

    [Header("Inertia Feel")]
    public float aiAccelerationScale = 8.3f;

    [Range(0.1f, 1.0f)]
    public float boostStrafeFactor = 0.35f;

    [Range(0.1f, 1.0f)]
    public float normalStrafeFactor = 1.0f;

    [Header("Physical Constraints")]
    public float shipCollisionRadius = 8f;
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

    [Header("Flanking")]
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

    // Flanking
    private GameObject flankingTarget;
    private bool isFlankingMode = false;

    // Boost
    private bool boostRequested = false;
    private float targetBoostMultiplier = 1f;

    // Physics
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

    // Obstacle density tracking
    private int countedObstacles = 0;

    protected override void Awake()
    {
        base.Awake();

        if (shipCollisionRadius <= 0.1f)
        {
            var col = GetComponent<Collider>();
            shipCollisionRadius = (col != null) ? col.bounds.extents.magnitude : 8f;
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

        if (maxSpeed < 30f) maxSpeed = 100f;
        if (acceleration < 1f) acceleration = 8f;
    }

    void FixedUpdate()
    {
        isPhysicallyColliding = false;
        rb.linearDamping = boostRequested ? boostDrag : standardDrag;
        UpdateMovement();
    }

    public override void UpdateMovement()
    {
        if (!hasGoal) return;

        smoothedGoal = Vector3.SmoothDamp(smoothedGoal, currentGoal, ref smoothedGoalVel, goalSmoothingTime);

        float currentSpeed = rb.linearVelocity.magnitude;

        // Scale look-ahead more aggressively, especially when boosting
        float boostMultiplier = boostRequested ? targetBoostMultiplier : 1.0f;
        float activeLookAhead = baseLookAhead * boostMultiplier + (currentSpeed * 0.8f);

        Vector3 globalDir = GetGlobalNavigationDirection();
        globalDir.y = 0f;
        if (globalDir.sqrMagnitude < 0.0001f) globalDir = transform.forward;
        globalDir.Normalize();

        // 1. Context Maps
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
            // STRICTER danger threshold
            if (dangerMap[i] > 0.4f)
                interestMap[i] = 0f;
            else
                interestMap[i] *= (1.0f - dangerMap[i] * 1.2f);

            if (interestMap[i] > bestScore)
            {
                bestScore = interestMap[i];
                bestDir = rayDirections[i];
            }
        }

        // Separation Logic
        Vector3 separation = ComputeSeparationSteer();
        if (separation != Vector3.zero)
        {
            bestDir = (bestDir + separation * separationForce).normalized;
        }

        HandleCombat(bestScore);

        if (isPhysicallyColliding)
        {
            smoothedDir = transform.forward;
            return;
        }

        float throttle = 1.0f;
        float distToGoal = Vector3.Distance(transform.position, smoothedGoal);

        if (distToGoal < passThroughRadius)
            throttle = Mathf.Max(throttle, minThrottleNearGoal);

        if (bestScore < stuckThreshold)
        {
            throttle = 0.5f;
            bestDir = FindEscapeDirection();
        }

        smoothedDir = Vector3.SmoothDamp(smoothedDir, bestDir, ref steerVelocity, 0.28f);

        float currentMaxSpeed = maxSpeed * targetBoostMultiplier;

        // Reduce boost in dense asteroid fields
        if (boostRequested && countedObstacles > 5)
        {
            float densityReduction = Mathf.Clamp01((countedObstacles - 5) / 10.0f);
            currentMaxSpeed = Mathf.Lerp(currentMaxSpeed, maxSpeed, densityReduction);
        }

        ApplyInertialMotion(smoothedDir, throttle, currentMaxSpeed);
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

            if (d < 0.001f) { steer += Random.onUnitSphere; count++; continue; }

            float w = 1f - Mathf.Clamp01(d / separationRadius);
            steer += away.normalized * w;
            count++;
        }

        if (count == 0) return Vector3.zero;
        steer /= count;
        steer.y = 0f;
        return steer.normalized;
    }

    void ApplyInertialMotion(Vector3 moveDir, float throttle, float speedLimit)
    {
        base.RotateTowards(Quaternion.LookRotation(moveDir));

        Vector3 desiredVel = moveDir.normalized * (speedLimit * throttle);
        Vector3 v = rb.linearVelocity;
        v.y = 0f;

        float baseAccel = Mathf.Max(0.1f, acceleration);
        float effectiveAccel = baseAccel * Mathf.Max(0.1f, aiAccelerationScale);

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
        if (aLat.magnitude > latLimit) aLat = aLat.normalized * latLimit;

        Vector3 a = aFwd + aLat;
        if (a.magnitude > effectiveAccel) a = a.normalized * effectiveAccel;

        Vector3 newVel = v + a * dt;
        rb.linearVelocity = new Vector3(newVel.x, rb.linearVelocity.y, newVel.z);
    }

    Vector3 GetGlobalNavigationDirection()
    {
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
        int count = Physics.OverlapSphereNonAlloc(transform.position, lookAhead, nearbyObstacles, obstacleLayer);
        int checks = Mathf.Min(count, nearbyObstacles.Length);

        countedObstacles = checks;

        // Get our predicted position
        Vector3 ourFuturePos = transform.position + rb.linearVelocity * predictionTime;

        for (int k = 0; k < checks; k++)
        {
            Collider obs = nearbyObstacles[k];
            if (obs == null || obs.transform.root == transform.root) continue;

            Vector3 obsVel = Vector3.zero;
            if (obs.attachedRigidbody != null) obsVel = obs.attachedRigidbody.linearVelocity;

            Vector3 currentPos = obs.bounds.center;
            float radius = obs.bounds.extents.magnitude;

            // Check both current AND predicted positions
            if (obsVel.sqrMagnitude > 1.0f)
            {
                Vector3 futurePos = currentPos + (obsVel * predictionTime);
                ProcessCapsuleObstacle(currentPos, futurePos, radius, lookAhead);
            }
            else
            {
                ProcessCapsuleObstacle(currentPos, currentPos, radius, lookAhead);
            }

            // ADDITIONAL: Check collision with our future position
            float futureCollisionDist = Vector3.Distance(ourFuturePos, currentPos + obsVel * predictionTime);
            if (futureCollisionDist < radius + shipCollisionRadius + dynamicClearance)
            {
                // Mark all directions toward this obstacle as dangerous
                Vector3 toObs = (currentPos - transform.position).normalized;
                for (int i = 0; i < resolution; i++)
                {
                    if (Vector3.Dot(rayDirections[i], toObs) > 0.3f)
                    {
                        dangerMap[i] = Mathf.Max(dangerMap[i], 0.9f);
                    }
                }
            }
        }
    }

    void ProcessCapsuleObstacle(Vector3 start, Vector3 end, float radius, float lookAhead)
    {
        // Scale clearance with boost
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

            if (dist > lookAhead) continue;
            if (dist <= 0.001f) continue;

            // RELAXED angle check - detect obstacles at wider angles
            if (Vector3.Dot(rayDir, toObs.normalized) < -0.1f) continue;

            float angleRad = Mathf.Asin(Mathf.Clamp01(safeRadius / dist));
            float angleDeg = angleRad * Mathf.Rad2Deg;
            float angleToRay = Vector3.Angle(rayDir, toObs.normalized);

            if (angleToRay < angleDeg)
            {
                // STRONGER danger weighting - closer obstacles = much more dangerous
                float distFactor = 1.0f - (dist / lookAhead);
                float weight = Mathf.Pow(distFactor, 0.7f);

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
        int combinedMask = obstacleLayer | shipAvoidanceLayer;

        if (((1 << collision.gameObject.layer) & combinedMask) != 0)
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
            if (smoothedGoal == Vector3.zero || !Application.isPlaying) smoothedGoal = currentGoal;
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
    }

    // Public accessor for obstacle count (useful for debugging)
    public int GetObstacleCount() => countedObstacles;
}

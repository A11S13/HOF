using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class NPCShipMovement : ShipMovementBase
{
    [Header("Moving Field Settings")]
    public float predictionTime = 1.5f;
    public float dynamicClearance = 2.0f;
    public float baseLookAhead = 60f;

    [Header("Physical Constraints")]
    public float shipCollisionRadius = 5f;
    public bool enableStrafing = true;
    [Tooltip("Drag applied when NOT boosting to prevent overshooting drift.")]
    public float standardDrag = 1.0f;
    [Tooltip("Drag applied when boosting.")]
    public float boostDrag = 0.2f;

    [Header("Combat & Stuck")]
    public ShipTargetingSystem targetingSystem;
    public float stuckThreshold = 0.1f;
    public float contactRepulsionForce = 50f;

    [Header("Sensors")]
    public LayerMask obstacleLayer;
    public int resolution = 72;

    [Header("Flanking Settings")]
    public bool lockFacingDuringFlank = true;

    // Flow Field
    public NPCFlowFieldNavigator flowFieldNavigator;
    public bool useFlowFieldNavigation = true;

    // Internal
    private Vector3 currentGoal;
    private bool hasGoal = false;
    private WaypointPriority currentPriority = WaypointPriority.None;

    // Flanking & Boost State
    private GameObject flankingTarget;
    private bool isFlankingMode = false;
    private bool isBoosting = false;
    private float boostMultiplier = 1f;

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

        if (shipCollisionRadius <= 0.1f)
        {
            var col = GetComponent<Collider>();
            if (col != null) shipCollisionRadius = col.bounds.extents.magnitude;
            else shipCollisionRadius = 5f;
        }

        interestMap = new float[resolution];
        dangerMap = new float[resolution];
        rayDirections = new Vector3[resolution];

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
        if (maxSpeed < 30) maxSpeed = 45f;
        if (acceleration < 10) acceleration = 20f;
    }

    void FixedUpdate()
    {
        isPhysicallyColliding = false;

        // Dynamic Drag Control (Prevent overshoot)
        rb.linearDamping = isBoosting ? boostDrag : standardDrag;

        UpdateMovement();
    }

    public override void UpdateMovement()
    {
        if (!hasGoal)
        {
            ApplyForce(Vector3.MoveTowards(rb.linearVelocity, Vector3.zero, Time.fixedDeltaTime * 10f));
            return;
        }

        float currentSpeed = rb.linearVelocity.magnitude;
        float activeLookAhead = baseLookAhead + (currentSpeed * 0.5f);

        Vector3 globalDir = GetGlobalNavigationDirection();
        globalDir.y = 0;
        globalDir.Normalize();

        // 1. Context Maps (Interest/Danger)
        System.Array.Clear(interestMap, 0, resolution);
        System.Array.Clear(dangerMap, 0, resolution);

        for (int i = 0; i < resolution; i++)
        {
            float dot = Vector3.Dot(rayDirections[i], globalDir);
            interestMap[i] = Mathf.Max(0, dot);
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

        HandleCombat(bestScore);

        if (isPhysicallyColliding)
        {
            smoothedDir = transform.forward;
        }
        else
        {
            float throttle = 1.0f;

            if (bestScore < stuckThreshold)
            {
                throttle = 0.5f;
                bestDir = FindEscapeDirection();
            }

            // Smooth direction
            smoothedDir = Vector3.SmoothDamp(smoothedDir, bestDir, ref steerVelocity, 0.25f);

            // Determine effective max speed
            float currentMaxSpeed = isBoosting ? maxSpeed * boostMultiplier : maxSpeed;

            // Mode Selection:
            if (isFlankingMode && lockFacingDuringFlank && flankingTarget != null && !isBoosting)
            {
                ApplyFlankingPhysics(smoothedDir, throttle, currentMaxSpeed);
            }
            else
            {
                ApplyXZPhysics(smoothedDir, throttle, currentMaxSpeed);
            }
        }
    }

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

            if (obsVel.sqrMagnitude > 1.0f)
            {
                Vector3 futurePos = currentPos + (obsVel * predictionTime);
                ProcessCapsuleObstacle(currentPos, futurePos, radius, lookAhead);
            }
            else
            {
                ProcessCapsuleObstacle(currentPos, currentPos, radius, lookAhead);
            }
        }
    }

    void ProcessCapsuleObstacle(Vector3 start, Vector3 end, float radius, float lookAhead)
    {
        float safeRadius = radius + shipCollisionRadius + dynamicClearance;

        for (int i = 0; i < resolution; i++)
        {
            Vector3 rayDir = rayDirections[i];

            float t_seg = DistancePointToSegmentParam(transform.position, start, end);
            Vector3 closestObstaclePoint = Vector3.Lerp(start, end, t_seg);

            Vector3 toObs = closestObstaclePoint - transform.position;
            toObs.y = 0;
            float dist = toObs.magnitude;

            if (dist > lookAhead) continue;
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

    // === MOVEMENT PHYSICS ===

    void ApplyFlankingPhysics(Vector3 moveDir, float throttle, float speedLimit)
    {
        // Face the target
        Vector3 toTarget = flankingTarget.transform.position - transform.position;
        toTarget.y = 0;

        if (toTarget.sqrMagnitude > 0.1f)
        {
            Quaternion lookRotation = Quaternion.LookRotation(toTarget);
            base.RotateTowards(lookRotation);
        }

        // Strafe
        Vector3 targetVel = moveDir * (speedLimit * throttle);
        base.ApplyForce(targetVel);
    }

    void ApplyXZPhysics(Vector3 moveDir, float throttle, float speedLimit)
    {
        base.RotateTowards(Quaternion.LookRotation(moveDir));
        Vector3 targetVel = moveDir * (speedLimit * throttle);

        if (enableStrafing && !isBoosting)
            base.ApplyForce(targetVel);
        else
        {
            // Main thruster only
            float align = Vector3.Dot(transform.forward, moveDir);
            if (align > 0) base.ApplyForce(transform.forward * (speedLimit * throttle * align));
        }
    }

    void OnCollisionStay(Collision collision)
    {
        if (((1 << collision.gameObject.layer) & obstacleLayer) != 0)
        {
            isPhysicallyColliding = true;
            Vector3 pushDir = Vector3.zero;
            foreach (ContactPoint contact in collision.contacts)
            {
                pushDir += contact.normal;
            }
            pushDir.Normalize();
            pushDir.y = 0;
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

    Vector3 GetGlobalNavigationDirection()
    {
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

    // === PUBLIC API ===

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
        isBoosting = active;
        boostMultiplier = multiplier;
    }

    void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
            Gizmos.color = isBoosting ? Color.green : Color.cyan;
            Gizmos.DrawLine(transform.position, transform.position + smoothedDir * 10f);

            if (isFlankingMode && flankingTarget != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, flankingTarget.transform.position);
            }
        }
    }
}

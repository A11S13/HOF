using UnityEngine;

public class NPCBehavior : MonoBehaviour
{
    [Header("References")]
    private NPCShipMovement movement;
    private ShipCore shipCore;
    private ShipTargetingSystem targeting;

    [Header("Behavior Settings")]
    public AIBehaviorMode behaviorMode = AIBehaviorMode.Wander;

    [Header("Combat Tactics")]
    [Tooltip("Distance to start an attack run.")]
    public float attackInitiateRange = 150f;

    [Tooltip("Target point behind enemy. Set HIGH (120+) to ensure ship doesn't slow down at the end.")]
    public float overshootDistance = 120f;

    [Tooltip("Distance past the enemy where we actually break off.")]
    public float breakTriggerDistance = 80f;

    [Tooltip("Sideways spacing. 45 is safe for 5-unit ships.")]
    public float strafeOffsetDistance = 45f;

    [Tooltip("Time between attack runs.")]
    public float attackCooldown = 5f;

    [Tooltip("How long to stay in 'Break' mode after an attack.")]
    public float breakDuration = 2.5f;

    [Tooltip("General engagement radar range.")]
    public float engagementRange = 600f;

    [Header("Dogfight Settings (Circling)")]
    public float baseCirclingRadius = 60f;
    public float circlingSpeed = 50f;
    public float radiusOscillation = 20f;
    public float oscillationFreq = 1.5f;

    [Header("Diagram Flanking v2")]
    [Tooltip("The 'Safe' distance for drifting. Ship will try to maintain this.")]
    public float flankingBaseRadius = 75f;

    [Tooltip("How far OUT the ship pushes during Forsage (Base + This).")]
    public float forsageRadiusExtension = 50f;

    [Tooltip("How much of the circle to traverse during DRIFT. Higher = Longer Drift.")]
    public float driftAngleStep = 110f;

    [Tooltip("How much of the circle to traverse during FORSAGE. Lower = Quick burst outward.")]
    public float forsageAngleStep = 60f;

    [Tooltip("Distance threshold to switch to next leg.")]
    public float waypointReachThreshold = 20f;

    [Tooltip("Speed multiplier during Forsage segments.")]
    public float forsageSpeedMultiplier = 1.6f;

    [Tooltip("If closer than this, force Forsage to get away.")]
    public float tooCloseSafetyThreshold = 40f;

    [Header("Wander Settings")]
    public float wanderRadius = 400f;
    public float minWanderDistance = 20f;
    public float maxWanderDistance = 800f;
    public float waypointReachDistance = 10f;
    public float wanderWaitTime = 0f;

    // Internal State
    private Vector3 wanderCenter;
    private Vector3 currentWanderTarget;
    private float wanderWaitTimer = 0f;
    private bool isWaiting = false;

    // Combat State
    private GameObject currentEnemy;
    private AIBehaviorMode currentState;
    private AIBehaviorMode previousState;
    private float stateEnterTime;

    // Attack Run Logic
    private float lastAttackTime;
    private bool committedToRun = false;
    private Vector3 cachedAttackDir;
    private Vector3 cachedStrafeOffset;

    // Circle Logic
    private Vector3 circlingCenter;
    private float circlingAngle;
    private float lastCircleUpdateTime;

    // Flanking State
    private enum FlankSubState { PickNewLeg, Forsage, Drifting }
    private FlankSubState currentFlankState = FlankSubState.PickNewLeg;
    private float currentFlankingAngle;
    private float currentFlankingDirection = 1f; // 1 = CW, -1 = CCW
    private Vector3 currentFlankWaypoint;
    private bool isForsageLeg = false;

    void Awake()
    {
        movement = GetComponent<NPCShipMovement>();
        shipCore = GetComponentInChildren<ShipCore>();
        targeting = GetComponentInChildren<ShipTargetingSystem>();

        wanderCenter = transform.position;
        currentState = behaviorMode;
        previousState = behaviorMode;
        stateEnterTime = Time.time;
        lastAttackTime = Time.time;
    }

    void Update()
    {
        if (movement == null || shipCore == null) return;
        UpdateBehavior();
    }

    void UpdateBehavior()
    {
        currentEnemy = FindNearestEnemy();
        AIBehaviorMode desiredState = DetermineTacticalState();

        if (desiredState != currentState)
        {
            bool locked = false;

            if (currentState == AIBehaviorMode.AttackRun)
            {
                if (currentEnemy != null && desiredState == AIBehaviorMode.AttackRun)
                    locked = true;
            }
            else if (currentState == AIBehaviorMode.EvasiveBreak)
            {
                if (Time.time - stateEnterTime < breakDuration) locked = true;
            }

            if (currentEnemy == null) locked = false;

            if (!locked)
            {
                ChangeState(desiredState);
            }
        }

        switch (currentState)
        {
            case AIBehaviorMode.Wander: DoWander(); break;
            case AIBehaviorMode.Approach: DoApproach(); break;
            case AIBehaviorMode.Dogfight: DoDogfight(); break;
            case AIBehaviorMode.Flanking: DoFlanking(); break;
            case AIBehaviorMode.AttackRun: DoAttackRun(); break;
            case AIBehaviorMode.EvasiveBreak: DoEvasiveBreak(); break;
            case AIBehaviorMode.Retreat: DoRetreat(); break;
            case AIBehaviorMode.Hold: DoHold(); break;
        }
    }

    void ChangeState(AIBehaviorMode newState)
    {
        previousState = currentState;
        currentState = newState;
        stateEnterTime = Time.time;

        movement.ClearWaypoint(WaypointPriority.Critical);
        movement.SetBoost(false);

        switch (currentState)
        {
            case AIBehaviorMode.AttackRun:
                committedToRun = false;
                movement.ClearFlankingTarget();
                break;

            case AIBehaviorMode.Dogfight:
                InitializeCircling();
                movement.ClearFlankingTarget();
                break;

            case AIBehaviorMode.Flanking:
                InitializeFlanking();
                break;

            case AIBehaviorMode.Wander:
                currentWanderTarget = Vector3.zero;
                movement.ClearFlankingTarget();
                break;

            default:
                movement.ClearFlankingTarget();
                break;
        }
    }

    AIBehaviorMode DetermineTacticalState()
    {
        if (currentEnemy == null) return behaviorMode;

        float dist = Vector3.Distance(transform.position, currentEnemy.transform.position);

        if (dist > engagementRange) return behaviorMode;

        switch (currentState)
        {
            case AIBehaviorMode.Approach:
                if (dist < attackInitiateRange)
                {
                    if (Time.time - lastAttackTime > attackCooldown)
                        return AIBehaviorMode.AttackRun;
                    else
                        return Random.value > 0.5f ? AIBehaviorMode.Flanking : AIBehaviorMode.Dogfight;
                }
                return AIBehaviorMode.Approach;

            case AIBehaviorMode.Dogfight:
            case AIBehaviorMode.Flanking:
                if (dist > attackInitiateRange * 1.5f) return AIBehaviorMode.Approach;

                if (Time.time - lastAttackTime > attackCooldown && dist < attackInitiateRange)
                    return AIBehaviorMode.AttackRun;

                if (Time.time - stateEnterTime > 15f)
                {
                    // Refresh randomness occasionally
                    return Random.value > 0.5f ? AIBehaviorMode.Flanking : AIBehaviorMode.Dogfight;
                }

                return currentState;

            case AIBehaviorMode.AttackRun:
                Vector3 toEnemy = currentEnemy.transform.position - transform.position;
                float dot = Vector3.Dot(transform.forward, toEnemy.normalized);

                if (dot < 0 && dist > breakTriggerDistance)
                    return AIBehaviorMode.EvasiveBreak;

                if (Time.time - stateEnterTime > 5f)
                    return AIBehaviorMode.EvasiveBreak;

                return AIBehaviorMode.AttackRun;

            case AIBehaviorMode.EvasiveBreak:
                if (Time.time - stateEnterTime > breakDuration)
                {
                    lastAttackTime = Time.time;
                    return AIBehaviorMode.Flanking;
                }
                return AIBehaviorMode.EvasiveBreak;

            default:
                return AIBehaviorMode.Approach;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // FLANKING (DIAGRAM IMPLEMENTATION v2)
    // ═══════════════════════════════════════════════════════════

    void InitializeFlanking()
    {
        if (currentEnemy == null) return;

        // Calculate current angle relative to enemy
        Vector3 toShip = transform.position - currentEnemy.transform.position;
        currentFlankingAngle = Mathf.Atan2(toShip.z, toShip.x) * Mathf.Rad2Deg;

        // Randomize direction
        currentFlankingDirection = Random.value > 0.5f ? 1f : -1f;

        // Start fresh
        currentFlankState = FlankSubState.PickNewLeg;
        isForsageLeg = false;

        Debug.Log($"[{gameObject.name}] Started Flanking Loop. Dir: {currentFlankingDirection}");
    }

    void DoFlanking()
    {
        if (currentEnemy == null)
        {
            movement.ClearFlankingTarget();
            movement.SetBoost(false);
            return;
        }

        Vector3 enemyPos = GetPredictedEnemyPosition();
        float distToEnemy = Vector3.Distance(transform.position, enemyPos);

        // SAFETY: If too close, force a Forsage-like break outward
        if (distToEnemy < tooCloseSafetyThreshold && currentFlankState == FlankSubState.Drifting)
        {
            currentFlankState = FlankSubState.PickNewLeg;
            isForsageLeg = false; // Will flip to True (Forsage) immediately below
        }

        // STATE 1: Pick New Leg
        if (currentFlankState == FlankSubState.PickNewLeg)
        {
            isForsageLeg = !isForsageLeg; // Toggle logic

            // Determine Target Radius
            float targetRadius = flankingBaseRadius;
            float angleStep = driftAngleStep;

            if (isForsageLeg)
            {
                // FORSAGE: Go Out and Away
                targetRadius = flankingBaseRadius + forsageRadiusExtension;
                angleStep = forsageAngleStep;

                // Configure Movement: Fast, face forward
                currentFlankState = FlankSubState.Forsage;
                movement.ClearFlankingTarget();
                movement.SetBoost(true, forsageSpeedMultiplier);
            }
            else
            {
                // DRIFT: Return to Orbit, Slide Long
                targetRadius = flankingBaseRadius;
                angleStep = driftAngleStep;

                // Configure Movement: Normal speed, face enemy
                currentFlankState = FlankSubState.Drifting;
                movement.SetFlankingTarget(currentEnemy);
                movement.SetBoost(false);
            }

            // Calculate Angle
            currentFlankingAngle += angleStep * currentFlankingDirection;

            // Compute Waypoint
            float rad = currentFlankingAngle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(rad) * targetRadius, 0, Mathf.Sin(rad) * targetRadius);
            currentFlankWaypoint = enemyPos + offset;
            currentFlankWaypoint.y = enemyPos.y;
        }

        // STATE 2: Execution & Updates
        // Update Waypoint position as enemy moves (Orbiting logic)
        float currentTargetRad = isForsageLeg ? (flankingBaseRadius + forsageRadiusExtension) : flankingBaseRadius;
        float radUpdate = currentFlankingAngle * Mathf.Deg2Rad;
        Vector3 dynamicOffset = new Vector3(Mathf.Cos(radUpdate) * currentTargetRad, 0, Mathf.Sin(radUpdate) * currentTargetRad);
        currentFlankWaypoint = enemyPos + dynamicOffset;

        movement.SetWaypoint(currentFlankWaypoint, WaypointPriority.High);

        // Check Completion
        float distToWaypoint = Vector3.Distance(transform.position, currentFlankWaypoint);

        // Use a larger tolerance for Forsage to keep it fluid, tighter for Drift
        float tolerance = isForsageLeg ? waypointReachThreshold * 1.5f : waypointReachThreshold;

        if (distToWaypoint < tolerance)
        {
            currentFlankState = FlankSubState.PickNewLeg;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // STANDARD MANEUVERS
    // ═══════════════════════════════════════════════════════════

    void DoApproach()
    {
        if (currentEnemy == null) return;
        Vector3 target = currentEnemy.transform.position;
        movement.SetWaypoint(target - (target - transform.position).normalized * 20f, WaypointPriority.High);
    }

    void DoAttackRun()
    {
        if (currentEnemy == null) return;

        Vector3 currentTargetPos = GetPredictedEnemyPosition();

        if (!committedToRun)
        {
            cachedAttackDir = (currentTargetPos - transform.position).normalized;
            Vector3 strafeDir = Vector3.Cross(cachedAttackDir, Vector3.up).normalized;
            float sideSign = (Random.value > 0.5f) ? 1f : -1f;
            cachedStrafeOffset = strafeDir * sideSign * strafeOffsetDistance;
            committedToRun = true;
        }

        Vector3 flyThroughPoint = currentTargetPos + (cachedAttackDir * overshootDistance) + cachedStrafeOffset;
        flyThroughPoint.y = currentTargetPos.y;

        movement.SetWaypoint(flyThroughPoint, WaypointPriority.Critical);
        movement.SetBoost(true, 1.3f);
    }

    void DoEvasiveBreak()
    {
        if (currentEnemy == null) return;

        Vector3 toEnemy = (currentEnemy.transform.position - transform.position).normalized;
        Vector3 right = Vector3.Cross(toEnemy, Vector3.up);

        Vector3 myRelPos = transform.position - currentEnemy.transform.position;
        float sideCheck = Vector3.Dot(myRelPos, right);
        float sideSign = (sideCheck > 0) ? 1f : -1f;

        Vector3 breakDir = (right * sideSign) + (transform.forward * 0.5f);
        breakDir.y = 0f;
        breakDir.Normalize();

        Vector3 breakPoint = transform.position + (breakDir * 100f);
        movement.SetWaypoint(breakPoint, WaypointPriority.Critical);
        movement.SetBoost(true, 1.5f);
    }

    void InitializeCircling()
    {
        if (currentEnemy == null) return;
        circlingCenter = currentEnemy.transform.position;
        Vector3 toShip = transform.position - circlingCenter;
        circlingAngle = Mathf.Atan2(toShip.z, toShip.x) * Mathf.Rad2Deg;
        lastCircleUpdateTime = 0f;
    }

    void DoDogfight()
    {
        if (currentEnemy == null) return;

        circlingAngle += circlingSpeed * Time.deltaTime;

        if (Time.time - lastCircleUpdateTime >= 0.2f)
        {
            lastCircleUpdateTime = Time.time;
            Vector3 targetPos = GetPredictedEnemyPosition();

            float dynamicRadius = baseCirclingRadius + Mathf.Sin(Time.time * oscillationFreq) * radiusOscillation;
            float angleRad = circlingAngle * Mathf.Deg2Rad;

            Vector3 offset = new Vector3(
                Mathf.Cos(angleRad) * dynamicRadius,
                0f,
                Mathf.Sin(angleRad) * dynamicRadius
            );

            movement.SetWaypoint(targetPos + offset, WaypointPriority.High);
        }
    }

    void DoWander()
    {
        if (isWaiting)
        {
            wanderWaitTimer -= Time.deltaTime;
            if (wanderWaitTimer <= 0f)
            {
                isWaiting = false;
                currentWanderTarget = Vector3.zero;
            }
            else
            {
                movement.ClearWaypoint(WaypointPriority.Low);
                return;
            }
        }

        if (currentWanderTarget == Vector3.zero || Vector3.Distance(transform.position, currentWanderTarget) < waypointReachDistance)
        {
            if (wanderWaitTime > 0f && currentWanderTarget != Vector3.zero)
            {
                isWaiting = true;
                wanderWaitTimer = wanderWaitTime;
                movement.ClearWaypoint(WaypointPriority.Low);
                return;
            }
            currentWanderTarget = GetRandomWanderPoint();
        }
        movement.SetWaypoint(currentWanderTarget, WaypointPriority.Low);
    }

    Vector3 GetRandomWanderPoint()
    {
        float distance = Random.Range(minWanderDistance, maxWanderDistance);
        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector3 offset = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
        Vector3 newPoint = wanderCenter + offset;

        if ((newPoint - wanderCenter).magnitude > wanderRadius)
        {
            newPoint = wanderCenter + (newPoint - wanderCenter).normalized * wanderRadius;
        }

        return newPoint;
    }

    void DoRetreat()
    {
        if (currentEnemy == null) return;
        Vector3 away = (transform.position - currentEnemy.transform.position).normalized;
        movement.SetWaypoint(transform.position + away * 60f, WaypointPriority.Critical);
        movement.SetBoost(true, 1.2f);
    }

    void DoHold()
    {
        movement.ClearWaypoint(WaypointPriority.Medium);
    }

    // ═══════════════════════════════════════════════════════════
    // UTILITY
    // ═══════════════════════════════════════════════════════════

    Vector3 GetPredictedEnemyPosition()
    {
        if (currentEnemy == null) return transform.position;
        Rigidbody rb = currentEnemy.GetComponent<Rigidbody>();
        if (rb != null) return currentEnemy.transform.position + rb.linearVelocity * 0.75f;
        return currentEnemy.transform.position;
    }

    GameObject FindNearestEnemy()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, engagementRange);
        GameObject best = null;
        float minDist = float.MaxValue;
        var myAlign = GetComponent<Alignment>();
        bool iHaveAlign = myAlign != null;

        foreach (var col in colliders)
        {
            if (col.transform.root == transform.root) continue;
            var targetAlign = col.GetComponent<Alignment>();
            if (targetAlign == null) continue;
            if (iHaveAlign && targetAlign.isFriendly == myAlign.isFriendly) continue;

            float d = Vector3.Distance(transform.position, col.transform.position);
            if (d < minDist)
            {
                minDist = d;
                best = col.gameObject;
            }
        }
        return best;
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        if (currentEnemy != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, currentEnemy.transform.position);

            if (currentState == AIBehaviorMode.Flanking)
            {
                Gizmos.color = isForsageLeg ? Color.green : Color.yellow;
                Gizmos.DrawWireSphere(currentFlankWaypoint, 4f);
                Gizmos.DrawLine(transform.position, currentFlankWaypoint);

                // Draw arc
                if (!isForsageLeg)
                {
                    Gizmos.color = new Color(1, 0.9f, 0.2f, 0.3f);
                    Gizmos.DrawWireSphere(currentEnemy.transform.position, flankingBaseRadius);
                }
            }
        }
    }
}

public enum AIBehaviorMode { Wander, Approach, Dogfight, Flanking, AttackRun, EvasiveBreak, Retreat, Hold }
public enum WaypointPriority { None = 0, Low = 1, Medium = 2, High = 3, Critical = 4 }

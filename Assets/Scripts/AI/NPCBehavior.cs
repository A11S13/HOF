using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum NPCClass
{
    Fighter,
    Capital
}

public enum FighterManeuver
{
    ApproachRun,
    AttackPass,
    BreakAway,
    Reposition
}

public class NPCBehavior : MonoBehaviour
{
    [Header("References")]
    private NPCShipMovement movement;
    private ShipCore shipCore;

    [Header("Class Settings")]
    public NPCClass shipClass = NPCClass.Fighter;
    public AIBehaviorMode behaviorMode = AIBehaviorMode.Wander;

    [Header("Engagement")]
    public float engagementRange = 600f;
    public float disengageRange = 850f;

    [Header("Fighter Boom & Zoom")]
    [Tooltip("Distance to start attack run")]
    public float attackRunDistance = 450f;
    [Tooltip("How close to get during attack pass")]
    public float closingDistance = 80f;
    [Tooltip("Distance past target to fly after attack")]
    public float overrunDistance = 220f;
    [Tooltip("Radius of repositioning arc")]
    public float repositionRadius = 320f;
    [Tooltip("Time to wait before starting next attack run")]
    public float repositionTime = 1.2f;
    [Tooltip("Boost multiplier during attack runs")]
    public float attackBoostMultiplier = 1.8f;
    [Tooltip("Random angle variation for attack approaches (degrees)")]
    public float approachAngleVariation = 60f;

    [Header("Orbit Settings (Capital/Cruiser)")]
    public float orbitRange = 350f;
    public float orbitCorrectionFactor = 0.5f;

    [Header("Boost Settings")]
    public float approachBoostMultiplier = 1.25f;

    [Header("Wander Settings")]
    public float wanderRadius = 400f;
    public float minWanderDistance = 20f;
    public float maxWanderDistance = 800f;
    public float waypointReachDistance = 15f;
    public float wanderWaitTime = 2f;

    // Internal
    private Vector3 wanderCenter;
    private Vector3 currentWanderTarget;
    private float wanderWaitTimer = 0f;
    private bool isWaiting = false;

    private GameObject currentEnemy;
    private AIBehaviorMode currentState;
    private AIBehaviorMode previousState;

    // Fighter Maneuver State
    private FighterManeuver currentManeuver = FighterManeuver.ApproachRun;
    private Vector3 attackRunStartPoint;
    private Vector3 breakAwayTarget;
    private float repositionTimer = 0f;
    private float currentApproachAngle = 0f;
    private bool hasReachedApproachPoint = false;

    // Orbit logic
    private bool orbitClockwise = true;
    private float orbitChangeTimer = 0f;

    void Awake()
    {
        movement = GetComponent<NPCShipMovement>();
        shipCore = GetComponentInChildren<ShipCore>();
        wanderCenter = transform.position;
        currentState = behaviorMode;
        orbitClockwise = Random.value > 0.5f;
        currentApproachAngle = Random.Range(0f, 360f);
    }

    void Update()
    {
        if (movement == null) return;
        UpdateBehavior();
    }

    void UpdateBehavior()
    {
        currentEnemy = FindNearestEnemy();
        AIBehaviorMode desiredState = DetermineTacticalState();

        if (desiredState != currentState)
        {
            ChangeState(desiredState);
        }

        switch (currentState)
        {
            case AIBehaviorMode.Wander:
                DoWander();
                break;
            case AIBehaviorMode.Approach:
                if (shipClass == NPCClass.Capital) DoOrbit();
                else DoBoomAndZoom();
                break;
            case AIBehaviorMode.Retreat:
                DoRetreat();
                break;
            case AIBehaviorMode.Hold:
                DoHold();
                break;
        }
    }

    void ChangeState(AIBehaviorMode newState)
    {
        previousState = currentState;
        currentState = newState;

        movement.ClearWaypoint(WaypointPriority.Critical);
        movement.SetBoost(false);
        movement.SetCombatManeuverMode(false);
        movement.ClearFlankingTarget();
        movement.ClearFocusTarget();

        if (currentState == AIBehaviorMode.Wander)
        {
            currentWanderTarget = Vector3.zero;
        }
        else if (currentState == AIBehaviorMode.Approach && shipClass == NPCClass.Fighter)
        {
            currentManeuver = FighterManeuver.ApproachRun;
            currentApproachAngle = Random.Range(-approachAngleVariation, approachAngleVariation);
            hasReachedApproachPoint = false;
        }
    }

    AIBehaviorMode DetermineTacticalState()
    {
        if (currentEnemy == null) return behaviorMode;
        float dist = Vector3.Distance(transform.position, currentEnemy.transform.position);
        if (dist > disengageRange) return behaviorMode;
        return AIBehaviorMode.Approach;
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    float LockY() => transform.position.y;

    // ----------------------------------------------------------
    // BOOM & ZOOM FIGHTER BEHAVIOR
    // ----------------------------------------------------------
    void DoBoomAndZoom()
    {
        if (currentEnemy == null)
        {
            ChangeState(AIBehaviorMode.Wander);
            return;
        }

        // CRITICAL: Tell movement we're in combat mode (no auto-braking)
        movement.SetCombatManeuverMode(true);

        Vector3 targetPos = currentEnemy.transform.position;
        targetPos.y = LockY();
        Vector3 myPos = transform.position;
        myPos.y = LockY();

        float distToTarget = Vector3.Distance(myPos, targetPos);

        switch (currentManeuver)
        {
            case FighterManeuver.ApproachRun:
                DoApproachRun(targetPos, myPos, distToTarget);
                break;

            case FighterManeuver.AttackPass:
                DoAttackPass(targetPos, myPos, distToTarget);
                break;

            case FighterManeuver.BreakAway:
                DoBreakAway(targetPos, myPos, distToTarget);
                break;

            case FighterManeuver.Reposition:
                DoReposition(targetPos, myPos, distToTarget);
                break;
        }
    }

    void DoApproachRun(Vector3 targetPos, Vector3 myPos, float distToTarget)
    {
        if (distToTarget < attackRunDistance * 0.7f)
        {
            currentManeuver = FighterManeuver.AttackPass;
            return;
        }

        Vector3 targetVel = Vector3.zero;
        Rigidbody targetRb = currentEnemy.GetComponent<Rigidbody>();
        if (targetRb != null) targetVel = targetRb.linearVelocity;

        Vector3 predictedPos = targetPos + targetVel * 1.5f;
        predictedPos.y = LockY();

        Vector3 toTarget = (predictedPos - myPos).normalized;
        Quaternion angleOffset = Quaternion.Euler(0f, currentApproachAngle, 0f);
        Vector3 approachVector = angleOffset * toTarget;

        float actualApproachDist = Mathf.Min(attackRunDistance, distToTarget * 1.2f);
        attackRunStartPoint = predictedPos - approachVector * actualApproachDist;
        attackRunStartPoint.y = LockY();

        movement.SetBoost(true, attackBoostMultiplier);
        movement.ClearFocusTarget();
        movement.SetWaypoint(attackRunStartPoint, WaypointPriority.High);

        float distToApproach = Vector3.Distance(myPos, attackRunStartPoint);
        Vector3 toApproach = (attackRunStartPoint - myPos).normalized;
        Vector3 velocity = movement.ShipRigidbody.linearVelocity;
        float velocityAlignment = velocity.magnitude > 1f ? Vector3.Dot(velocity.normalized, toApproach) : 1f;

        bool reachedPoint = distToApproach < 120f;
        bool closeEnough = distToTarget < attackRunDistance * 0.7f;
        bool timeoutTransition = distToApproach > attackRunDistance * 1.5f;
        bool movingAwayFromApproach = velocityAlignment < 0.3f && distToApproach < attackRunDistance;

        if (reachedPoint || closeEnough || timeoutTransition || movingAwayFromApproach)
        {
            hasReachedApproachPoint = true;
            currentManeuver = FighterManeuver.AttackPass;
        }
    }

    void DoAttackPass(Vector3 targetPos, Vector3 myPos, float distToTarget)
    {
        movement.SetBoost(true, attackBoostMultiplier);
        movement.SetFocusTarget(currentEnemy.transform);

        Vector3 toTarget = (targetPos - myPos).normalized;

        Vector3 attackPoint = targetPos - toTarget * (closingDistance * 0.5f);
        attackPoint.y = LockY();

        Vector3 throughPoint = targetPos + toTarget * overrunDistance;
        throughPoint.y = LockY();

        // This uses SetWaypointPath - priority must be Critical to snap if needed
        movement.SetWaypointPath(attackPoint, throughPoint, WaypointPriority.Critical);

        Vector3 myVel = movement.ShipRigidbody.linearVelocity;
        float dotToTarget = Vector3.Dot(myVel.normalized, toTarget);

        if (distToTarget < closingDistance || (distToTarget < 150f && dotToTarget < 0.3f))
        {
            currentManeuver = FighterManeuver.BreakAway;
            CalculateBreakAwayPoint(myPos, myVel, targetPos);
        }
    }

    bool CheckPathBlocked(Vector3 from, Vector3 to)
    {
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist < 50f) return false;
        RaycastHit hit;
        if (Physics.Raycast(from, dir.normalized, out hit, dist, movement.obstacleLayer))
        {
            return hit.distance < dist * 0.7f;
        }
        return false;
    }

    void CalculateBreakAwayPoint(Vector3 myPos, Vector3 myVel, Vector3 targetPos)
    {
        Vector3 breakDir;
        if (myVel.magnitude > 5f)
        {
            breakDir = myVel.normalized;
            float breakAngle = Random.Range(-70f, 70f);
            Quaternion breakRot = Quaternion.Euler(0f, breakAngle, 0f);
            breakDir = breakRot * breakDir;
        }
        else
        {
            breakDir = (myPos - targetPos).normalized;
            float breakAngle = Random.Range(-45f, 45f);
            Quaternion breakRot = Quaternion.Euler(0f, breakAngle, 0f);
            breakDir = breakRot * breakDir;
        }

        breakAwayTarget = myPos + breakDir * overrunDistance;
        breakAwayTarget.y = LockY();
    }

    void DoBreakAway(Vector3 targetPos, Vector3 myPos, float distToTarget)
    {
        movement.SetBoost(true, attackBoostMultiplier);
        movement.ClearFocusTarget();

        // Priority Critical here triggers the "Snap" in the new NPCShipMovement script
        movement.SetWaypoint(breakAwayTarget, WaypointPriority.Critical);

        float distToBreak = Vector3.Distance(myPos, breakAwayTarget);

        if (distToTarget > attackRunDistance * 0.8f || distToBreak < 100f)
        {
            currentManeuver = FighterManeuver.Reposition;
            repositionTimer = 0f;
        }
    }

    void DoReposition(Vector3 targetPos, Vector3 myPos, float distToTarget)
    {
        repositionTimer += Time.deltaTime;

        movement.SetBoost(true, 1.2f);
        movement.ClearFocusTarget();

        Vector3 toUs = (myPos - targetPos).normalized;
        float arcProgress = Mathf.Clamp01(repositionTimer / repositionTime);
        float arcAngle = arcProgress * 140f + Random.Range(-20f, 20f);

        Quaternion arcRotation = Quaternion.Euler(0f, arcAngle, 0f);
        Vector3 arcPosition = targetPos + arcRotation * toUs * repositionRadius;
        arcPosition.y = LockY();

        movement.SetWaypoint(arcPosition, WaypointPriority.High);

        bool timedOut = repositionTimer >= repositionTime * 0.7f;
        bool farEnough = distToTarget > attackRunDistance * 0.8f;
        bool tooClose = distToTarget < attackRunDistance * 0.4f;

        if (timedOut || (farEnough && repositionTimer > 0.8f) || tooClose)
        {
            currentManeuver = FighterManeuver.ApproachRun;
            currentApproachAngle = Random.Range(-approachAngleVariation, approachAngleVariation);
            hasReachedApproachPoint = false;
        }
    }

    void DoOrbit()
    {
        if (currentEnemy == null)
        {
            ChangeState(AIBehaviorMode.Wander);
            return;
        }

        movement.SetCombatManeuverMode(false);
        movement.SetFocusTarget(currentEnemy.transform);

        orbitChangeTimer += Time.deltaTime;
        if (orbitChangeTimer > 15f && movement.GetObstacleCount() > 3)
        {
            orbitClockwise = !orbitClockwise;
            orbitChangeTimer = 0f;
        }

        Vector3 enemyPos = currentEnemy.transform.position;
        enemyPos.y = LockY();
        Vector3 myPos = transform.position;
        myPos.y = LockY();

        Vector3 toEnemy = enemyPos - myPos;
        float distToEnemy = toEnemy.magnitude;
        Vector3 dirToEnemy = toEnemy / distToEnemy;

        Vector3 tangent = Vector3.Cross(dirToEnemy, Vector3.up);
        if (orbitClockwise) tangent = -tangent;

        float error = distToEnemy - orbitRange;
        float correction = Mathf.Clamp(error * orbitCorrectionFactor, -20f, 20f);

        Vector3 desiredDir = (tangent * 10f) + (dirToEnemy * correction);
        desiredDir.Normalize();

        Vector3 dest = myPos + (desiredDir * 150f);
        dest.y = LockY();

        movement.SetBoost(false);
        movement.SetWaypoint(dest, WaypointPriority.High);
    }

    void DoWander()
    {
        movement.SetCombatManeuverMode(false);
        movement.ClearFocusTarget();

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

        if (currentWanderTarget == Vector3.zero ||
            Vector3.Distance(transform.position, currentWanderTarget) < waypointReachDistance)
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

        movement.SetBoost(false);
        movement.SetWaypoint(currentWanderTarget, WaypointPriority.Low);
    }

    Vector3 GetRandomWanderPoint()
    {
        float distance = Random.Range(minWanderDistance, maxWanderDistance);
        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector3 offset = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
        Vector3 newPoint = wanderCenter + offset;

        if ((newPoint - wanderCenter).magnitude > wanderRadius)
            newPoint = wanderCenter + (newPoint - wanderCenter).normalized * wanderRadius;

        newPoint.y = LockY();
        return newPoint;
    }

    void DoRetreat()
    {
        movement.SetCombatManeuverMode(false);
        movement.ClearFocusTarget();

        if (currentEnemy == null) return;
        Vector3 away = Flat(transform.position - currentEnemy.transform.position).normalized;
        Vector3 p = transform.position + away * 150f;
        p.y = LockY();
        movement.SetWaypoint(p, WaypointPriority.Critical);
        movement.SetBoost(true, 1.2f);
    }

    void DoHold()
    {
        movement.SetCombatManeuverMode(false);
        if (currentEnemy != null) movement.SetFocusTarget(currentEnemy.transform);
        movement.ClearWaypoint(WaypointPriority.Medium);
        movement.SetBoost(false);
    }

    GameObject FindNearestEnemy()
    {
        if (Time.frameCount % 5 != 0 && currentEnemy != null) return currentEnemy;

        Collider[] colliders = Physics.OverlapSphere(transform.position, engagementRange);
        GameObject best = null;
        float minDist = float.MaxValue;
        var myAlign = GetComponent<Alignment>();
        bool iHaveAlign = myAlign != null;

        foreach (var col in colliders)
        {
            if (col == null) continue;
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

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Handles.Label(transform.position + Vector3.up * 3f,
            $"Class: {shipClass} | State: {currentState} | Maneuver: {currentManeuver}");

        if (currentEnemy != null && shipClass == NPCClass.Fighter)
        {
            Vector3 targetPos = currentEnemy.transform.position;
            Vector3 myPos = transform.position;

            if (currentManeuver == FighterManeuver.ApproachRun)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(attackRunStartPoint, 30f);
                Gizmos.DrawLine(myPos, attackRunStartPoint);
                Gizmos.DrawLine(attackRunStartPoint, targetPos);
            }

            if (currentManeuver == FighterManeuver.AttackPass)
            {
                Gizmos.color = Color.red;
                Vector3 dir = (targetPos - myPos).normalized;
                Vector3 through = targetPos + dir * overrunDistance;
                Gizmos.DrawLine(myPos, through);
                Gizmos.DrawWireSphere(through, 25f);
            }

            if (currentManeuver == FighterManeuver.BreakAway)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(myPos, breakAwayTarget);
                Gizmos.DrawWireSphere(breakAwayTarget, 30f);
            }

            if (currentManeuver == FighterManeuver.Reposition)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(targetPos, repositionRadius);
                Vector3 start = myPos - targetPos;
                for (int i = 0; i < 20; i++)
                {
                    float angle1 = (i / 20f) * 180f;
                    float angle2 = ((i + 1) / 20f) * 180f;
                    Vector3 p1 = targetPos + Quaternion.Euler(0, angle1, 0) * start.normalized * repositionRadius;
                    Vector3 p2 = targetPos + Quaternion.Euler(0, angle2, 0) * start.normalized * repositionRadius;
                    Gizmos.DrawLine(p1, p2);
                }
            }

            Gizmos.color = Color.green * 0.3f;
            Gizmos.DrawWireSphere(targetPos, closingDistance);
            Gizmos.color = Color.yellow * 0.3f;
            Gizmos.DrawWireSphere(targetPos, attackRunDistance);
        }
        else if (shipClass == NPCClass.Capital && currentEnemy != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(currentEnemy.transform.position, orbitRange);
            Gizmos.DrawLine(transform.position, currentEnemy.transform.position);
        }
    }
#endif
}

public enum AIBehaviorMode
{
    Wander,
    Approach,
    Retreat,
    Hold
}

public enum WaypointPriority
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}
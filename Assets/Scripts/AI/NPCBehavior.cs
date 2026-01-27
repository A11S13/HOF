using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum NPCClass
{
    Fighter,
    Capital
}

public enum CapitalCombatStyle
{
    BroadsideOrbit, // Circle target, keeping side guns bearing (Naval style)
    SpinalAssault   // Drive at target to fire forward guns, then break off (Destroyer style)
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

    [Header("Capital Ship Tactics")]
    public CapitalCombatStyle capitalStyle = CapitalCombatStyle.BroadsideOrbit;
    [Tooltip("Desired distance for broadside/orbit")]
    public float orbitRange = 350f;
    [Tooltip("Distance to break off spinal attack")]
    public float spinalBreakDistance = 250f;
    [Tooltip("Distance to turn back for new spinal attack")]
    public float spinalResetDistance = 700f;
    [Tooltip("How aggressively to correct orbit distance")]
    public float orbitCorrectionFactor = 0.5f;

    [Header("Engagement")]
    public float engagementRange = 600f;
    public float disengageRange = 850f;

    [Header("Fighter Boom & Zoom")]
    public float attackRunDistance = 450f;
    public float closingDistance = 80f;
    public float overrunDistance = 220f;
    public float repositionRadius = 320f;
    public float repositionTime = 1.2f;
    public float attackBoostMultiplier = 1.8f;
    public float approachAngleVariation = 60f;
    public float strafeOffset = 25f;

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

    // Fighter State
    private FighterManeuver currentManeuver = FighterManeuver.ApproachRun;
    private Vector3 attackRunStartPoint;
    private Vector3 breakAwayTarget;
    private float repositionTimer = 0f;
    private float currentApproachAngle = 0f;
    private bool hasReachedApproachPoint = false;
    private Vector3 currentStrafeOffsetVector;

    // Capital State
    private bool orbitClockwise = true;
    private float orbitChangeTimer = 0f;
    private bool initializedCapitalPhysics = false;

    // New: Spinal Assault State
    private bool isSpinalAttacking = true; // true = charging, false = retreating/turning

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

        if (desiredState != currentState) ChangeState(desiredState);

        switch (currentState)
        {
            case AIBehaviorMode.Wander: DoWander(); break;
            case AIBehaviorMode.Approach:
                if (shipClass == NPCClass.Capital) DoCapitalBehavior();
                else DoBoomAndZoom();
                break;
            case AIBehaviorMode.Retreat: DoRetreat(); break;
            case AIBehaviorMode.Hold: DoHold(); break;
        }
    }

    void ChangeState(AIBehaviorMode newState)
    {
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

    // ==========================================================
    // FIGHTER LOGIC
    // ==========================================================
    void DoBoomAndZoom()
    {
        if (currentEnemy == null) { ChangeState(AIBehaviorMode.Wander); return; }
        movement.SetCombatManeuverMode(true);

        Vector3 targetPos = currentEnemy.transform.position; targetPos.y = LockY();
        Vector3 myPos = transform.position; myPos.y = LockY();
        float distToTarget = Vector3.Distance(myPos, targetPos);

        switch (currentManeuver)
        {
            case FighterManeuver.ApproachRun: DoApproachRun(targetPos, myPos, distToTarget); break;
            case FighterManeuver.AttackPass: DoAttackPass(targetPos, myPos, distToTarget); break;
            case FighterManeuver.BreakAway: DoBreakAway(targetPos, myPos, distToTarget); break;
            case FighterManeuver.Reposition: DoReposition(targetPos, myPos, distToTarget); break;
        }
    }

    void DoApproachRun(Vector3 targetPos, Vector3 myPos, float distToTarget)
    {
        if (distToTarget < attackRunDistance * 0.7f)
        {
            float dir = Random.value > 0.5f ? 1f : -1f;
            Vector3 forward = (targetPos - myPos).normalized;
            Vector3 right = Vector3.Cross(forward, Vector3.up);
            currentStrafeOffsetVector = right * dir * strafeOffset;
            currentManeuver = FighterManeuver.AttackPass;
            return;
        }

        Vector3 targetVel = Vector3.zero;
        if (currentEnemy.TryGetComponent<Rigidbody>(out var rb)) targetVel = rb.linearVelocity;
        Vector3 predictedPos = targetPos + targetVel * 1.5f; predictedPos.y = LockY();

        Vector3 approachVector = Quaternion.Euler(0f, currentApproachAngle, 0f) * (predictedPos - myPos).normalized;
        float actualApproachDist = Mathf.Min(attackRunDistance, distToTarget * 1.2f);
        attackRunStartPoint = predictedPos - approachVector * actualApproachDist; attackRunStartPoint.y = LockY();

        movement.SetBoost(true, attackBoostMultiplier);
        movement.ClearFocusTarget();
        movement.SetWaypoint(attackRunStartPoint, WaypointPriority.High);

        if (Vector3.Distance(myPos, attackRunStartPoint) < 120f) hasReachedApproachPoint = true;
        if (hasReachedApproachPoint || distToTarget < attackRunDistance * 0.7f)
        {
            float dir = Random.value > 0.5f ? 1f : -1f;
            Vector3 right = Vector3.Cross((targetPos - myPos).normalized, Vector3.up);
            currentStrafeOffsetVector = right * dir * strafeOffset;
            currentManeuver = FighterManeuver.AttackPass;
        }
    }

    void DoAttackPass(Vector3 targetPos, Vector3 myPos, float distToTarget)
    {
        movement.SetBoost(true, attackBoostMultiplier);
        movement.SetFocusTarget(currentEnemy.transform);

        Vector3 toTarget = (targetPos - myPos).normalized;
        Vector3 attackPoint = targetPos - toTarget * (closingDistance * 0.5f) + currentStrafeOffsetVector; attackPoint.y = LockY();
        Vector3 throughPoint = targetPos + toTarget * overrunDistance + currentStrafeOffsetVector; throughPoint.y = LockY();

        movement.SetWaypointPath(attackPoint, throughPoint, WaypointPriority.Critical);

        float dotToTarget = Vector3.Dot(movement.ShipRigidbody.linearVelocity.normalized, toTarget);
        if (distToTarget < closingDistance || (distToTarget < 150f && dotToTarget < 0.3f))
        {
            currentManeuver = FighterManeuver.BreakAway;
            CalculateBreakAwayPoint(myPos, movement.ShipRigidbody.linearVelocity, targetPos);
        }
    }

    void CalculateBreakAwayPoint(Vector3 myPos, Vector3 myVel, Vector3 targetPos)
    {
        Vector3 breakDir = (myVel.magnitude > 5f) ? myVel.normalized : (myPos - targetPos).normalized;
        breakDir = Quaternion.Euler(0f, Random.Range(-45f, 45f), 0f) * breakDir;
        breakAwayTarget = myPos + breakDir * overrunDistance; breakAwayTarget.y = LockY();
    }

    void DoBreakAway(Vector3 targetPos, Vector3 myPos, float distToTarget)
    {
        movement.SetBoost(true, attackBoostMultiplier);
        movement.ClearFocusTarget();
        movement.SetWaypoint(breakAwayTarget, WaypointPriority.Critical);

        if (distToTarget > attackRunDistance * 0.8f || Vector3.Distance(myPos, breakAwayTarget) < 100f)
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
        Vector3 arcPosition = targetPos + Quaternion.Euler(0f, arcAngle, 0f) * toUs * repositionRadius; arcPosition.y = LockY();

        movement.SetWaypoint(arcPosition, WaypointPriority.High);

        if (repositionTimer >= repositionTime * 0.7f || (distToTarget > attackRunDistance * 0.8f && repositionTimer > 0.8f))
        {
            currentManeuver = FighterManeuver.ApproachRun;
            currentApproachAngle = Random.Range(-approachAngleVariation, approachAngleVariation);
            hasReachedApproachPoint = false;
        }
    }

    // ==========================================================
    // CAPITAL LOGIC
    // ==========================================================
    void DoCapitalBehavior()
    {
        if (currentEnemy == null) { ChangeState(AIBehaviorMode.Wander); return; }

        movement.SetCombatManeuverMode(false);

        // Randomly switch orbit direction if blocked
        orbitChangeTimer += Time.deltaTime;
        if (orbitChangeTimer > 20f && movement.GetObstacleCount() > 5)
        {
            orbitClockwise = !orbitClockwise;
            orbitChangeTimer = 0f;
        }

        if (capitalStyle == CapitalCombatStyle.SpinalAssault)
        {
            DoCapitalSpinalAssault();
        }
        else
        {
            DoCapitalBroadside();
        }
    }

    void DoCapitalSpinalAssault()
    {
        Vector3 enemyPos = currentEnemy.transform.position; enemyPos.y = LockY();
        Vector3 myPos = transform.position; myPos.y = LockY();
        float dist = Vector3.Distance(myPos, enemyPos);

        // State Machine: Charge vs Retreat
        if (isSpinalAttacking)
        {
            // PHASE 1: CHARGE
            // Look directly at enemy to fire fixed weapons
            movement.SetFocusTarget(currentEnemy.transform);

            // Move to a point slightly offset (to avoid ramming)
            // We use the same offset trick as fighters
            if (currentStrafeOffsetVector == Vector3.zero)
            {
                float dir = Random.value > 0.5f ? 1f : -1f;
                Vector3 right = Vector3.Cross((enemyPos - myPos).normalized, Vector3.up);
                currentStrafeOffsetVector = right * dir * 100f; // Wider offset for capitals
            }

            Vector3 attackDest = enemyPos + currentStrafeOffsetVector;
            movement.SetWaypoint(attackDest, WaypointPriority.High);

            // If too close, BREAK OFF
            if (dist < spinalBreakDistance)
            {
                isSpinalAttacking = false;
                currentStrafeOffsetVector = Vector3.zero; // Reset for next time
            }
        }
        else
        {
            // PHASE 2: REPOSITION (Heavy Turn)
            // Do NOT look at enemy (we can't fire anyway). Look where we are going (Naval turn).
            movement.ClearFocusTarget();

            // Calculate retreat point: Fly PAST the enemy and out
            Vector3 awayFromEnemy = (myPos - enemyPos).normalized;
            // Add a curve to it so we don't just back up
            Vector3 flankDir = Quaternion.Euler(0f, orbitClockwise ? 45f : -45f, 0f) * awayFromEnemy;

            Vector3 retreatPos = enemyPos + flankDir * spinalResetDistance;
            retreatPos.y = LockY();

            movement.SetWaypoint(retreatPos, WaypointPriority.High);

            // If far enough, TURN BACK IN
            if (dist > spinalResetDistance * 0.9f)
            {
                isSpinalAttacking = true;
            }
        }
    }

    void DoCapitalBroadside()
    {
        Vector3 enemyPos = currentEnemy.transform.position; enemyPos.y = LockY();
        Vector3 myPos = transform.position; myPos.y = LockY();
        Vector3 toEnemy = enemyPos - myPos;
        float distToEnemy = toEnemy.magnitude;
        Vector3 dirToEnemy = toEnemy / distToEnemy;

        Vector3 tangent = Vector3.Cross(dirToEnemy, Vector3.up);
        if (orbitClockwise) tangent = -tangent;

        float error = distToEnemy - orbitRange;
        float correction = Mathf.Clamp(error * orbitCorrectionFactor, -30f, 30f);

        Vector3 desiredDir = (tangent * 10f) + (dirToEnemy * correction);
        desiredDir.Normalize();

        Vector3 dest = myPos + (desiredDir * 200f); dest.y = LockY();

        movement.SetBoost(false);
        movement.SetWaypoint(dest, WaypointPriority.High);

        // Broadside: Look where we go
        movement.ClearFocusTarget();
        movement.SetFocusPoint(myPos + desiredDir * 1000f);
    }

    // ==========================================================
    // UTILS
    // ==========================================================
    void DoWander()
    {
        movement.SetCombatManeuverMode(false);
        movement.ClearFocusTarget();
        if (isWaiting)
        {
            wanderWaitTimer -= Time.deltaTime;
            if (wanderWaitTimer <= 0f) { isWaiting = false; currentWanderTarget = Vector3.zero; }
            else { movement.ClearWaypoint(WaypointPriority.Low); return; }
        }
        if (currentWanderTarget == Vector3.zero || Vector3.Distance(transform.position, currentWanderTarget) < waypointReachDistance)
        {
            if (wanderWaitTime > 0f && currentWanderTarget != Vector3.zero) { isWaiting = true; wanderWaitTimer = wanderWaitTime; movement.ClearWaypoint(WaypointPriority.Low); return; }
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
        if ((newPoint - wanderCenter).magnitude > wanderRadius) newPoint = wanderCenter + (newPoint - wanderCenter).normalized * wanderRadius;
        newPoint.y = LockY();
        return newPoint;
    }

    void DoRetreat()
    {
        movement.SetCombatManeuverMode(false);
        movement.ClearFocusTarget();
        if (currentEnemy == null) return;
        Vector3 away = Flat(transform.position - currentEnemy.transform.position).normalized;
        Vector3 p = transform.position + away * 150f; p.y = LockY();
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
            if (col == null || col.transform.root == transform.root) continue;
            var targetAlign = col.GetComponent<Alignment>();
            if (targetAlign == null) continue;
            if (iHaveAlign && targetAlign.isFriendly == myAlign.isFriendly) continue;
            float d = Vector3.Distance(transform.position, col.transform.position);
            if (d < minDist) { minDist = d; best = col.gameObject; }
        }
        return best;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Handles.Label(transform.position + Vector3.up * 3f, $"Class: {shipClass} | State: {currentState}");
        if (shipClass == NPCClass.Capital && currentEnemy != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(currentEnemy.transform.position, orbitRange);

            if (capitalStyle == CapitalCombatStyle.SpinalAssault)
            {
                Gizmos.color = isSpinalAttacking ? Color.red : Color.blue;
                Vector3 dest = isSpinalAttacking ? currentEnemy.transform.position : transform.position + (transform.position - currentEnemy.transform.position).normalized * 200f;
                Gizmos.DrawLine(transform.position, dest);
                Handles.Label(transform.position + Vector3.up * 6f, isSpinalAttacking ? "Charging Spinal" : "Repositioning");
            }
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
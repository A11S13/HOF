using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum NPCClass
{
    Fighter,    // Chases directly, dogfights
    Capital     // Orbits target facing INWARDS (Front weapons), Strafe-Circling
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
    public float approachBuffer = 40f;

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
            case AIBehaviorMode.Wander: DoWander(); break;
            case AIBehaviorMode.Approach:
                if (shipClass == NPCClass.Capital) DoOrbit();
                else DoFighterApproach();
                break;
            case AIBehaviorMode.Retreat: DoRetreat(); break;
            case AIBehaviorMode.Hold: DoHold(); break;
        }
    }

    void ChangeState(AIBehaviorMode newState)
    {
        previousState = currentState;
        currentState = newState;

        movement.ClearWaypoint(WaypointPriority.Critical);
        movement.SetBoost(false);
        movement.ClearFlankingTarget();
        movement.ClearFocusTarget(); // Reset rotation logic

        if (currentState == AIBehaviorMode.Wander)
        {
            currentWanderTarget = Vector3.zero;
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
    // FIGHTER BEHAVIOR
    // ----------------------------------------------------------
    void DoFighterApproach()
    {
        if (currentEnemy == null) { ChangeState(AIBehaviorMode.Wander); return; }

        // Fighter looks at enemy too
        movement.SetFocusTarget(currentEnemy.transform);

        Vector3 targetPos = currentEnemy.transform.position;
        targetPos.y = LockY();

        Vector3 toTarget = targetPos - transform.position;
        float dist = toTarget.magnitude;

        if (dist > 200f) movement.SetBoost(true, approachBoostMultiplier);
        else movement.SetBoost(false);

        Vector3 approachPoint = targetPos - Flat(toTarget).normalized * approachBuffer;
        approachPoint.y = LockY();

        movement.SetWaypoint(approachPoint, WaypointPriority.High);
    }

    // ----------------------------------------------------------
    // CAPITAL BEHAVIOR (Strafing Orbit)
    // ----------------------------------------------------------
    void DoOrbit()
    {
        if (currentEnemy == null) { ChangeState(AIBehaviorMode.Wander); return; }

        // CRITICAL: Tell movement script to face the enemy, 
        // even though we are moving sideways.
        movement.SetFocusTarget(currentEnemy.transform);

        // Switch orbit direction occasionally
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

        // Tangent calculation (Left or Right)
        Vector3 tangent = Vector3.Cross(dirToEnemy, Vector3.up);
        if (orbitClockwise) tangent = -tangent;

        // Distance correction:
        // distToEnemy - orbitRange = error
        // If error > 0 (too far), move IN (+dirToEnemy)
        // If error < 0 (too close), move OUT (-dirToEnemy)
        float error = distToEnemy - orbitRange;
        float correction = Mathf.Clamp(error * orbitCorrectionFactor, -20f, 20f);

        // Blend Tangent (sideways) with Correction (in/out)
        Vector3 desiredDir = (tangent * 10f) + (dirToEnemy * correction);
        desiredDir.Normalize();

        Vector3 dest = myPos + (desiredDir * 150f);
        dest.y = LockY();

        movement.SetBoost(false);
        movement.SetWaypoint(dest, WaypointPriority.High);
    }

    // ----------------------------------------------------------
    // UTILS
    // ----------------------------------------------------------
    void DoWander()
    {
        movement.ClearFocusTarget(); // Look where we are going

        if (isWaiting)
        {
            wanderWaitTimer -= Time.deltaTime;
            if (wanderWaitTimer <= 0f) { isWaiting = false; currentWanderTarget = Vector3.zero; }
            else { movement.ClearWaypoint(WaypointPriority.Low); return; }
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
        // When running away, look where we are going (Clear focus)
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

public enum WaypointPriority { None = 0, Low = 1, Medium = 2, High = 3, Critical = 4 }

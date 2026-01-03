using UnityEngine;

public class NPCBehavior : MonoBehaviour
{
    [Header("References")]
    private NPCShipMovement movement;
    private ShipCore shipCore;
    private ShipTargetingSystem targeting;

    [Header("Behavior Settings")]
    public AIBehaviorMode behaviorMode = AIBehaviorMode.Wander;

    [Header("Engagement")]
    public float engagementRange = 600f;
    public float disengageRange = 750f;

    [Header("Boom & Zoom")]
    public bool useBoomAndZoom = true;

    public float interceptRange = 150f;
    public float strikeStartRange = 150f;
    public float extendDistance = 150f;
    public float extendMinTime = 0.5f;
    public float strikeMaxTime = 1f;
    public float repositionRange = 100f;
    public float knifeFightRange = 90f;

    [Range(0.3f, 0.99f)]
    public float minAttackDot = 0.85f;

    public float leadTime = 0.8f;
    public float interceptLateralOffset = 140f;
    public float repositionSideOffset = 320f;
    public float repositionBackOffset = 220f;

    [Header("Fire Windows")]
    [Tooltip("If true, AI authorizes firing in InterceptAlign when aligned and close enough.")]
    public bool allowFireInInterceptAlign = true;

    [Tooltip("Distance gate for firing in InterceptAlign (prevents long-range spray).")]
    public float interceptFireRangeMultiplier = 1.0f; // * strikeStartRange

    [Header("Boost Multipliers")]
    public float interceptBoostMultiplier = 1.25f;
    public float strikeBoostMultiplier = 1.85f;
    public float extendBoostMultiplier = 1.6f;
    public float repositionBoostMultiplier = 1.25f;

    [Header("Wander Settings")]
    public float wanderRadius = 400f;
    public float minWanderDistance = 20f;
    public float maxWanderDistance = 800f;
    public float waypointReachDistance = 10f;
    public float wanderWaitTime = 0f;

    // Public: weapon systems can read this
    public bool FireAuthorized { get; private set; }

    // Internal
    private Vector3 wanderCenter;
    private Vector3 currentWanderTarget;
    private float wanderWaitTimer = 0f;
    private bool isWaiting = false;

    private GameObject currentEnemy;
    private AIBehaviorMode currentState;
    private AIBehaviorMode previousState;
    private float stateEnterTime;

    // Boom & Zoom internal
    private Vector3 strikeAimPoint;
    private Vector3 extendPoint;
    private float extendEnterTime;

    void Awake()
    {
        movement = GetComponent<NPCShipMovement>();
        shipCore = GetComponentInChildren<ShipCore>();
        targeting = GetComponentInChildren<ShipTargetingSystem>();

        wanderCenter = transform.position;

        currentState = behaviorMode;
        previousState = behaviorMode;
        stateEnterTime = Time.time;

        FireAuthorized = false;
    }

    void Update()
    {
        if (movement == null || shipCore == null) return;
        UpdateBehavior();
    }

    void UpdateBehavior()
    {
        currentEnemy = FindNearestEnemy();
        FireAuthorized = false; // reset each frame; states enable it explicitly

        AIBehaviorMode desiredState = DetermineTacticalState();

        if (desiredState != currentState)
        {
            // Lock Extend for min time so it doesn't instantly turn back into a turn-fight.
            bool locked = (currentState == AIBehaviorMode.Extend) && (Time.time - extendEnterTime < extendMinTime);

            if (currentEnemy == null) locked = false;

            if (!locked) ChangeState(desiredState);
        }

        switch (currentState)
        {
            case AIBehaviorMode.Wander: DoWander(); break;
            case AIBehaviorMode.Approach: DoApproach(); break;

            case AIBehaviorMode.InterceptAlign: DoInterceptAlign(); break;
            case AIBehaviorMode.StrikePass: DoStrikePass(); break;
            case AIBehaviorMode.Extend: DoExtend(); break;
            case AIBehaviorMode.Reposition: DoReposition(); break;

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
        movement.ClearFlankingTarget();

        switch (currentState)
        {
            case AIBehaviorMode.InterceptAlign:
                strikeAimPoint = Vector3.zero;
                extendPoint = Vector3.zero;
                break;

            case AIBehaviorMode.StrikePass:
                strikeAimPoint = Vector3.zero;
                extendPoint = Vector3.zero;
                BuildStrikePlan();
                break;

            case AIBehaviorMode.Extend:
                extendEnterTime = Time.time;
                if (extendPoint == Vector3.zero) BuildStrikePlan();
                break;

            case AIBehaviorMode.Reposition:
                strikeAimPoint = Vector3.zero;
                extendPoint = Vector3.zero;
                break;

            case AIBehaviorMode.Wander:
                currentWanderTarget = Vector3.zero;
                break;
        }
    }

    AIBehaviorMode DetermineTacticalState()
    {
        if (currentEnemy == null) return behaviorMode;

        float dist = Vector3.Distance(transform.position, currentEnemy.transform.position);

        if (dist > disengageRange) return behaviorMode;
        if (dist > engagementRange) return behaviorMode;

        if (!useBoomAndZoom) return AIBehaviorMode.Approach;

        if (dist < knifeFightRange)
            return AIBehaviorMode.Extend;

        switch (currentState)
        {
            case AIBehaviorMode.Wander:
            case AIBehaviorMode.Approach:
                if (dist < interceptRange) return AIBehaviorMode.InterceptAlign;
                return AIBehaviorMode.Approach;

            case AIBehaviorMode.InterceptAlign:
                {
                    Vector3 leadPos = GetLeadPosition(currentEnemy, leadTime);
                    bool aligned = IsAlignedTo(leadPos, minAttackDot);

                    if (dist < strikeStartRange && aligned) return AIBehaviorMode.StrikePass;

                    if (dist > interceptRange * 1.35f) return AIBehaviorMode.Approach;

                    return AIBehaviorMode.InterceptAlign;
                }

            case AIBehaviorMode.StrikePass:
                {
                    if (Time.time - stateEnterTime > strikeMaxTime) return AIBehaviorMode.Extend;

                    Vector3 toEnemy = currentEnemy.transform.position - transform.position;
                    float dot = Vector3.Dot(Flat(transform.forward).normalized, Flat(toEnemy).normalized);

                    // Once enemy is behind us, we "passed" -> extend.
                    if (dot < 0f) return AIBehaviorMode.Extend;

                    return AIBehaviorMode.StrikePass;
                }

            case AIBehaviorMode.Extend:
                // Extend lock handled outside (UpdateBehavior)
                if (dist > repositionRange) return AIBehaviorMode.Reposition;
                return AIBehaviorMode.Extend;

            case AIBehaviorMode.Reposition:
                {
                    Vector3 leadPos = GetLeadPosition(currentEnemy, leadTime);
                    if (dist < interceptRange && IsAlignedTo(leadPos, 0.6f)) return AIBehaviorMode.InterceptAlign;
                    if (dist > interceptRange * 1.6f) return AIBehaviorMode.Approach;
                    return AIBehaviorMode.Reposition;
                }

            default:
                return AIBehaviorMode.Approach;
        }
    }

    // ==========================================================
    // Boom & Zoom execution (Y-locked)
    // ==========================================================

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    float LockY() => transform.position.y;

    Vector3 GetLeadPosition(GameObject enemy, float t)
    {
        if (enemy == null) return transform.position;
        Rigidbody rb = enemy.GetComponent<Rigidbody>();
        Vector3 p = (rb != null) ? enemy.transform.position + rb.linearVelocity * t : enemy.transform.position;

        // Y-locked: use our current Y
        p.y = LockY();
        return p;
    }

    bool IsAlignedTo(Vector3 point, float dotThreshold)
    {
        Vector3 to = Flat(point - transform.position);
        if (to.sqrMagnitude < 0.0001f) return true;
        to.Normalize();

        Vector3 f = Flat(transform.forward);
        if (f.sqrMagnitude < 0.0001f) f = Vector3.forward;
        f.Normalize();

        return Vector3.Dot(f, to) >= dotThreshold;
    }

    void BuildStrikePlan()
    {
        if (currentEnemy == null) return;

        Vector3 leadPos = GetLeadPosition(currentEnemy, leadTime);
        strikeAimPoint = leadPos;

        Vector3 dir = Flat(leadPos - transform.position);
        if (dir.sqrMagnitude < 0.0001f) dir = Flat(transform.forward);
        dir.Normalize();

        extendPoint = leadPos + dir * extendDistance;
        extendPoint.y = LockY();
    }

    void DoApproach()
    {
        if (currentEnemy == null)
        {
            if (behaviorMode == AIBehaviorMode.Wander) DoWander();
            return;
        }

        Vector3 target = currentEnemy.transform.position;
        target.y = LockY();

        Vector3 toTarget = target - transform.position;
        if (toTarget.sqrMagnitude < 0.0001f) return;

        Vector3 approachPoint = target - Flat(toTarget).normalized * 25f;
        approachPoint.y = LockY();

        movement.SetBoost(true, interceptBoostMultiplier);
        movement.SetWaypoint(approachPoint, WaypointPriority.High);
    }

    void DoInterceptAlign()
    {
        if (currentEnemy == null) return;

        Vector3 leadPos = GetLeadPosition(currentEnemy, leadTime);

        Vector3 toEnemy = Flat(leadPos - transform.position);
        if (toEnemy.sqrMagnitude < 0.0001f) toEnemy = Flat(transform.forward);

        Vector3 right = Vector3.Cross(Vector3.up, toEnemy.normalized);

        // stable side choice based on where we are relative to enemy-right axis
        float sideSign = Mathf.Sign(Vector3.Dot(Flat(transform.position - leadPos), right));
        if (sideSign == 0f) sideSign = (Random.value > 0.5f) ? 1f : -1f;

        Vector3 alignPoint = leadPos + right * sideSign * interceptLateralOffset;
        alignPoint.y = LockY();

        movement.SetBoost(true, interceptBoostMultiplier);
        movement.SetWaypoint(alignPoint, WaypointPriority.High);

        // Fire gating: InterceptAlign when aligned (and close enough)
        if (allowFireInInterceptAlign)
        {
            float dist = Vector3.Distance(transform.position, currentEnemy.transform.position);
            bool aligned = IsAlignedTo(leadPos, minAttackDot);
            bool closeEnough = dist <= strikeStartRange * interceptFireRangeMultiplier;

            FireAuthorized = aligned && closeEnough;
        }
    }

    void DoStrikePass()
    {
        if (currentEnemy == null) return;

        // keep plan fresh vs moving target
        BuildStrikePlan();

        movement.SetBoost(true, strikeBoostMultiplier);
        movement.SetWaypoint(strikeAimPoint, WaypointPriority.Critical);

        // Always allow fire during the committed pass
        FireAuthorized = true;
    }

    void DoExtend()
    {
        if (currentEnemy == null) return;

        if (extendPoint == Vector3.zero) BuildStrikePlan();

        // Keep extending forward so we don't "arrive" and brake.
        Vector3 f = Flat(transform.forward);
        if (f.sqrMagnitude < 0.0001f) f = Vector3.forward;
        f.Normalize();

        extendPoint = transform.position + f * extendDistance;
        extendPoint.y = LockY();

        movement.SetBoost(true, extendBoostMultiplier);
        movement.SetWaypoint(extendPoint, WaypointPriority.Critical);

        FireAuthorized = false;
    }

    void DoReposition()
    {
        if (currentEnemy == null) return;

        Vector3 enemyPos = GetLeadPosition(currentEnemy, leadTime);

        Vector3 toEnemy = Flat(enemyPos - transform.position);
        if (toEnemy.sqrMagnitude < 0.0001f) toEnemy = Flat(transform.forward);

        Vector3 right = Vector3.Cross(Vector3.up, toEnemy.normalized);

        // consistent turning direction
        float sideSign = Mathf.Sign(Vector3.Dot(transform.right, right));
        if (sideSign == 0f) sideSign = (Random.value > 0.5f) ? 1f : -1f;

        Vector3 repositionPoint =
            enemyPos +
            right * sideSign * repositionSideOffset +
            (-toEnemy.normalized) * repositionBackOffset;

        repositionPoint.y = LockY();

        movement.SetBoost(true, repositionBoostMultiplier);
        movement.SetWaypoint(repositionPoint, WaypointPriority.High);

        FireAuthorized = false;
    }

    // ==========================================================
    // Wander / Retreat / Hold
    // ==========================================================

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
        if (currentEnemy == null) return;
        Vector3 away = Flat(transform.position - currentEnemy.transform.position).normalized;
        Vector3 p = transform.position + away * 60f;
        p.y = LockY();
        movement.SetWaypoint(p, WaypointPriority.Critical);
        movement.SetBoost(true, 1.2f);
        FireAuthorized = false;
    }

    void DoHold()
    {
        movement.ClearWaypoint(WaypointPriority.Medium);
        movement.SetBoost(false);
        FireAuthorized = false;
    }

    // ==========================================================
    // Enemy search
    // ==========================================================

    GameObject FindNearestEnemy()
    {
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
}

public enum AIBehaviorMode
{
    Wander,
    Approach,
    InterceptAlign,
    StrikePass,
    Extend,
    Reposition,
    Retreat,
    Hold
}

public enum WaypointPriority { None = 0, Low = 1, Medium = 2, High = 3, Critical = 4 }

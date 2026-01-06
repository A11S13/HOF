using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class NPCBehavior : MonoBehaviour
{
    [Header("References")]
    private NPCShipMovement movement;
    private ShipCore shipCore;

    [Header("Behavior Settings")]
    public AIBehaviorMode behaviorMode = AIBehaviorMode.Wander;

    [Header("Engagement")]
    public float engagementRange = 600f;
    public float disengageRange = 750f;
    public float approachBuffer = 40f;

    [Header("Boost Settings")]
    public float approachBoostMultiplier = 1.25f;

    [Header("Wander Settings")]
    public float wanderRadius = 400f;
    public float minWanderDistance = 20f;
    public float maxWanderDistance = 800f;
    public float waypointReachDistance = 10f;
    public float wanderWaitTime = 0f;

    // Internal
    private Vector3 wanderCenter;
    private Vector3 currentWanderTarget;
    private float wanderWaitTimer = 0f;
    private bool isWaiting = false;

    private GameObject currentEnemy;
    private AIBehaviorMode currentState;
    private AIBehaviorMode previousState;
    private float stateEnterTime;

    void Awake()
    {
        movement = GetComponent<NPCShipMovement>();
        shipCore = GetComponentInChildren<ShipCore>();

        wanderCenter = transform.position;

        currentState = behaviorMode;
        previousState = behaviorMode;
        stateEnterTime = Time.time;
    }

    void Update()
    {
        if (movement == null || shipCore == null) return;
        UpdateBehavior();
    }

    void UpdateBehavior()
    {
        // Optimization: FindNearestEnemy typically calls Physics.OverlapSphere. 
        // Ensure this isn't too expensive if run every frame. 
        // For production, consider throttling this call too if lag persists.
        currentEnemy = FindNearestEnemy();

        AIBehaviorMode desiredState = DetermineTacticalState();

        if (desiredState != currentState)
        {
            ChangeState(desiredState);
        }

        switch (currentState)
        {
            case AIBehaviorMode.Wander: DoWander(); break;
            case AIBehaviorMode.Approach: DoApproach(); break;
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

    // ==========================================================
    // Core Behaviors (Y-locked)
    // ==========================================================

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    float LockY() => transform.position.y;

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

        // Increased buffer so we don't crash into them
        Vector3 approachPoint = target - Flat(toTarget).normalized * approachBuffer;
        approachPoint.y = LockY();

        movement.SetBoost(true, approachBoostMultiplier);
        movement.SetWaypoint(approachPoint, WaypointPriority.High);
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
    }

    void DoHold()
    {
        movement.ClearWaypoint(WaypointPriority.Medium);
        movement.SetBoost(false);
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

    // ==========================================================
    // Visual Debugging (Gizmos)
    // ==========================================================
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.white;
        style.fontSize = 12;
        style.fontStyle = FontStyle.Bold;
        Handles.Label(transform.position + Vector3.up * 2f, $"State: {currentState}", style);

        Handles.color = new Color(1, 0, 0, 0.05f);
        Handles.DrawSolidDisc(transform.position, Vector3.up, engagementRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, engagementRange);

        if (currentEnemy != null)
        {
            Gizmos.color = new Color(1, 1, 1, 0.5f);
            Gizmos.DrawLine(transform.position, currentEnemy.transform.position);

            Vector3 toTarget = currentEnemy.transform.position - transform.position;
            Vector3 approachPoint = currentEnemy.transform.position - Flat(toTarget).normalized * approachBuffer;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(approachPoint, 2f);
        }

        if (currentState == AIBehaviorMode.Wander && currentWanderTarget != Vector3.zero)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, currentWanderTarget);
            Gizmos.DrawWireSphere(currentWanderTarget, 2f);
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

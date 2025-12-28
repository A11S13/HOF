using UnityEngine;

/// <summary>
/// NPC ship AI behavior controller
/// </summary>
public class NPCBehavior : MonoBehaviour
{
    [Header("References")]
    private NPCShipMovement movement;
    private ShipCore shipCore;
    private ShipTargetingSystem targeting;

    [Header("Behavior Settings")]
    public AIBehaviorMode behaviorMode = AIBehaviorMode.Patrol;
    public float engagementRange = 100f;
    public float optimalRange = 50f;
    public float retreatRange = 20f;

    [Header("Patrol Settings")]
    public Transform[] patrolPoints;
    public float patrolSpeed = 5f;
    private int currentPatrolIndex = 0;

    [Header("Combat Settings")]
    public float circlingSpeed = 8f;
    public float circlingRadius = 40f;
    private Vector3 circlingCenter;
    private float circlingAngle;

    private GameObject currentEnemy;
    private AIBehaviorMode currentState;

    void Awake()
    {
        movement = GetComponent<NPCShipMovement>();
        shipCore = GetComponentInChildren<ShipCore>();
        targeting = GetComponentInChildren<ShipTargetingSystem>();
    }

    void Update()
    {
        if (movement == null || shipCore == null) return;

        UpdateBehavior();
    }

    void UpdateBehavior()
    {
        // Find nearest enemy
        currentEnemy = FindNearestEnemy();

        if (currentEnemy != null)
        {
            float distance = Vector3.Distance(transform.position, currentEnemy.transform.position);

            if (distance < engagementRange)
            {
                // Switch to combat
                if (distance > optimalRange)
                    currentState = AIBehaviorMode.Approach;
                else if (distance < retreatRange)
                    currentState = AIBehaviorMode.Retreat;
                else
                    currentState = AIBehaviorMode.Circle;
            }
            else
            {
                // Too far, go back to patrol
                currentState = behaviorMode;
            }
        }
        else
        {
            // No enemies, use default behavior
            currentState = behaviorMode;
        }

        // Execute behavior
        switch (currentState)
        {
            case AIBehaviorMode.Patrol:
                DoPatrol();
                break;
            case AIBehaviorMode.Approach:
                DoApproach();
                break;
            case AIBehaviorMode.Circle:
                DoCircle();
                break;
            case AIBehaviorMode.Retreat:
                DoRetreat();
                break;
            case AIBehaviorMode.Hold:
                DoHold();
                break;
        }
    }

    void DoPatrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            movement.ClearWaypoint();
            return;
        }

        Transform target = patrolPoints[currentPatrolIndex];
        movement.SetWaypoint(target.position);

        // Check if reached waypoint
        if (Vector3.Distance(transform.position, target.position) < 10f)
        {
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
        }
    }

    void DoApproach()
    {
        if (currentEnemy == null) return;

        Vector3 targetPos = currentEnemy.transform.position;
        Vector3 direction = (targetPos - transform.position).normalized;
        Vector3 approachPos = targetPos - direction * optimalRange;

        movement.SetWaypoint(approachPos);
    }

    void DoCircle()
    {
        if (currentEnemy == null) return;

        // Initialize circling
        if (circlingCenter != currentEnemy.transform.position)
        {
            circlingCenter = currentEnemy.transform.position;
            Vector3 toShip = transform.position - circlingCenter;
            circlingAngle = Mathf.Atan2(toShip.z, toShip.x);
        }

        // Update circle position
        circlingAngle += (circlingSpeed / circlingRadius) * Time.deltaTime;

        Vector3 offset = new Vector3(
            Mathf.Cos(circlingAngle) * circlingRadius,
            0f,
            Mathf.Sin(circlingAngle) * circlingRadius
        );

        Vector3 circlePos = currentEnemy.transform.position + offset;
        movement.SetWaypoint(circlePos);
    }

    void DoRetreat()
    {
        if (currentEnemy == null) return;

        Vector3 awayDirection = (transform.position - currentEnemy.transform.position).normalized;
        Vector3 retreatPos = transform.position + awayDirection * 30f;

        movement.SetWaypoint(retreatPos);
    }

    void DoHold()
    {
        movement.ClearWaypoint();
    }

    GameObject FindNearestEnemy()
    {
        if (targeting == null) return null;

        var targets = targeting.GetTargetDataArray();
        if (targets.Length == 0) return null;

        GameObject nearest = null;
        float minDist = float.MaxValue;

        foreach (var target in targets)
        {
            if (target.TargetObject == null) continue;

            float dist = Vector3.Distance(transform.position, target.TargetObject.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = target.TargetObject;
            }
        }

        return nearest;
    }

    void OnDrawGizmosSelected()
    {
        // Draw engagement ranges
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, engagementRange);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, optimalRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, retreatRange);

        // Draw patrol path
        if (patrolPoints != null && patrolPoints.Length > 1)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                if (patrolPoints[i] == null) continue;

                Vector3 current = patrolPoints[i].position;
                Vector3 next = patrolPoints[(i + 1) % patrolPoints.Length].position;

                Gizmos.DrawLine(current, next);
                Gizmos.DrawWireSphere(current, 5f);
            }
        }
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        // Draw current state
        Color stateColor = currentState switch
        {
            AIBehaviorMode.Patrol => Color.cyan,
            AIBehaviorMode.Approach => Color.yellow,
            AIBehaviorMode.Circle => Color.green,
            AIBehaviorMode.Retreat => Color.red,
            AIBehaviorMode.Hold => Color.gray,
            _ => Color.white
        };

        Gizmos.color = stateColor;
        Gizmos.DrawWireSphere(transform.position, 3f);

        // Draw line to current enemy
        if (currentEnemy != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, currentEnemy.transform.position);
        }

        // Draw waypoint
        if (movement != null && movement.HasWaypoint())
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, movement.GetCurrentWaypoint());
            Gizmos.DrawWireSphere(movement.GetCurrentWaypoint(), 2f);
        }
    }

}

public enum AIBehaviorMode
{
    Patrol,
    Approach,
    Circle,
    Retreat,
    Hold
}

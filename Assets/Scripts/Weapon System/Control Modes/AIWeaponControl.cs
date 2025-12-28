using UnityEngine;

/// <summary>
/// AI weapon control mode - fully autonomous targeting
/// </summary>
public class AIWeaponControl : IWeaponControlMode
{
    private GameObject currentTarget;
    private float targetLostUntil;
    private float nextReactionTime;
    private float reactionDelay;

    private Vector3 lastTargetPos;
    private Vector3 targetVel;
    private float velSampleTimer;

    const float VelSampleDt = 0.05f;

    public AIWeaponControl(float reactionDelay)
    {
        this.reactionDelay = reactionDelay;
        nextReactionTime = 0f;
        targetLostUntil = 0f;
    }

    public float UpdateControl(Weapon weapon)
    {
        // Validate current target
        if (currentTarget != null)
        {
            var t = currentTarget.GetComponent<Targetable>();
            if (t == null || !t.IsAlive)
            {
                weapon.RemoveTargetAssignment(currentTarget);
                currentTarget = null;
                targetLostUntil = Time.time + reactionDelay;
            }
        }

        // Pick new target if needed
        if (Time.time >= targetLostUntil)
            PickTarget(weapon);

        if (currentTarget == null)
            return -1f; // No target, don't fire

        // Update target velocity
        velSampleTimer -= Time.deltaTime;
        if (velSampleTimer <= 0f)
        {
            var pos = currentTarget.transform.position;
            targetVel = (pos - lastTargetPos) / VelSampleDt;
            lastTargetPos = pos;
            velSampleTimer = VelSampleDt;
        }

        // Rotate towards target
        var aimPos = GetAimPosition(weapon);
        weapon.RotateTowardsTarget(aimPos);

        // Check if aimed at target
        if (weapon.IsAimedAtTarget(aimPos))
        {
            return 1f - weapon.angularDistance / weapon.ShipCore.firingThresholdAngle;
        }

        return -1f; // Not aimed yet
    }

    public Vector3 GetAimPosition(Weapon weapon)
    {
        if (currentTarget == null)
            return weapon.transform.position + weapon.transform.forward * 100f;

        return weapon.CalculateAimPosition(currentTarget, targetVel);
    }

    private void PickTarget(Weapon weapon)
    {
        weapon.PrioritizeTargets();

        if (weapon.CombinedTargets.Length == 0)
        {
            currentTarget = null;
            return;
        }

        GameObject best = weapon.FindBestAvailableTarget();
        if (best == null)
        {
            currentTarget = weapon.CombinedTargets[0].TargetObject;
            weapon.AddTargetAssignment(currentTarget);
            return;
        }

        float jitter = 0.15f;
        float now = Time.time + Random.Range(-jitter, jitter);

        bool urgent = currentTarget != null &&
            (best.transform.position - weapon.transform.position).sqrMagnitude <
            (currentTarget.transform.position - weapon.transform.position).sqrMagnitude * 0.5f;

        if (best != currentTarget && (urgent || (weapon.ShouldSwitch(currentTarget, best) && now >= nextReactionTime)))
        {
            if (currentTarget != null)
                weapon.RemoveTargetAssignment(currentTarget);

            currentTarget = best;
            weapon.fireAccumulator += Random.Range(0f, 0.3f);

            lastTargetPos = currentTarget.transform.position;
            targetVel = Vector3.zero;
            weapon.AddTargetAssignment(currentTarget);

            nextReactionTime = now + reactionDelay;
        }
    }

    public GameObject GetCurrentTarget() => currentTarget;
}

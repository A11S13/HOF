using UnityEngine;

/// <summary>
/// AI weapon control mode - fully autonomous targeting
/// UPDATED: Added "Drill Mode" bypass for Fixed Weapons with Sector Scan
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

    // Tracking statistics
    private int targetSwitchCount = 0;
    private float lastTargetSwitchTime = 0f;

    public AIWeaponControl(float reactionDelay)
    {
        this.reactionDelay = reactionDelay;
        nextReactionTime = 0f;
        targetLostUntil = 0f;
    }

    public float UpdateControl(Weapon weapon)
    {
        // ----------------------------------------------------------------------
        // 1. "DRILL MODE" BYPASS (The Fix)
        // If this is a Fixed Weapon using Sector Scan, and we see SOMETHING:
        // Ignore all AI logic. Just shoot the closest thing in the list.
        // ----------------------------------------------------------------------
        if (weapon.lockRotation && weapon.useSectorScan && weapon.sectorScanResults.Count > 0)
        {
            // Pick the first object (Weapon.cs sorts them by distance, so [0] is closest)
            GameObject scanTarget = weapon.sectorScanResults[0];

            if (scanTarget != null)
            {
                // Force-set this as the target so BeamWeapon knows where to gimbal (if enabled)
                if (currentTarget != scanTarget)
                {
                    if (currentTarget != null) weapon.RemoveTargetAssignment(currentTarget);
                    currentTarget = scanTarget;
                    weapon.AddTargetAssignment(currentTarget);
                }

                // Force Update Rotation (resets to forward for fixed weapons)
                weapon.RotateTowardsTarget(scanTarget.transform.position);

                // Return MAX FIRE COMMAND immediately. 
                // No checks, no angles, no delays. Just Fire.
                return 1.0f;
            }
        }
        // ----------------------------------------------------------------------

        // 2. Standard Validation - Check if current target is still valid
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

        // 3. Target Selection (Standard Sonar Logic)
        if (Time.time >= targetLostUntil)
        {
            PickTarget(weapon);
        }

        if (currentTarget == null)
        {
            return -1f;
        }

        // 4. Velocity Calculation
        velSampleTimer -= Time.deltaTime;
        if (velSampleTimer <= 0f)
        {
            var pos = currentTarget.transform.position;
            targetVel = (pos - lastTargetPos) / VelSampleDt;
            lastTargetPos = pos;
            velSampleTimer = VelSampleDt;
        }

        // 5. Physical Rotation
        var aimPos = GetAimPosition(weapon);
        weapon.RotateTowardsTarget(aimPos);

        // 6. Fire Authorization (Standard)
        if (weapon.IsAimedAtTarget(currentTarget))
        {
            float quality = 1f - weapon.angularDistance / weapon.ShipCore.firingThresholdAngle;
            return Mathf.Clamp01(quality + 0.1f);
        }

        return -1f;
    }

    public Vector3 GetAimPosition(Weapon weapon)
    {
        if (currentTarget == null)
            return weapon.transform.position + weapon.transform.forward * 100f;

        return weapon.CalculateAimPosition(currentTarget, targetVel);
    }

    private void PickTarget(Weapon weapon)
    {
        // Standard Sonar Search
        weapon.PrioritizeTargets();

        if (weapon.CombinedTargets == null || weapon.CombinedTargets.Length == 0)
        {
            if (currentTarget != null)
            {
                weapon.RemoveTargetAssignment(currentTarget);
                currentTarget = null;
            }
            return;
        }

        GameObject best = weapon.FindBestAvailableTarget();

        if (best == null)
        {
            // Fallback
            if (currentTarget != null) weapon.RemoveTargetAssignment(currentTarget);
            currentTarget = weapon.CombinedTargets[0].TargetObject;
            weapon.AddTargetAssignment(currentTarget);
            lastTargetPos = currentTarget.transform.position;
            targetVel = Vector3.zero;
            return;
        }

        // Switch Logic
        bool urgent = currentTarget != null &&
            (best.transform.position - weapon.transform.position).sqrMagnitude <
            (currentTarget.transform.position - weapon.transform.position).sqrMagnitude * 0.5f;

        if (best != currentTarget && (urgent || (weapon.ShouldSwitch(currentTarget, best) && Time.time >= nextReactionTime)))
        {
            if (currentTarget != null) weapon.RemoveTargetAssignment(currentTarget);
            currentTarget = best;
            weapon.AddTargetAssignment(currentTarget);
            lastTargetPos = currentTarget.transform.position;
            targetVel = Vector3.zero;
            nextReactionTime = Time.time + reactionDelay;
        }
        else if (currentTarget == null)
        {
            currentTarget = best;
            weapon.AddTargetAssignment(currentTarget);
            lastTargetPos = currentTarget.transform.position;
            targetVel = Vector3.zero;
        }
    }

    public GameObject GetCurrentTarget() => currentTarget;
}
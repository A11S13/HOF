using UnityEngine;

public class BeamWeapon : Weapon
{
    [Header("Beam Stats")]
    public float damagePerSecond = 30f;
    public float maxBeamRange = 50f;
    public bool invertFireDirection = false;
    [Header("AI Logic")]
    [Tooltip("How long (in seconds) the beam keeps firing after losing a clear shot.")]
    public float fireLingerDuration = 0.5f;

    private float lastValidShotTime = -10f; // Tracks the last time we hit something


    [Header("Smart Gimbal")]
    [Tooltip("The arc within which the beam auto-locks.")]
    [Range(0f, 180f)]
    public float gimbalAngle = 30f;

    [Tooltip("CRITICAL: Set this to Enemy/Player layers so the beam can see them.")]
    public LayerMask targetMask;

    [Header("Beam Visuals")]
    public float beamWidth = 0.5f;
    public Color beamColor = Color.cyan;
    public Material beamMaterial;

    private LineRenderer lineRenderer;
    private int ownerLayer = -1;
    private Transform lockedTarget;
    private float lastScanTime;

    protected override void InitializeWeapon()
    {
        fireType = WeaponFireType.Continuous;

        // --- 1. SETUP SPAWN POINT (DEEP SEARCH) ---
        // We scan all children, grandchildren, etc.
        Transform[] allChildren = GetComponentsInChildren<Transform>(true); // 'true' includes inactive children
        foreach (Transform child in allChildren)
        {
            if (child.name == "projectileSpawnPoint")
            {
                weaponSpawnPoint = child;
                break;
            }
        }

        if (weaponSpawnPoint == null)
        {
            // Fallback if truly missing
            weaponSpawnPoint = transform;
            Debug.LogWarning($"{name}: Could not find 'projectileSpawnPoint' anywhere in children. Using weapon root.");
        }

        // --- 2. SETUP LINE RENDERER ---
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null) lineRenderer = gameObject.AddComponent<LineRenderer>();

        lineRenderer.positionCount = 2;
        lineRenderer.useWorldSpace = true;
        UpdateBeamVisuals();

        // --- 3. IGNORE SELF ---
        ownerLayer = transform.root.gameObject.layer;
    }


    void UpdateBeamVisuals()
    {
        if (lineRenderer == null) return;
        lineRenderer.startWidth = beamWidth;
        lineRenderer.endWidth = beamWidth;
        lineRenderer.startColor = beamColor;
        lineRenderer.endColor = new Color(beamColor.r, beamColor.g, beamColor.b, 0.5f);
        if (beamMaterial != null) lineRenderer.sharedMaterial = beamMaterial;
    }

    private void ScanForTarget(Vector3 startPos, Vector3 mountForward)
    {
        // Optimization: Scan 5 times per second
        if (Time.time < lastScanTime + 0.2f) return;
        lastScanTime = Time.time;

        // If we have a target, verify it is still valid
        if (lockedTarget != null)
        {
            Vector3 dir = (lockedTarget.position - startPos).normalized;
            float dot = Vector3.Dot(mountForward, dir);
            float minDot = Mathf.Cos(gimbalAngle * Mathf.Deg2Rad);

            // If valid (in angle and range), keep it
            if (dot >= minDot && Vector3.Distance(startPos, lockedTarget.position) <= maxBeamRange)
            {
                return;
            }
        }

        // Otherwise find a new best target
        lockedTarget = null;
        float bestDot = -1f;
        Collider[] hits = Physics.OverlapSphere(startPos, maxBeamRange, targetMask);
        float limitDot = Mathf.Cos(gimbalAngle * Mathf.Deg2Rad);

        foreach (var hit in hits)
        {
            if (hit.transform.root == transform.root) continue;

            Vector3 dirToEnemy = (hit.transform.position - startPos).normalized;
            float dot = Vector3.Dot(mountForward, dirToEnemy);

            if (dot >= limitDot)
            {
                // Prefer targets closer to the center of the crosshair
                if (dot > bestDot)
                {
                    bestDot = dot;
                    lockedTarget = hit.transform;
                }
            }
        }
    }

    protected override void Fire(float aimQuality)
    {
        if (weaponSpawnPoint == null) return;

        if (!lineRenderer.enabled) lineRenderer.enabled = true;
        UpdateBeamVisuals();

        Vector3 startPos = weaponSpawnPoint.position;
        Vector3 mountForward = weaponSpawnPoint.forward;
        if (invertFireDirection) mountForward = -mountForward;

        // 1. Scan logic
        ScanForTarget(startPos, mountForward);

        // 2. Determine Aim Direction
        Vector3 finalDirection = mountForward;

        if (lockedTarget != null)
        {
            Vector3 dirToTarget = (lockedTarget.position - startPos).normalized;
            finalDirection = Vector3.RotateTowards(mountForward, dirToTarget, gimbalAngle * Mathf.Deg2Rad, 0.0f);
        }

        Vector3 endPos = startPos + (finalDirection * maxBeamRange);

        // 3. Fire Raycast
        int layerMask = ~(1 << ownerLayer);
        RaycastHit hit;

        if (Physics.Raycast(startPos, finalDirection, out hit, maxBeamRange, layerMask))
        {
            endPos = hit.point;

            // FIX: Use GetComponentInParent to find the script even if we hit a child collider
            var damageable = hit.collider.GetComponentInParent<Damageable>();

            if (damageable != null)
            {
                damageable.Damage(damagePerSecond * Time.deltaTime);
            }
            else
            {
                // Fallback: Try looking for Health directly if Damageable is missing
                var health = hit.collider.GetComponentInParent<Health>();
                if (health != null)
                {
                    health.Damage(damagePerSecond * Time.deltaTime);
                }
            }
        }

        lineRenderer.SetPosition(0, startPos);
        lineRenderer.SetPosition(1, endPos);
    }


    protected override void StopFiring()
    {
        if (lineRenderer != null) lineRenderer.enabled = false;
        lockedTarget = null;
    }

    // --- AI LOGIC: RAYCAST TRIGGER ---
    public override Vector3 CalculateAimPosition(GameObject target, Vector3 targetVelocity)
    {
        if (target == null) return transform.position + transform.forward * 100f;

        Vector3 startPos = weaponSpawnPoint != null ? weaponSpawnPoint.position : transform.position;
        Vector3 mountForward = weaponSpawnPoint != null ? weaponSpawnPoint.forward : transform.forward;
        if (invertFireDirection) mountForward = -mountForward;

        // 1. Calculate the projected beam direction
        Vector3 dirToTarget = (target.transform.position - startPos).normalized;
        Vector3 projectedBeamDir = Vector3.RotateTowards(
            mountForward,
            dirToTarget,
            gimbalAngle * Mathf.Deg2Rad,
            0.0f
        );

        // 2. Raycast Check
        int layerMask = ~(1 << ownerLayer);
        RaycastHit hit;
        bool validHit = false;

        if (Physics.Raycast(startPos, projectedBeamDir, out hit, maxBeamRange, layerMask))
        {
            // Did we hit a valid target in the mask?
            if (((1 << hit.collider.gameObject.layer) & targetMask) != 0)
            {
                validHit = true;
                lastValidShotTime = Time.time; // Update our timestamp
            }
        }

        // 3. DECISION LOGIC: 
        // Keep firing if we have a Valid Hit OR if we are inside the "Linger Time"
        if (validHit || (Time.time < lastValidShotTime + fireLingerDuration))
        {
            // Force the AI to pull the trigger by returning a fake "perfect aim" position
            return transform.position + mountForward * 100f;
        }

        // Otherwise, return real target position (AI will rotate ship or stop firing)
        return target.transform.position;
    }

}

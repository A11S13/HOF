using UnityEngine;
using System.Collections;

public class BeamWeapon : Weapon
{
    [Header("Beam Stats")]
    public float damagePerSecond = 30f;
    public float maxBeamRange = 50f;
    public bool invertFireDirection = false;

    [Header("AI Logic")]
    public float fireLingerDuration = 0.5f;
    private float lastValidShotTime = -10f;

    [Header("Smart Gimbal")]
    [Range(0f, 180f)]
    public float gimbalAngle = 30f;
    public LayerMask targetMask;

    [Header("Beam Visuals")]
    public float beamWidth = 0.5f;
    public Color beamColor = Color.cyan;
    public Material beamMaterial;
    [Range(0f, 1f)]
    public float beamIntensityPulse = 0.3f;
    public float pulseSpeed = 5f;
    [Tooltip("Duration of beam fade-out when firing stops")]
    public float beamFadeOutDuration = 0.3f;

    private bool isFading = false;
    private Coroutine fadeCoroutine;

    [Header("VFX - Muzzle (Fire Zone)")]
    public GameObject muzzleFlashPrefab;
    public float muzzleFlashLifetime = 0.1f;
    public Vector3 muzzleFlashOffset = Vector3.zero;
    public Vector3 muzzleFlashRotation = Vector3.zero;
    private GameObject activeMuzzleFlash;
    private ParticleSystem muzzleParticles;
    private Transform muzzleFlashParent;

    [Header("VFX - Impact (Hit Zone)")]
    public GameObject impactEffectPrefab;
    [Tooltip("How often to spawn new impact effects (in seconds). Lower = more effects.")]
    public float impactSpawnInterval = 0.1f;
    [Tooltip("Lifetime of each impact effect before auto-destroy")]
    public float impactLifetime = 2f;
    private float lastImpactSpawnTime;
    private Vector3 lastHitPoint;
    private Vector3 lastHitNormal;
    private bool wasHittingSomething = false;

    [Header("VFX - Beam Core")]
    public GameObject beamCorePrefab;
    private GameObject activeBeamCore;
    private LineRenderer coreLineRenderer;

    private LineRenderer lineRenderer;
    private int ownerLayer = -1;
    private Transform lockedTarget;
    private float lastScanTime;

    // Store beam end position for fade-out
    private Vector3 lastBeamStart;
    private Vector3 lastBeamEnd;

    protected override void InitializeWeapon()
    {
        fireType = WeaponFireType.Continuous;

        Transform foundSpawnPoint = FindDeepChild(transform, "projectileSpawnPoint");

        if (foundSpawnPoint == null)
        {
            Transform[] allChildren = GetComponentsInChildren<Transform>(true);
            foreach (Transform child in allChildren)
            {
                if (child.name == "projectileSpawnPoint")
                {
                    foundSpawnPoint = child;
                    Debug.Log($"✓ Found projectileSpawnPoint at path: {GetGameObjectPath(child.gameObject)}");
                    break;
                }
            }
        }

        if (foundSpawnPoint != null)
        {
            weaponSpawnPoint = foundSpawnPoint;
            muzzleFlashParent = foundSpawnPoint;
            Debug.Log($"<color=green>✓ BeamWeapon initialized with spawn point: {GetGameObjectPath(weaponSpawnPoint.gameObject)}</color>");
        }
        else
        {
            weaponSpawnPoint = transform;
            muzzleFlashParent = transform;
            Debug.LogWarning($"<color=yellow>⚠ Could not find 'projectileSpawnPoint' in {name}. Using weapon root.</color>");
        }

        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null) lineRenderer = gameObject.AddComponent<LineRenderer>();

        lineRenderer.positionCount = 2;
        lineRenderer.useWorldSpace = true;
        lineRenderer.enabled = false;
        UpdateBeamVisuals();

        if (beamCorePrefab != null)
        {
            activeBeamCore = Instantiate(beamCorePrefab, transform);
            coreLineRenderer = activeBeamCore.GetComponent<LineRenderer>();
            if (coreLineRenderer != null)
            {
                coreLineRenderer.positionCount = 2;
                coreLineRenderer.useWorldSpace = true;
                coreLineRenderer.startWidth = beamWidth * 0.3f;
                coreLineRenderer.endWidth = beamWidth * 0.3f;
            }
            activeBeamCore.SetActive(false);
        }

        ownerLayer = transform.root.gameObject.layer;
    }

    private Transform FindDeepChild(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child;

            Transform result = FindDeepChild(child, childName);
            if (result != null)
                return result;
        }
        return null;
    }

    private string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        Transform parent = obj.transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }

    void UpdateBeamVisuals(float fadeMultiplier = 1f)
    {
        if (lineRenderer == null) return;

        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * beamIntensityPulse;
        pulse *= fadeMultiplier; // Apply fade

        lineRenderer.startWidth = beamWidth * pulse;
        lineRenderer.endWidth = beamWidth * pulse * 0.7f;

        Color startColor = beamColor * pulse;
        Color endColor = new Color(beamColor.r, beamColor.g, beamColor.b, 0.5f) * pulse;

        // Apply fade to alpha
        startColor.a *= fadeMultiplier;
        endColor.a *= fadeMultiplier;

        lineRenderer.startColor = startColor;
        lineRenderer.endColor = endColor;

        if (beamMaterial != null) lineRenderer.sharedMaterial = beamMaterial;

        // Update core beam fade
        if (coreLineRenderer != null)
        {
            coreLineRenderer.startWidth = beamWidth * 0.3f * pulse;
            coreLineRenderer.endWidth = beamWidth * 0.3f * pulse;

            Color coreColor = Color.white * pulse;
            coreColor.a *= fadeMultiplier;
            coreLineRenderer.startColor = coreColor;
            coreLineRenderer.endColor = coreColor;
        }
    }

    private void ScanForTarget(Vector3 startPos, Vector3 mountForward)
    {
        if (Time.time < lastScanTime + 0.2f) return;
        lastScanTime = Time.time;

        if (lockedTarget != null)
        {
            Vector3 dir = (lockedTarget.position - startPos).normalized;
            float dot = Vector3.Dot(mountForward, dir);
            float minDot = Mathf.Cos(gimbalAngle * Mathf.Deg2Rad);

            if (dot >= minDot && Vector3.Distance(startPos, lockedTarget.position) <= maxBeamRange)
            {
                return;
            }
        }

        lockedTarget = null;
        float bestDot = -1f;
        Collider[] hits = Physics.OverlapSphere(startPos, maxBeamRange, targetMask);
        float limitDot = Mathf.Cos(gimbalAngle * Mathf.Deg2Rad);

        foreach (var hit in hits)
        {
            if (hit.transform.root == transform.root) continue;

            Vector3 dirToEnemy = (hit.transform.position - startPos).normalized;
            float dot = Vector3.Dot(mountForward, dirToEnemy);

            if (dot >= limitDot && dot > bestDot)
            {
                bestDot = dot;
                lockedTarget = hit.transform;
            }
        }
    }

    protected override void Fire(float aimQuality)
    {
        if (weaponSpawnPoint == null) return;

        // Cancel fade-out if we start firing again
        if (isFading && fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            isFading = false;
        }

        if (!lineRenderer.enabled) lineRenderer.enabled = true;
        if (activeBeamCore != null && !activeBeamCore.activeSelf) activeBeamCore.SetActive(true);

        UpdateBeamVisuals(); // Full brightness
        ShowMuzzleFlash();

        Vector3 startPos = weaponSpawnPoint.position;
        Vector3 mountForward = weaponSpawnPoint.forward;
        if (invertFireDirection) mountForward = -mountForward;

        ScanForTarget(startPos, mountForward);

        Vector3 finalDirection = mountForward;

        if (lockedTarget != null)
        {
            Vector3 dirToTarget = (lockedTarget.position - startPos).normalized;
            finalDirection = Vector3.RotateTowards(mountForward, dirToTarget, gimbalAngle * Mathf.Deg2Rad, 0.0f);
        }

        Vector3 endPos = startPos + (finalDirection * maxBeamRange);

        int layerMask = ~(1 << ownerLayer);
        RaycastHit hit;
        bool hitSomething = false;

        if (Physics.Raycast(startPos, finalDirection, out hit, maxBeamRange, layerMask))
        {
            endPos = hit.point;
            hitSomething = true;

            lastHitPoint = hit.point;
            lastHitNormal = hit.normal;

            SpawnImpactEffect(hit.point, hit.normal);

            var damageable = hit.collider.GetComponentInParent<Damageable>();
            if (damageable != null)
            {
                damageable.Damage(damagePerSecond * Time.deltaTime);
            }
            else
            {
                var health = hit.collider.GetComponentInParent<Health>();
                if (health != null)
                {
                    health.Damage(damagePerSecond * Time.deltaTime);
                }
            }
        }

        // Store beam positions for fade-out
        lastBeamStart = startPos;
        lastBeamEnd = endPos;

        lineRenderer.SetPosition(0, startPos);
        lineRenderer.SetPosition(1, endPos);

        if (coreLineRenderer != null)
        {
            coreLineRenderer.SetPosition(0, startPos);
            coreLineRenderer.SetPosition(1, endPos);
        }

        if (!hitSomething && wasHittingSomething)
        {
            SpawnImpactEffect(lastHitPoint, lastHitNormal);
        }

        wasHittingSomething = hitSomething;
    }

    void ShowMuzzleFlash()
    {
        if (muzzleFlashPrefab == null || muzzleFlashParent == null) return;

        if (activeMuzzleFlash == null)
        {
            activeMuzzleFlash = Instantiate(muzzleFlashPrefab, muzzleFlashParent);
            activeMuzzleFlash.transform.localPosition = muzzleFlashOffset;
            activeMuzzleFlash.transform.localRotation = Quaternion.Euler(muzzleFlashRotation);

            muzzleParticles = activeMuzzleFlash.GetComponent<ParticleSystem>();
            if (muzzleParticles == null)
            {
                muzzleParticles = activeMuzzleFlash.GetComponentInChildren<ParticleSystem>();
            }

            Debug.Log($"<color=cyan>✓ Muzzle flash created at: {GetGameObjectPath(activeMuzzleFlash)}</color>");
        }

        if (!activeMuzzleFlash.activeSelf)
        {
            activeMuzzleFlash.SetActive(true);
        }

        if (muzzleParticles != null && !muzzleParticles.isPlaying)
        {
            muzzleParticles.Play();
        }
    }

    void SpawnImpactEffect(Vector3 position, Vector3 normal)
    {
        if (impactEffectPrefab == null) return;

        if (Time.time - lastImpactSpawnTime < impactSpawnInterval)
            return;

        lastImpactSpawnTime = Time.time;

        GameObject impact = Instantiate(impactEffectPrefab, position, Quaternion.LookRotation(normal));

        ParticleSystem[] particles = impact.GetComponentsInChildren<ParticleSystem>();

        foreach (var ps in particles)
        {
            var main = ps.main;
            main.loop = false;
            main.stopAction = ParticleSystemStopAction.Destroy;
            ps.Play();
        }

        Destroy(impact, impactLifetime);
    }

    protected override void StopFiring()
    {
        // Start fade-out instead of instant disable
        if (!isFading && lineRenderer != null && lineRenderer.enabled)
        {
            fadeCoroutine = StartCoroutine(FadeOutBeam());
        }

        HideMuzzleFlash();

        lockedTarget = null;
        wasHittingSomething = false;
    }

    IEnumerator FadeOutBeam()
    {
        isFading = true;
        float elapsed = 0f;

        // Keep beam position frozen during fade
        if (lineRenderer != null)
        {
            lineRenderer.SetPosition(0, lastBeamStart);
            lineRenderer.SetPosition(1, lastBeamEnd);
        }

        if (coreLineRenderer != null)
        {
            coreLineRenderer.SetPosition(0, lastBeamStart);
            coreLineRenderer.SetPosition(1, lastBeamEnd);
        }

        while (elapsed < beamFadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float fadeAmount = 1f - (elapsed / beamFadeOutDuration); // 1.0 → 0.0

            // Apply smooth fade curve (ease-out)
            fadeAmount = Mathf.SmoothStep(1f, 0f, elapsed / beamFadeOutDuration);

            UpdateBeamVisuals(fadeAmount);

            yield return null;
        }

        // Fully disable after fade
        if (lineRenderer != null) lineRenderer.enabled = false;
        if (activeBeamCore != null) activeBeamCore.SetActive(false);

        isFading = false;
    }

    void HideMuzzleFlash()
    {
        if (activeMuzzleFlash != null)
        {
            activeMuzzleFlash.SetActive(false);
        }
    }

    void OnDestroy()
    {
        if (activeMuzzleFlash != null) Destroy(activeMuzzleFlash);
        if (activeBeamCore != null) Destroy(activeBeamCore);
    }

    public override Vector3 CalculateAimPosition(GameObject target, Vector3 targetVelocity)
    {
        if (target == null) return transform.position + transform.forward * 100f;

        Vector3 startPos = weaponSpawnPoint != null ? weaponSpawnPoint.position : transform.position;
        Vector3 mountForward = weaponSpawnPoint != null ? weaponSpawnPoint.forward : transform.forward;
        if (invertFireDirection) mountForward = -mountForward;

        Vector3 dirToTarget = (target.transform.position - startPos).normalized;
        Vector3 projectedBeamDir = Vector3.RotateTowards(
            mountForward,
            dirToTarget,
            gimbalAngle * Mathf.Deg2Rad,
            0.0f
        );

        int layerMask = ~(1 << ownerLayer);
        RaycastHit hit;
        bool validHit = false;

        if (Physics.Raycast(startPos, projectedBeamDir, out hit, maxBeamRange, layerMask))
        {
            if (((1 << hit.collider.gameObject.layer) & targetMask) != 0)
            {
                validHit = true;
                lastValidShotTime = Time.time;
            }
        }

        if (validHit || (Time.time < lastValidShotTime + fireLingerDuration))
        {
            return transform.position + mountForward * 100f;
        }

        return target.transform.position;
    }

    void OnDrawGizmosSelected()
    {
        if (weaponSpawnPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(weaponSpawnPoint.position, 0.2f);
            Gizmos.DrawRay(weaponSpawnPoint.position, weaponSpawnPoint.forward * 2f);
        }
    }
}

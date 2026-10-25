using UnityEngine;
using System.Collections;

/// <summary>
/// BeamWeapon - Continuous fire weapon
/// FIXED: Improved gimbal control for fixed weapons
/// UPDATED: Passes RaycastHit point to Asteroid for directional fracture
/// </summary>
public class BeamWeapon : Weapon
{
    [Header("Beam Stats")]
    public float damagePerSecond = 30f;
    public float maxBeamRange = 50f;
    public bool invertFireDirection = false;

    [Header("Beam Collision")]
    public float beamCollisionWidth = 1f;
    public float beamCollisionHeight = 30f;
    public float beamCollisionDepth = 1f;

    [Header("Smart Gimbal")]
    [Range(0f, 180f)]
    public float gimbalAngle = 30f;
    public LayerMask targetMask;
    [Tooltip("Disable gimbal when weapon is locked (fixed mount). Recommended for fixed weapons.")]
    public bool disableGimbalWhenLocked = true;

    [Header("Beam Visuals")]
    public float beamWidth = 0.5f;
    public Color beamColor = Color.cyan;
    public Material beamMaterial;
    [Range(0f, 1f)]
    public float beamIntensityPulse = 0.3f;
    public float pulseSpeed = 5f;
    public float beamFadeOutDuration = 0.3f;

    private bool isFading = false;
    private Coroutine fadeCoroutine;

    [Header("VFX")]
    public GameObject muzzleFlashPrefab;
    public GameObject impactEffectPrefab;
    public GameObject beamCorePrefab;

    // RESTORED VARIABLES
    [Tooltip("How often to spawn new impact effects (in seconds). Lower = more effects.")]
    public float impactSpawnInterval = 0.1f;
    [Tooltip("Lifetime of each impact effect before auto-destroy")]
    public float impactLifetime = 2f;

    // Internal VFX State
    private GameObject activeMuzzleFlash;
    private ParticleSystem muzzleParticles;
    private Transform muzzleFlashParent;
    private GameObject activeBeamCore;
    private LineRenderer coreLineRenderer;
    private LineRenderer lineRenderer;
    private float lastImpactSpawnTime;
    private int ownerLayer = -1;

    // Beam position memory
    private Vector3 lastBeamStart;
    private Vector3 lastBeamEnd;

    protected override void InitializeWeapon()
    {
        fireType = WeaponFireType.Continuous;

        // 1. Setup Spawn Point
        Transform foundSpawnPoint = FindDeepChild(transform, "projectileSpawnPoint");
        weaponSpawnPoint = foundSpawnPoint != null ? foundSpawnPoint : transform;
        muzzleFlashParent = weaponSpawnPoint;

        // 2. Setup Line Renderer
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null) lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 2;
        lineRenderer.useWorldSpace = true;
        lineRenderer.enabled = false;

        // 3. Setup Beam Core
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
        UpdateBeamVisuals();
    }

    // Logic: Called by Weapon.cs when AI or Player pulls trigger
    protected override void Fire(float aimQuality)
    {
        if (weaponSpawnPoint == null) return;

        // Reset Fade if re-firing
        if (isFading && fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            isFading = false;
        }

        // Enable Visuals
        if (!lineRenderer.enabled) lineRenderer.enabled = true;
        if (activeBeamCore != null && !activeBeamCore.activeSelf) activeBeamCore.SetActive(true);
        UpdateBeamVisuals();
        ShowMuzzleFlash();

        // --- FIXED AIMING LOGIC ---
        Vector3 startPos = weaponSpawnPoint.position;
        Vector3 mountForward = weaponSpawnPoint.forward;
        if (invertFireDirection) mountForward = -mountForward;

        Vector3 finalDirection = mountForward;

        // FIX: Disable gimbal for locked weapons (they rely on ship rotation)
        if (lockRotation && disableGimbalWhenLocked)
        {
            // Fixed weapons should fire straight ahead, no gimbal adjustment
            finalDirection = mountForward;
        }
        else
        {
            // Original gimbal logic for turrets
            // If we have a target from Weapon.cs logic, try to gimbal towards it
            GameObject currentTarget = (controlMode as AIWeaponControl)?.GetCurrentTarget();

            if (currentTarget != null)
            {
                Vector3 dirToTarget = (currentTarget.transform.position - startPos).normalized;

                // Check if target is within gimbal limits
                if (Vector3.Angle(mountForward, dirToTarget) <= gimbalAngle)
                {
                    // Allowed to aim at target
                    finalDirection = dirToTarget;
                }
                else
                {
                    // Clamp to max gimbal angle
                    finalDirection = Vector3.RotateTowards(mountForward, dirToTarget, gimbalAngle * Mathf.Deg2Rad, 0.0f);
                }
            }
            else if (!isAI && prevTarget != null) // Player Manual Case
            {
                finalDirection = mountForward;
            }
        }

        // Perform the Hit Scan
        Vector3 endPos = startPos + (finalDirection * maxBeamRange);
        int layerMask = ~(1 << ownerLayer);
        RaycastHit hit;

        // 30-unit vertical box cast
        Vector3 boxHalfExtents = new Vector3(beamCollisionWidth * 0.5f, beamCollisionHeight * 0.5f, beamCollisionDepth * 0.5f);
        bool hitSomething = Physics.BoxCast(startPos, boxHalfExtents, finalDirection, out hit, Quaternion.LookRotation(finalDirection), maxBeamRange, layerMask);

        if (hitSomething)
        {
            endPos = hit.point;
            SpawnImpactEffect(hit.point, hit.normal);

            // UPDATED: Pass the hit.point into ApplyDamage
            ApplyDamage(hit.collider, hit.point);
        }

        // Update Visuals
        lastBeamStart = startPos;
        lastBeamEnd = endPos;

        lineRenderer.SetPosition(0, startPos);
        lineRenderer.SetPosition(1, endPos);

        if (coreLineRenderer != null)
        {
            coreLineRenderer.SetPosition(0, startPos);
            coreLineRenderer.SetPosition(1, endPos);
        }
    }

    // UPDATED: Now accepts the hit point to pass to the asteroid
    private void ApplyDamage(Collider hitCollider, Vector3 hitPoint)
    {
        float damageAmount = damagePerSecond * Time.deltaTime;
        var asteroid = hitCollider.GetComponentInParent<Asteroid>();
        if (asteroid != null)
        {
            // Pass the exact hit point to the asteroid so chunks fly away from the laser
            asteroid.TakeDamage(damageAmount, hitPoint);
            return;
        }

        var damageable = hitCollider.GetComponentInParent<Damageable>();
        if (damageable != null) damageable.Damage(damageAmount);
        else
        {
            var health = hitCollider.GetComponentInParent<Health>();
            if (health != null) health.Damage(damageAmount);
        }
    }

    void UpdateBeamVisuals(float fadeMultiplier = 1f)
    {
        if (lineRenderer == null) return;
        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * beamIntensityPulse;
        pulse *= fadeMultiplier;

        lineRenderer.startWidth = beamWidth * pulse;
        lineRenderer.endWidth = beamWidth * pulse * 0.7f;

        Color startColor = beamColor * pulse;
        Color endColor = new Color(beamColor.r, beamColor.g, beamColor.b, 0.5f) * pulse;
        startColor.a *= fadeMultiplier;
        endColor.a *= fadeMultiplier;

        lineRenderer.startColor = startColor;
        lineRenderer.endColor = endColor;
        if (beamMaterial != null) lineRenderer.sharedMaterial = beamMaterial;

        if (coreLineRenderer != null)
        {
            coreLineRenderer.startWidth = beamWidth * 0.3f * pulse;
            coreLineRenderer.endWidth = beamWidth * 0.3f * pulse;
            Color coreC = Color.white * pulse;
            coreC.a *= fadeMultiplier;
            coreLineRenderer.startColor = coreC;
            coreLineRenderer.endColor = coreC;
        }
    }

    protected override void StopFiring()
    {
        if (!isFading && lineRenderer != null && lineRenderer.enabled)
            fadeCoroutine = StartCoroutine(FadeOutBeam());

        HideMuzzleFlash();
    }

    IEnumerator FadeOutBeam()
    {
        isFading = true;
        float elapsed = 0f;

        if (lineRenderer != null) { lineRenderer.SetPosition(0, lastBeamStart); lineRenderer.SetPosition(1, lastBeamEnd); }
        if (coreLineRenderer != null) { coreLineRenderer.SetPosition(0, lastBeamStart); coreLineRenderer.SetPosition(1, lastBeamEnd); }

        while (elapsed < beamFadeOutDuration)
        {
            elapsed += Time.deltaTime;
            UpdateBeamVisuals(Mathf.SmoothStep(1f, 0f, elapsed / beamFadeOutDuration));
            yield return null;
        }

        if (lineRenderer != null) lineRenderer.enabled = false;
        if (activeBeamCore != null) activeBeamCore.SetActive(false);
        isFading = false;
    }

    void ShowMuzzleFlash()
    {
        if (muzzleFlashPrefab == null) return;
        if (activeMuzzleFlash == null)
        {
            activeMuzzleFlash = Instantiate(muzzleFlashPrefab, muzzleFlashParent);
            activeMuzzleFlash.transform.localPosition = Vector3.zero;
            activeMuzzleFlash.transform.localRotation = Quaternion.identity;
            muzzleParticles = activeMuzzleFlash.GetComponentInChildren<ParticleSystem>();
        }
        if (!activeMuzzleFlash.activeSelf) activeMuzzleFlash.SetActive(true);
        if (muzzleParticles != null && !muzzleParticles.isPlaying) muzzleParticles.Play();
    }

    void HideMuzzleFlash() { if (activeMuzzleFlash != null) activeMuzzleFlash.SetActive(false); }

    // UPDATED IMPACT EFFECT LOGIC (Fixes invisible effects)
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
            // Removed: main.stopAction = ParticleSystemStopAction.Destroy; (This was the bug)
            ps.Play();
        }

        Destroy(impact, impactLifetime);
    }

    private Transform FindDeepChild(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName) return child;
            Transform result = FindDeepChild(child, childName);
            if (result != null) return result;
        }
        return null;
    }
}
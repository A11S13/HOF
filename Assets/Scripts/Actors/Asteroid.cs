using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(Targetable))]
public class Asteroid : MonoBehaviour
{
    [Header("Movement & Orbit Settings")]
    public float orbitalSpeed = 0.01f;
    public float speedVariation = 0.005f;
    public float rotationSpeed = 0.5f;

    [Header("Hierarchy & Layer Tiers")]
    public int currentLayerTier = 1;

    [Header("Demolition & Durability")]
    public float explosionForce = 2f;

    [Header("Chunk Physics")]
    public float chunkRotationSpeed = 3f;

    [Header("Fragments (Assign these in the Prefab)")]
    public GameObject[] tier2Fragments;
    public GameObject[] tier3Fragments;

    [Header("Optimization Settings")]
    public float debrisLifetime = 15f;
    public float debrisLifetimeVariation = 3f;
    public float fadeDuration = 1.5f;
    public bool collideWithSiblings = false;

    [Header("Rotation Inheritance (applies at moment of fracture)")]
    [Range(0f, 2f)] public float orbitalInheritance = 1f;
    [Range(0f, 2f)] public float spinInheritance = 1f;

    private static PhysicsMaterial cachedRockMaterial;
    private static Transform cachedPlayerTransform;

    private Rigidbody rb;
    private Health healthComponent;
    private Material cachedMaterial;
    private bool isFractured;
    private float randomizedOrbitalSpeed;
    private float spawnTime;
    private float nextDistanceCheckTime;
    private const float spawnImmunityDuration = 1.5f;

    // OPTIMIZATION: Cache the transform to avoid native C++ calls
    private Transform cachedTransform;

    // OPTIMIZATION: Pre-allocate lists to prevent garbage collection spikes during fracturing
    private static readonly List<GameObject> spawnedFragments = new List<GameObject>(10);
    private static readonly List<Rigidbody> bodies = new List<Rigidbody>(10);
    private static readonly List<Collider> allColliders = new List<Collider>(20);
    private static readonly List<Vector3> targetPositions = new List<Vector3>(10);
    private static readonly List<Quaternion> targetRotations = new List<Quaternion>(10);
    private static readonly List<Vector3> targetVelocities = new List<Vector3>(10);
    private static readonly List<Vector3> targetAngularVelocities = new List<Vector3>(10);

    void Awake()
    {
        cachedTransform = transform;
        rb = GetComponent<Rigidbody>();
        healthComponent = GetComponent<Health>();

        MeshRenderer renderer = GetComponentInChildren<MeshRenderer>();
        if (renderer != null) cachedMaterial = renderer.sharedMaterial;

        ConfigureRigidbody(rb);

        if (cachedRockMaterial == null)
        {
            cachedRockMaterial = new PhysicsMaterial("AsteroidRock");
            cachedRockMaterial.bounciness = 0.1f;
            cachedRockMaterial.dynamicFriction = 0.8f;
            cachedRockMaterial.staticFriction = 0.8f;
            cachedRockMaterial.bounceCombine = PhysicsMaterialCombine.Minimum;
            cachedRockMaterial.frictionCombine = PhysicsMaterialCombine.Average;
        }
    }

    void Start()
    {
        if (cachedPlayerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) cachedPlayerTransform = player.transform;
        }

        randomizedOrbitalSpeed = orbitalSpeed + Random.Range(-speedVariation, speedVariation);
        spawnTime = Time.time;
        nextDistanceCheckTime = Time.time + Random.Range(0f, 2f);

        if (currentLayerTier == 3)
        {
            float actualLifetime = debrisLifetime + Random.Range(-debrisLifetimeVariation, debrisLifetimeVariation);
            actualLifetime = Mathf.Max(fadeDuration, actualLifetime);
            StartCoroutine(FadeAndDestroyRoutine(actualLifetime, fadeDuration));
        }
    }

    private IEnumerator FadeAndDestroyRoutine(float totalLifetime, float fadeTime)
    {
        yield return new WaitForSeconds(totalLifetime - fadeTime);

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        Vector3 initialScale = cachedTransform.localScale;
        float elapsed = 0f;

        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeTime;
            cachedTransform.localScale = Vector3.Lerp(initialScale, Vector3.zero, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        Destroy(gameObject);
    }

    void FixedUpdate()
    {
        if (currentLayerTier == 1)
            HandleMovement();

        if (Time.time > nextDistanceCheckTime && cachedPlayerTransform != null)
        {
            nextDistanceCheckTime = Time.time + 2f;
            if (currentLayerTier > 1 && (cachedTransform.position - cachedPlayerTransform.position).sqrMagnitude > 2250000f)
                Destroy(gameObject);
        }
    }

    private void HandleMovement()
    {
        cachedTransform.RotateAround(Vector3.zero, Vector3.up, randomizedOrbitalSpeed * Time.fixedDeltaTime);
        cachedTransform.Rotate(Vector3.up, rotationSpeed * 10f * Time.fixedDeltaTime, Space.Self);
    }

    private static void ConfigureRigidbody(Rigidbody body)
    {
        if (body == null) return;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.None;
        body.collisionDetectionMode = CollisionDetectionMode.Discrete;
        body.constraints = RigidbodyConstraints.FreezePositionY;
    }

    public void SetupStats(int tier)
    {
        currentLayerTier = tier;
        float newMass = 200f;
        float newHealth = 300f;
        if (tier == 2) { newMass = 50f; newHealth = 150f; }
        else if (tier == 3) { newMass = 10f; newHealth = 75f; }

        if (rb != null) rb.mass = newMass;
        if (healthComponent != null)
        {
            healthComponent.max = newHealth;
            healthComponent.current = newHealth;
        }
    }

    public void Initialize() { }
    public void Initialize(float sizeMultiplier) { cachedTransform.localScale = Vector3.one * sizeMultiplier; }
    public void Initialize(int tier) { SetupStats(tier); }
    public void Initialize(Vector3 pos, Quaternion rot, float scale, int tier)
    {
        cachedTransform.SetPositionAndRotation(pos, rot);
        cachedTransform.localScale = Vector3.one * scale;
        SetupStats(tier);
    }

    public void TakeDamage(float amount) => TakeDamage(amount, cachedTransform.position);

    public void TakeDamage(float amount, Vector3 impactPoint)
    {
        if (isFractured || Time.time < spawnTime + spawnImmunityDuration) return;

        healthComponent?.Damage(amount);

        if (currentLayerTier >= 3 || (healthComponent != null && healthComponent.current <= 0))
            Fracture(impactPoint);
    }

    private void Fracture(Vector3 impactCenter)
    {
        if (isFractured) return;
        isFractured = true;

        Collider myCollider = GetComponent<Collider>();
        if (myCollider != null) myCollider.enabled = false;

        if (currentLayerTier >= 3)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 parentPosition = cachedTransform.position;
        Quaternion parentRotation = cachedTransform.rotation;
        Vector3 parentScale = cachedTransform.lossyScale;

        Vector3 parentLinearVel = Vector3.zero;
        Vector3 parentAngularVel = Vector3.zero;

        if (currentLayerTier == 1)
        {
            Vector3 flatPos = new Vector3(parentPosition.x, 0f, parentPosition.z);
            float orbitRadius = flatPos.magnitude;
            Vector3 orbitTangent = orbitRadius > 0.01f ? Vector3.Cross(Vector3.up, flatPos / orbitRadius) : Vector3.right;
            parentLinearVel = orbitTangent * (randomizedOrbitalSpeed * Mathf.Deg2Rad * orbitRadius);
            parentAngularVel = Vector3.up * (rotationSpeed * 10f * Mathf.Deg2Rad);
        }
        else if (rb != null)
        {
            parentLinearVel = rb.linearVelocity;
            parentLinearVel.y = 0f;
            parentAngularVel = rb.angularVelocity;
        }

        GameObject[] matchingPrefabs;
        int nextTier;

        if (tier2Fragments != null && tier2Fragments.Length > 0 && (tier3Fragments == null || tier3Fragments.Length == 0))
        {
            matchingPrefabs = tier2Fragments;
            nextTier = 2;
        }
        else if (tier3Fragments != null && tier3Fragments.Length > 0)
        {
            matchingPrefabs = tier3Fragments;
            nextTier = 3;
        }
        else
        {
            matchingPrefabs = (currentLayerTier == 1) ? tier2Fragments : tier3Fragments;
            nextTier = currentLayerTier + 1;
        }

        if (matchingPrefabs == null || matchingPrefabs.Length == 0)
        {
            Debug.LogError($"[ASTEROID ERROR] '{gameObject.name}' vanished because its Tier {nextTier} Fragments array is EMPTY!");
            Destroy(gameObject);
            return;
        }

        GameObject assemblyHolder = new GameObject($"{gameObject.name}_AssemblyHolder");
        Transform holderTransform = assemblyHolder.transform;
        holderTransform.position = parentPosition;
        holderTransform.rotation = parentRotation * Quaternion.Euler(90f, 0f, 0f);
        holderTransform.localScale = parentScale;

        // OPTIMIZATION: Clear pre-allocated lists instead of making new ones
        spawnedFragments.Clear();
        bodies.Clear();
        allColliders.Clear();
        targetPositions.Clear();
        targetRotations.Clear();
        targetVelocities.Clear();
        targetAngularVelocities.Clear();

        foreach (GameObject prefab in matchingPrefabs)
        {
            GameObject fragment = Instantiate(prefab, holderTransform);
            fragment.SetActive(false);
            spawnedFragments.Add(fragment);

            if (cachedMaterial != null)
            {
                MeshRenderer renderer = fragment.GetComponentInChildren<MeshRenderer>();
                if (renderer != null) renderer.sharedMaterial = cachedMaterial;
            }
        }

        // OPTIMIZATION: World-space AABB bounds are much faster than iterating 8 corners per renderer
        Bounds localBounds = new Bounds();
        bool boundsInit = false;
        foreach (var frag in spawnedFragments)
        {
            Renderer r = frag.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                Vector3 localCenter = holderTransform.InverseTransformPoint(r.bounds.center);
                Vector3 localExtents = r.bounds.extents; // Approximation of rotated bounds, much faster
                Bounds b = new Bounds(localCenter, localExtents * 2f);

                if (!boundsInit) { localBounds = b; boundsInit = true; }
                else localBounds.Encapsulate(b);
            }
        }

        Vector3 localCenterOffset = boundsInit ? localBounds.center : Vector3.zero;
        foreach (var frag in spawnedFragments)
        {
            frag.transform.localPosition -= localCenterOffset;
        }

        Physics.SyncTransforms();

        foreach (GameObject fragment in spawnedFragments)
        {
            Vector3 finalWorldPos = fragment.transform.position;
            Quaternion finalWorldRot = fragment.transform.rotation;

            targetPositions.Add(finalWorldPos);
            targetRotations.Add(finalWorldRot);

            Vector3 offsetFromParent = finalWorldPos - parentPosition;
            offsetFromParent.y = 0f;

            Vector3 tangentialVel = Vector3.Cross(parentAngularVel, offsetFromParent) * orbitalInheritance;
            Vector3 rigidBodyVel = parentLinearVel + tangentialVel;

            Vector3 explosionDir = finalWorldPos - impactCenter;
            explosionDir.y = 0f;
            if (explosionDir.sqrMagnitude < 0.001f)
                explosionDir = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
            explosionDir.Normalize();

            targetVelocities.Add(rigidBodyVel + explosionDir * explosionForce);

            Vector3 randomTumble = new Vector3(
                Random.Range(-chunkRotationSpeed, chunkRotationSpeed),
                Random.Range(-chunkRotationSpeed, chunkRotationSpeed),
                Random.Range(-chunkRotationSpeed, chunkRotationSpeed));

            Vector3 inheritedSpin = parentAngularVel * spinInheritance;
            targetAngularVelocities.Add(inheritedSpin + randomTumble);

            // OPTIMIZATION: DO NOT use AddComponent. Ensure the prefab already has these components!
            Asteroid asteroid = fragment.GetComponent<Asteroid>();
            Rigidbody body = fragment.GetComponent<Rigidbody>();

            asteroid.rb = body;
            asteroid.SetupStats(nextTier);
            asteroid.orbitalSpeed = orbitalSpeed;
            asteroid.speedVariation = speedVariation;
            asteroid.rotationSpeed = rotationSpeed;
            asteroid.spawnTime = Time.time;
            asteroid.debrisLifetime = debrisLifetime;
            asteroid.debrisLifetimeVariation = debrisLifetimeVariation;
            asteroid.fadeDuration = fadeDuration;
            asteroid.collideWithSiblings = collideWithSiblings;
            asteroid.orbitalInheritance = orbitalInheritance;
            asteroid.spinInheritance = spinInheritance;

            fragment.tag = gameObject.tag;
            SetLayerRecursively(fragment, gameObject.layer);

            body.isKinematic = true;
            ConfigureRigidbody(body);
            body.centerOfMass = Vector3.zero;
            body.inertiaTensorRotation = Quaternion.identity;
            body.maxDepenetrationVelocity = 0.5f;

            Collider[] cols = fragment.GetComponentsInChildren<Collider>(true);
            foreach (var c in cols)
            {
                c.material = cachedRockMaterial;
                allColliders.Add(c);
            }
            bodies.Add(body);
        }

        for (int i = 0; i < bodies.Count; i++)
        {
            bodies[i].transform.SetParent(null, true);
            bodies[i].gameObject.SetActive(true);
            bodies[i].position = targetPositions[i];
            bodies[i].rotation = targetRotations[i];
        }

        if (!collideWithSiblings)
        {
            for (int i = 0; i < allColliders.Count; i++)
                for (int j = i + 1; j < allColliders.Count; j++)
                    if (allColliders[i] != null && allColliders[j] != null)
                        Physics.IgnoreCollision(allColliders[i], allColliders[j], true);
        }

        for (int i = 0; i < bodies.Count; i++)
        {
            bodies[i].isKinematic = false;
            bodies[i].linearVelocity = targetVelocities[i];
            bodies[i].angularVelocity = targetAngularVelocities[i];
            bodies[i].interpolation = RigidbodyInterpolation.None;
        }

        Physics.SyncTransforms();

        Destroy(assemblyHolder);
        Destroy(gameObject);
    }

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null) return;
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            if (child == null) continue;
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }
}
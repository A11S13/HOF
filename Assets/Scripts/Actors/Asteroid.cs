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

    [Header("Fragments (Assign these in the Prefab)")]
    [Tooltip("Prefabs to spawn when this asteroid breaks into Tier 2 (Medium).")]
    public GameObject[] tier2Fragments;
    [Tooltip("Prefabs to spawn when this asteroid breaks into Tier 3 (Small).")]
    public GameObject[] tier3Fragments;

    [Header("Optimization Settings")]
    public float debrisLifetime = 15f;
    public float debrisLifetimeVariation = 3f;
    public float fadeDuration = 1.5f;
    public bool collideWithSiblings = false;

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

    void Awake()
    {
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

        Vector3 initialScale = transform.localScale;
        float elapsed = 0f;

        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeTime;
            transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        Destroy(gameObject);
    }

    void FixedUpdate()
    {
        if (currentLayerTier == 1)
        {
            HandleMovement();
        }

        if (Time.time > nextDistanceCheckTime && cachedPlayerTransform != null)
        {
            nextDistanceCheckTime = Time.time + 2f;

            if (currentLayerTier > 1 && (transform.position - cachedPlayerTransform.position).sqrMagnitude > 2250000f)
            {
                Destroy(gameObject);
            }
        }
    }

    private void HandleMovement()
    {
        transform.RotateAround(Vector3.zero, Vector3.up, randomizedOrbitalSpeed * Time.fixedDeltaTime);
        transform.Rotate(Vector3.up, rotationSpeed * 10f * Time.fixedDeltaTime, Space.Self);
    }

    private static void ConfigureRigidbody(Rigidbody body)
    {
        if (body == null) return;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.None;
        body.collisionDetectionMode = CollisionDetectionMode.Discrete;
        body.constraints = RigidbodyConstraints.FreezePositionY |
                           RigidbodyConstraints.FreezeRotationX |
                           RigidbodyConstraints.FreezeRotationZ;
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
    public void Initialize(float sizeMultiplier) { transform.localScale = Vector3.one * sizeMultiplier; }
    public void Initialize(int tier) { SetupStats(tier); }
    public void Initialize(Vector3 pos, Quaternion rot, float scale, int tier)
    {
        transform.SetPositionAndRotation(pos, rot);
        transform.localScale = Vector3.one * scale;
        SetupStats(tier);
    }

    public void TakeDamage(float amount) => TakeDamage(amount, transform.position);

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

        Vector3 parentPosition = transform.position;
        Quaternion parentRotation = transform.rotation;
        Vector3 parentScale = transform.lossyScale;
        Vector3 parentVelocity = rb != null ? rb.linearVelocity : Vector3.zero;
        parentVelocity.y = 0f;

        Vector3 parentAngularVelocity = rb != null ? rb.angularVelocity : Vector3.zero;

        // --- SMART ARRAY CHECK ---
        // We look at the actual arrays to determine what to spawn, avoiding the tier bug
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
            Debug.LogError($"[ASTEROID ERROR] '{gameObject.name}' vanished because its Tier {nextTier} Fragments array is EMPTY! Please assign prefabs in the Inspector.");
            Destroy(gameObject);
            return;
        }

        GameObject assemblyHolder = new GameObject($"{gameObject.name}_AssemblyHolder");
        assemblyHolder.transform.position = parentPosition;
        assemblyHolder.transform.rotation = parentRotation * Quaternion.Euler(90f, 0f, 0f);
        assemblyHolder.transform.localScale = parentScale;

        List<GameObject> spawnedFragments = new List<GameObject>(matchingPrefabs.Length);

        foreach (GameObject prefab in matchingPrefabs)
        {
            GameObject fragment = Instantiate(prefab, assemblyHolder.transform);
            fragment.SetActive(false);
            spawnedFragments.Add(fragment);

            if (cachedMaterial != null)
            {
                MeshRenderer renderer = fragment.GetComponentInChildren<MeshRenderer>();
                if (renderer != null) renderer.sharedMaterial = cachedMaterial;
            }
        }

        Bounds localBounds = new Bounds();
        bool boundsInit = false;

        foreach (var frag in spawnedFragments)
        {
            Renderer r = frag.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                Bounds b = r.localBounds;
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                        for (int z = -1; z <= 1; z += 2)
                        {
                            Vector3 localCorner = b.center + Vector3.Scale(b.extents, new Vector3(x, y, z));
                            Vector3 worldCorner = r.transform.TransformPoint(localCorner);
                            Vector3 holderLocalCorner = assemblyHolder.transform.InverseTransformPoint(worldCorner);

                            if (!boundsInit) { localBounds = new Bounds(holderLocalCorner, Vector3.zero); boundsInit = true; }
                            else localBounds.Encapsulate(holderLocalCorner);
                        }
            }
        }

        Vector3 localCenter = boundsInit ? localBounds.center : Vector3.zero;

        foreach (var frag in spawnedFragments)
        {
            frag.transform.localPosition -= localCenter;
        }

        Physics.SyncTransforms();

        List<Rigidbody> bodies = new();
        List<Collider> allColliders = new();
        List<Vector3> targetPositions = new();
        List<Quaternion> targetRotations = new();
        List<Vector3> targetVelocities = new();

        foreach (GameObject fragment in spawnedFragments)
        {
            Vector3 finalWorldPos = fragment.transform.position;
            Quaternion finalWorldRot = fragment.transform.rotation;

            targetPositions.Add(finalWorldPos);
            targetRotations.Add(finalWorldRot);

            Vector3 direction = finalWorldPos - impactCenter;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.001f)
                direction = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));

            targetVelocities.Add(parentVelocity + direction.normalized * explosionForce);

            if (fragment.GetComponent<Health>() == null) fragment.AddComponent<Health>();
            if (fragment.GetComponent<Targetable>() == null) fragment.AddComponent<Targetable>();

            Asteroid asteroid = fragment.GetComponent<Asteroid>();
            if (asteroid == null) asteroid = fragment.AddComponent<Asteroid>();

            Rigidbody body = fragment.GetComponent<Rigidbody>();
            if (body == null) body = fragment.AddComponent<Rigidbody>();

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

            fragment.tag = gameObject.tag;
            SetLayerRecursively(fragment, gameObject.layer);

            body.isKinematic = true;
            ConfigureRigidbody(body);

            body.centerOfMass = Vector3.zero;
            body.inertiaTensorRotation = Quaternion.identity;
            body.maxDepenetrationVelocity = 0.5f;

            Collider[] cols = fragment.GetComponentsInChildren<Collider>(true);
            if (cols.Length == 0)
            {
                Collider newCol = fragment.AddComponent<CapsuleCollider>();
                cols = new Collider[] { newCol };
            }

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
            {
                for (int j = i + 1; j < allColliders.Count; j++)
                {
                    if (allColliders[i] != null && allColliders[j] != null)
                        Physics.IgnoreCollision(allColliders[i], allColliders[j], true);
                }
            }
        }

        for (int i = 0; i < bodies.Count; i++)
        {
            bodies[i].isKinematic = false;
            bodies[i].linearVelocity = targetVelocities[i];
            bodies[i].angularVelocity = parentAngularVelocity;
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
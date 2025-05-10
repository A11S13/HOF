using UnityEngine;
using System.Collections.Generic;

public class Rocks : MonoBehaviour
{
    [Header("Prefab")]
    public GameObject rockPrefab;

    [Header("Spawn Settings")]
    [Min(1)] public int rockCount = 20;
    public float spawnRadius = 10f;
    public Vector2 sizeRange = new Vector2(0.5f, 2f);

    [Header("Physics Settings")]
    public float attractionForce = 5f;
    public float orbitSpeed = 3f;
    public ForceMode forceMode = ForceMode.Force;
    public Vector3 orbitAxis = Vector3.up;
    [Range(0f, 1f)] public float velocityRandomness = 0.2f;

    [Header("Rigidbody Properties")]
    public float density = 1f;
    [Range(0f, 1f)] public float angularDrag = 0.05f;
    [Range(0f, 1f)] public float linearDrag = 0f;

    private List<Rigidbody> rocks = new List<Rigidbody>();

    void Start()
    {
    }

    public void GenerateRocks()
    {
        ClearExistingRocks();

        for (int i = 0; i < rockCount; i++)
        {
            Vector3 spawnPosition = transform.position + Random.insideUnitSphere * spawnRadius;
            GameObject newRock = Instantiate(rockPrefab, spawnPosition, Random.rotation);
            newRock.transform.SetParent(transform);

            ConfigureRockSize(newRock);
            Rigidbody rb = ConfigureRigidbody(newRock);
            AddInitialVelocity(rb, spawnPosition);

            rocks.Add(rb);
        }
    }

    void ClearExistingRocks()
    {
        foreach (Rigidbody rb in rocks)
        {
            if (rb != null) Destroy(rb.gameObject);
        }
        rocks.Clear();
    }

    void ConfigureRockSize(GameObject rock)
    {
        float size = Random.Range(sizeRange.x, sizeRange.y);
        rock.transform.localScale = Vector3.one * size;
    }

    Rigidbody ConfigureRigidbody(GameObject rock)
    {
        Rigidbody rb = rock.GetComponent<Rigidbody>();
        if (rb == null) rb = rock.AddComponent<Rigidbody>();

        rb.useGravity = false;
        rb.linearDamping = linearDrag;
        rb.angularDamping = angularDrag;
        rb.mass = CalculateMass(rock.transform.localScale.x);

        return rb;
    }

    float CalculateMass(float size)
    {
        return (4f / 3f) * Mathf.PI * Mathf.Pow(size / 2f, 3) * density;
    }

    void AddInitialVelocity(Rigidbody rb, Vector3 spawnPosition)
    {
        Vector3 directionToCenter = (transform.position - spawnPosition).normalized;
        Vector3 tangent = Vector3.Cross(directionToCenter, orbitAxis).normalized;

        // Add randomness to orbit direction
        tangent = Vector3.Slerp(tangent, Random.onUnitSphere, velocityRandomness);

        rb.linearVelocity = tangent * orbitSpeed * Random.Range(0.8f, 1.2f);
    }

    void FixedUpdate()
    {
        foreach (Rigidbody rb in rocks)
        {
            if (rb != null)
            {
                Vector3 direction = (transform.position - rb.position).normalized;
                rb.AddForce(direction * attractionForce, forceMode);
            }
        }
    }

    // Editor button for regeneration
    [ContextMenu("Regenerate Field")]
    public void RegenerateField()
    {
        GenerateRocks();
    }
}
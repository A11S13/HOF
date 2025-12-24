using UnityEngine;
using System.Collections;

public class Asteroid : MonoBehaviour
{
    public float orbitalSpeed = 10f;
    public float speedVariation = 2f;
    public float rotationSpeed = 5f;

    private Vector3 orbitCenter = Vector3.zero;
    private Vector3 initialPosition;
    private Rigidbody rb;

    private Transform playerTransform;
    private float cleanupRadiusSquared;
    private bool canDespawn = false;

    private AsteroidBelt asteroidBelt;
    private AsteroidSize asteroidSize;


    public void Initialize(AsteroidBelt belt, AsteroidSize size, Transform player, float cleanupRadiusSq)
    {
        asteroidBelt = belt;
        asteroidSize = size;
        playerTransform = player;
        cleanupRadiusSquared = cleanupRadiusSq;
        canDespawn = true;

        StartCoroutine(SelfDespawnCheck());
    }

    void Start()
    {
        initialPosition = transform.position;
        rb = GetComponent<Rigidbody>();

        if (rb == null)
        {
            Debug.LogError("Rigidbody component is required for the Asteroid script to work. Please attach a Rigidbody to the GameObject.");
            rb = gameObject.AddComponent<Rigidbody>();
        }

        rb.useGravity = false;

        float initialAngle = Mathf.Atan2(initialPosition.z, initialPosition.x);
        float radius = Vector3.Distance(initialPosition, orbitCenter);
        Vector3 targetPosition = new Vector3(
            orbitCenter.x + radius * Mathf.Cos(initialAngle),
            transform.position.y,
            orbitCenter.z + radius * Mathf.Sin(initialAngle)
        );

        Vector3 tangentialDirection = Vector3.Cross(Vector3.up, (targetPosition - orbitCenter).normalized);
        float actualSpeed = orbitalSpeed + Random.Range(-speedVariation, speedVariation);
        Vector3 initialVelocity = tangentialDirection * actualSpeed;

        rb.linearVelocity = initialVelocity;
        rb.angularVelocity = Random.insideUnitSphere * rotationSpeed;
    }

    IEnumerator SelfDespawnCheck()
    {
        while (canDespawn)
        {
            int waitFrames = Random.Range(5, 15);
            for (int i = 0; i < waitFrames; i++)
            {
                yield return null;
            }

            if (playerTransform != null && (transform.position - playerTransform.position).sqrMagnitude > cleanupRadiusSquared)
            {
                Destroy(gameObject);
                canDespawn = false;
            }
        }
    }

    void OnDeath()
    {
        Explode();
    }


    private void Explode()
    {
        // Create fragments
        if (asteroidBelt != null)
        {
            asteroidBelt.CreateAsteroidFragments(transform.position, asteroidSize);
        }

        // Add explosion effect here (particles, sound, etc.)

        // Destroy this asteroid
        Destroy(gameObject);
    }




    void FixedUpdate()
    {
        // Orbital motion is handled by initial velocity
    }
}
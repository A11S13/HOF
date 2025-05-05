using UnityEngine;
using System.Collections;

public class Asteroid : MonoBehaviour
{
    public float orbitalSpeed = 10f; // Adjustable speed parameter
    public float speedVariation = 2f;
    private Vector3 orbitCenter = Vector3.zero;
    private Vector3 initialPosition;
    private Rigidbody rb;
    public float rotationSpeed = 5f;

    private Transform playerTransform;
    private float cleanupRadiusSquared;
    private bool canDespawn = false;

    public void InitializeDespawn(Transform player, float cleanupRadiusSq)
    {
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
            // Wait for a random number of frames
            int waitFrames = Random.Range(5, 15); // Adjust range as needed
            for (int i = 0; i < waitFrames; i++)
            {
                yield return null;
            }

            if (playerTransform != null && (transform.position - playerTransform.position).sqrMagnitude > cleanupRadiusSquared)
            {
                Destroy(gameObject);
                canDespawn = false; // Ensure the coroutine stops
            }
        }
    }

    void FixedUpdate()
    {
        // No need to calculate and set velocity in FixedUpdate anymore. The asteroid will continue to move
        // based on its initial velocity and the lack of any forces acting upon it (other than the initial impulse).
    }
}
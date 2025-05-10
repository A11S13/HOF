using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Azoth : MonoBehaviour
{
    [Header("Force Settings")]
    public float pushForce = 5f;
    public float minInterval = 0.5f;
    public float maxInterval = 2f;

    private float _borderAvoidanceDistance = 100f;
    private float _worldHalfSize = 100000f;
    private float _borderRepulsionStrength = 2f;

    private Rigidbody rb;
    private float nextPushTime;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        SetNextWanderTime();
    }

    void Update()
    {
        WanderBehaviour();
    }

    void WanderBehaviour()
    {
        if (Time.time >= nextPushTime)
        {
            AvoidBorders();
            Wander();
            SetNextWanderTime();
        }
    }
    void Wander()
    {
        // Generate random direction in XZ plane
        Vector3 randomDirection = new Vector3(
            Random.Range(-1f, 1f),
            0f,
            Random.Range(-1f, 1f)
        ).normalized;

        // Generate random force between 0 and pushForce
        float randomForceMagnitude = Random.Range(0, pushForce);

        // Apply impulse force
        rb.AddForce(randomDirection * randomForceMagnitude, ForceMode.Impulse);
    }

    private void AvoidBorders()
    {
        Vector3 currentPosition = transform.position;
        Vector3 repulsionForce = Vector3.zero;

        if (currentPosition.x > _worldHalfSize - _borderAvoidanceDistance)
        {
            repulsionForce += Vector3.left * Mathf.Pow(1 - ((_worldHalfSize - currentPosition.x) / _borderAvoidanceDistance), 2);
        }
        if (currentPosition.x < -_worldHalfSize + _borderAvoidanceDistance)
        {
            repulsionForce += Vector3.right * Mathf.Pow(1 - ((currentPosition.x - (-_worldHalfSize)) / _borderAvoidanceDistance), 2);
        }
        if (currentPosition.z > _worldHalfSize - _borderAvoidanceDistance)
        {
            repulsionForce += Vector3.back * Mathf.Pow(1 - ((_worldHalfSize - currentPosition.z) / _borderAvoidanceDistance), 2);
        }
        if (currentPosition.z < -_worldHalfSize + _borderAvoidanceDistance)
        {
            repulsionForce += Vector3.forward * Mathf.Pow(1 - ((currentPosition.z - (-_worldHalfSize)) / _borderAvoidanceDistance), 2);
        }

        rb.linearVelocity += repulsionForce * _borderRepulsionStrength;
    }

    void SetNextWanderTime()
    {
        nextPushTime = Time.time + Random.Range(minInterval, maxInterval);
    }
}
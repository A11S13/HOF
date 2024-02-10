using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Swarm : MonoBehaviour
{
    EnemySpawner _eS;
    public static float crowdingWeight = 1f; // Adjust the weight of crowding avoidance influence
    public static float noiseIntensity = 2f;
    public int rotationCounter = 0;
    public static int maxRotationFrequency = 300;
    public static int minRotationFrequency = 1;
    public int rotationFrequency = 0;
    public static float rotationSmoothness = 1;
    Quaternion targetRotation;
    List<GameObject> friends = new List<GameObject>();

    void Awake()
    {
        _eS = GetComponentInParent<EnemySpawner>();
    }

    void Start()
    {
        rotationFrequency = Random.Range(minRotationFrequency, rotationFrequency);
    }

    public void Adjust()
    {
        GetFriends();
        if (_eS.align)
            AlignWithFriends();
        if (_eS.cohesion)
            CohesionWithBouncing();
        if (_eS.crowd)
            CrowdingAvoidance();
        if (_eS.noise)
            ApplyNoise();
    }

    void AlignWithFriends()
    {
        if (friends.Count == 0)
            return;

        // Calculate the weighted average rotation of closest friends
        Quaternion averageRotation = Quaternion.identity;

        foreach (var friend in friends)
        {
            // Calculate the rotation from the agent to the friend
            Quaternion rotationToFriend =
                Quaternion.Inverse(transform.rotation) * friend.transform.rotation;

            // Slerp to blend the rotations
            averageRotation = Quaternion.Slerp(
                averageRotation,
                rotationToFriend,
                1.0f / friends.Count
            );
        }

        // Smoothly rotate the agent towards the average rotation
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            transform.rotation * averageRotation,
            _eS.wAlign * Time.deltaTime
        );
    }

    void CohesionWithBouncing()
    {
        if (friends.Count == 0)
            return;

        // Calculate the average position of closest friends
        Vector3 averagePosition =
            friends.Aggregate(Vector3.zero, (acc, friend) => acc + friend.transform.position)
            / friends.Count;

        // Calculate the direction to the average position
        Vector3 directionToAverage = averagePosition - transform.position;

        // Calculate the rotation needed to face the average position
        Quaternion targetRotation = Quaternion.LookRotation(directionToAverage, Vector3.up);

        // Smoothly rotate the agent towards the average position
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            _eS.wCohesion * Time.deltaTime
        );
    }

    void CrowdingAvoidance()
    {
        if (friends.Count == 0)
            return;

        Vector3 adjustedForward = transform.forward;

        foreach (var friend in friends)
        {
            // Check the distance to each friend
            float distanceToFriend = Vector3.Distance(
                transform.position,
                friend.transform.position
            );

            if (distanceToFriend < _eS.cRadius)
            {
                // Calculate the avoidance direction
                Vector3 avoidanceDirection = transform.position - friend.transform.position;

                // Normalize the avoidance direction
                avoidanceDirection.Normalize();

                // Adjust the forward vector to avoid the crowded friend
                adjustedForward += avoidanceDirection * _eS.wCrowd;
            }
        }

        // Normalize the adjusted forward vector
        adjustedForward.Normalize();

        // Apply the adjusted forward vector to the agent's rotation
        transform.rotation = Quaternion.LookRotation(adjustedForward, Vector3.up);
    }

    void ApplyNoise()
    {
        // Check if it's time to rotate
        if (rotationCounter >= rotationFrequency)
        {
            // Generate a random noise vector for rotation
            Vector3 rotationNoise = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));

            // Normalize the rotation noise vector to maintain direction
            rotationNoise.Normalize();

            // Calculate the target rotation based on the rotation noise
            targetRotation = Quaternion.LookRotation(rotationNoise);

            // Reset the rotation counter
            rotationCounter = 0;

            // Set a new random rotation frequency
            rotationFrequency = Random.Range(minRotationFrequency, maxRotationFrequency);
        }
        else
        {
            // Increment the rotation counter
            rotationCounter++;
        }
        // Smoothly rotate the agent
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            _eS.wNoise * Time.deltaTime
        );
    }

    void GetFriends()
    {
        friends.Clear(); // Clear the list before updating

        // Angle between each ray
        float angleStep = _eS.arcAngle / (_eS.rayCount - 1);

        for (int i = 0; i < _eS.rayCount; i++)
        {
            // Calculate the angle for the current ray
            float currentAngle = -_eS.arcAngle / 2f + i * angleStep;

            // Calculate the direction of the ray
            Vector3 rayDirection = Quaternion.Euler(0, currentAngle, 0) * transform.forward;

            // Raycast to detect enemies
            RaycastHit hit;
            if (Physics.Raycast(transform.position, rayDirection, out hit, _eS.fRadius))
            {
                // Check if the hit object has the "Enemy" tag
                if (hit.collider.CompareTag("Enemy"))
                {
                    friends.Add(hit.collider.gameObject);
                }
            }
        }
    }
}

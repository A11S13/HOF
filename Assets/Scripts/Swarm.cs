using UnityEngine;

public class Swarm : MonoBehaviour
{
    public float alignWeight = 1f; // Adjust the weight of alignment influence
    public float cohesionWeight = 1f; // Adjust the weight of cohesion influence
    public float crowdingWeight = 1f; // Adjust the weight of crowding avoidance influence
    public float noiseIntensity = 2f;
    public int rotationCounter = 0;
    public int maxRotationFrequency = 300;
    public int minRotationFrequency = 1;
    public int rotationFrequency = 0;
    public float rotationSmoothness = 1;
    Quaternion targetRotation;

    void Start()
    {
        rotationFrequency = Random.Range(minRotationFrequency, rotationFrequency);
    }

    public void Move(float enemySpeed)
    {
        AlignWithFriends();
        CohesionWithBouncing();
        CrowdingAvoidance();
        ApplyNoise(enemySpeed);
    }

    void AlignWithFriends() { }

    void CohesionWithBouncing()
    {
        // Cohesion and bouncing logic
    }

    void CrowdingAvoidance()
    {
        // Crowding avoidance logic
    }

    void ApplyNoise(float enemySpeed)
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
            rotationSmoothness * Time.deltaTime
        );
        // Move forward based on the input speed
        transform.Translate(Vector3.forward * enemySpeed * Time.deltaTime);
    }
}

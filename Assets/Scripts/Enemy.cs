using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    EnemySpawner _eS;
    public bool isTargeted;
    public bool isDestroyed;
    public float health = 100f;
    public static float enemySpeed = 5f;
    public static GameObject explosionPrefab;
    public static GameObject explosionSoundHolderPrefab;
    public static float crowdingWeight = 1f;
    public static float noiseIntensity = 2f;
    public int rotationCounter = 0;
    public static int maxRotationFrequency = 300;
    public static int minRotationFrequency = 1;
    public int rotationFrequency = 0;
    Quaternion targetRotation;
    private GameObject[] friends;

    private static GameObject player;

    void Awake()
    {
        _eS = GetComponentInParent<EnemySpawner>();
    }

    void Update()
    {
        if (_eS.swarm)
            Adjust();
        if (_eS.tPlayer)
            TargetPlayer();
        Move();
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

    void Start()
    {
        _eS = GetComponentInParent<EnemySpawner>();
        player = GameObject.FindGameObjectWithTag("Player");
        explosionPrefab = Resources.Load<GameObject>("Explode");
        explosionSoundHolderPrefab = Resources.Load<GameObject>("ExplosionSoundHolder");
        transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        rotationFrequency = Random.Range(minRotationFrequency, rotationFrequency);
        friends = new GameObject[5];
    }

    void AlignWithFriends()
    {
        if (friends.Length == 0)
            return;

        // Calculate the weighted average rotation of closest friends
        Quaternion averageRotation = Quaternion.identity;

        foreach (var friend in friends)
        {
            if (friend != null)
            {
                // Calculate the rotation from the agent to the friend
                Quaternion rotationToFriend =
                    Quaternion.Inverse(transform.rotation) * friend.transform.rotation;

                // Slerp to blend the rotations
                averageRotation = Quaternion.Slerp(
                    averageRotation,
                    rotationToFriend,
                    1.0f / friends.Length
                );
            }
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
        if (friends.Length == 0)
            return;

        // Calculate the average position of closest friends
        Vector3 averagePosition = Vector3.zero;

        int validFriendsCount = 0;

        foreach (var friend in friends)
        {
            if (friend != null)
            {
                averagePosition += friend.transform.position;
                validFriendsCount++;
            }
        }

        if (validFriendsCount > 0)
        {
            averagePosition /= validFriendsCount;

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
    }

    void CrowdingAvoidance()
    {
        if (friends.Length == 0)
            return;

        Vector3 adjustedForward = transform.forward;

        foreach (var friend in friends)
        {
            if (friend != null)
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
        // Clear the array before updating
        System.Array.Clear(friends, 0, friends.Length);

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
                    // Fill the first available slot in the array
                    for (int j = 0; j < friends.Length; j++)
                    {
                        if (friends[j] == null)
                        {
                            friends[j] = hit.collider.gameObject;
                            break;
                        }
                    }

                    // Sort the array based on distance
                    System.Array.Sort(
                        friends,
                        (a, b) =>
                            (
                                a == null
                                    ? float.MaxValue
                                    : Vector3.Distance(transform.position, a.transform.position)
                            ).CompareTo(
                                b == null
                                    ? float.MaxValue
                                    : Vector3.Distance(transform.position, b.transform.position)
                            )
                    );
                }
            }
        }
    }

    void TargetPlayer()
    {
        // Calculate the direction to the player
        Vector3 directionToPlayer = player.transform.position - transform.position;

        // Calculate the rotation needed to face the player
        Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer, Vector3.up);

        // Smoothly rotate the agent towards the player
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            Time.deltaTime * _eS.wPlayer
        );
    }

    void Move()
    {
        // Move forward based on the input speed
        transform.Translate(Vector3.forward * enemySpeed * Time.deltaTime);
    }

    void OnTriggerEnter(Collider collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            Debug.Log("Player collided with an enemy!");    
            TakeDamage(100);
        }
        else if (collision.gameObject.CompareTag("Projectile"))
        {
            Projectile projectile = collision.gameObject.GetComponent<Projectile>();
            if (projectile != null)
            {
                TakeDamage(projectile.GetDamage());
            }
        }
    }

    public void TakeDamage(float damage)
    {
        health -= damage;
        if (health <= 0)
        {
            GetComponent<Collider>().enabled = false;
            isDestroyed = true;
            Explode();
        }
    }

    void Explode()
    {
        GameObject explosion = Instantiate(
            explosionPrefab,
            transform.position,
            Quaternion.identity
        );

        // Instantiate soundHolder at the player's position
        GameObject soundHolder = Instantiate(
            explosionSoundHolderPrefab,
            player.transform.position,
            Quaternion.identity
        );

        AudioSource soundHolderAudioSource = soundHolder.GetComponent<AudioSource>();

        if (soundHolderAudioSource != null)
        {
            double playTime = AudioSettings.dspTime + 0.1;
            soundHolderAudioSource.PlayScheduled(playTime);
        }

        Destroy(explosion, 3f);
        Destroy(soundHolder, 1f);
        Destroy(gameObject, 0.2f);
    }
}

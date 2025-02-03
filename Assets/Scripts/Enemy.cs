using UnityEngine;

public class Enemy : MonoBehaviour
{
    public bool isTargeted;
    public bool isDestroyed;
    public float health = 100f;
    public static float enemySpeed = 5f; // Global enemy speed.
    public static GameObject explosionPrefab;
    public static GameObject explosionSoundHolderPrefab;

    // Data updated by the job.
    [System.Serializable]
    public struct EnemyData
    {
        public int rotationCounter;
        public int rotationFrequency;
        public Quaternion targetRotation;
    }
    public EnemyData data;

    void Awake()
    {
        // Load explosion assets (ensure you have these in your Resources folder)
        if (explosionPrefab == null)
            explosionPrefab = Resources.Load<GameObject>("Explode");
        if (explosionSoundHolderPrefab == null)
            explosionSoundHolderPrefab = Resources.Load<GameObject>("ExplosionSoundHolder");

        // Initialize noise rotation frequency randomly between 1 and 300.
        data.rotationFrequency = Random.Range(1, 300);
        data.rotationCounter = 0;
        data.targetRotation = transform.rotation;

        // Register with our manager.
        EnemyManager.Instance.RegisterEnemy(this);
    }

    void OnDestroy()
    {
        // Unregister if this enemy is destroyed.
        if (EnemyManager.Instance != null)
            EnemyManager.Instance.UnregisterEnemy(this);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Player collided with an enemy!");
            TakeDamage(100f);
        }
        else if (other.CompareTag("Projectile"))
        {
            // Assume your projectile has a GetDamage() method.
            float damage = other.GetComponent<Projectile>()?.GetDamage() ?? 0;
            TakeDamage(damage);
        }
    }

    public void TakeDamage(float damage)
    {
        health -= damage;
        if (health <= 0)
        {
            isDestroyed = true;
            GetComponent<Collider>().enabled = false;
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
            this.transform.position,
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
        Destroy(gameObject, 0.0f);
    }
}

using UnityEngine;

public class Enemy : MonoBehaviour
{
    public bool isTargeted;
    public bool isDestroyed;
    public float health = 100f;
    public float speed = 5f;
    public GameObject explosionPrefab; 
    public GameObject explosionSoundHolderPrefab;

    private GameObject player;

    void Update()
    {
        MoveTowardsPlayer();
    }

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player"); 
    }

    void MoveTowardsPlayer()
    {
        if (player != null)
        {
            Vector3 direction = (player.transform.position - transform.position).normalized;
            transform.Translate(direction * speed * Time.deltaTime);
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            Explode();
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
        GameObject explosion = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
       
        // Instantiate soundHolder at the player's position
        GameObject soundHolder = Instantiate(explosionSoundHolderPrefab, player.transform.position, Quaternion.identity);

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

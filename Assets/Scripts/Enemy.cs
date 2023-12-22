using UnityEngine;

public class Enemy : MonoBehaviour
{
    public float health = 100f;
    public float speed = 5f;
    public GameObject explosionPrefab; // Assign the explosion particle system prefab in the Editor

    void Update()
    {
        MoveTowardsPlayer();
    }

    void MoveTowardsPlayer()
    {
        // Implement movement logic towards the player
        GameObject player = GameObject.FindGameObjectWithTag("Player");

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
            // Check if the collision is with a projectile
            Projectile projectile = collision.gameObject.GetComponent<Projectile>();

            if (projectile != null)
            {
                // Subtract projectile damage from enemy health
                TakeDamage(projectile.GetDamage());
            }
        }
    }

    public void TakeDamage(float damage)
    {
        // Subtract damage from health
        health -= damage;

        // Check if health is less than or equal to zero
        if (health <= 0)
        {
            Explode();
        }
    }

    void Explode()
{
    // Instantiate explosion particles at the enemy's position
    if (explosionPrefab != null)
    {
        GameObject explosion = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
        
        // Destroy the explosion prefab after a certain duration (adjust the time as needed)
        Destroy(explosion, 3f); // Change 3f to the desired duration
    }

    // Implement other explosion logic (e.g., sound, additional effects)

    Destroy(gameObject); // Destroy the enemy object when it explodes
}
}

using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed = 200f;
    public static float damage = 20f;

    private Vector3 velocity;
    private bool isInitialized = false;

    void Start()
    {
        // Only set default velocity if not already initialized
        if (!isInitialized)
        {
            velocity = transform.forward * speed;
        }
    }

    public void Initialize(Vector3 parentVelocity)
    {
        // Add parent velocity to projectile's base velocity
        velocity = transform.forward * speed + parentVelocity;
        isInitialized = true;
    }

    void FixedUpdate()
    {
        transform.position += velocity * Time.fixedDeltaTime;
    }

    void OnTriggerEnter(Collider other)
    {
        // Check if we hit an asteroid
        Asteroid asteroid = other.GetComponent<Asteroid>();
        if (asteroid != null)
        {
            asteroid.TakeDamage((int)damage);
            Destroy(gameObject);
        }
        else if (other.CompareTag("Enemy"))
        {
            Destroy(gameObject);
        }
    }

    public float GetDamage()
    {
        return damage;
    }
}
using UnityEngine;

public class Projectile : MonoBehaviour
{
    public static float speed = 200f;
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
        if (other.CompareTag("Enemy"))
        {
            Destroy(gameObject);
        }
    }

    public float GetDamage()
    {
        return damage;
    }
}
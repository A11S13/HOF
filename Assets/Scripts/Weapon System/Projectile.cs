using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed = 200f;
    public static float damage = 20f;

    Vector3 velocity;
    bool isInitialized;

    // NEW: Store owner's layer to prevent self-damage
    private int ownerLayer = -1;

    void Start()
    {
        if (!isInitialized)
            velocity = transform.forward * speed;
    }

    public void Initialize(Vector3 parentVelocity)
    {
        velocity = transform.forward * speed + parentVelocity;
        isInitialized = true;
    }

    // NEW: Overload to accept owner layer
    public void Initialize(Vector3 parentVelocity, int ownerLayer)
    {
        velocity = transform.forward * speed + parentVelocity;
        this.ownerLayer = ownerLayer;
        isInitialized = true;
    }

    void FixedUpdate()
    {
        transform.position += velocity * Time.fixedDeltaTime;
    }

    void OnTriggerEnter(Collider other)
    {
        if (ownerLayer != -1 && other.gameObject.layer == ownerLayer)
            return;

        var asteroid = other.GetComponentInParent<Asteroid>();
        if (asteroid != null)
        {
            asteroid.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        var d = other.GetComponent<Damageable>();
        if (d != null)
        {
            d.Damage(damage);
            Destroy(gameObject);
        }
    }

    public float GetDamage()
    {
        return damage;
    }
}

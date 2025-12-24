using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed = 200f;
    public static float damage = 20f;

    Vector3 velocity;
    bool isInitialized;

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

    void FixedUpdate()
    {
        transform.position += velocity * Time.fixedDeltaTime;
    }

    void OnTriggerEnter(Collider other)
    {
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

using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed = 10f;
    public float damage = 20f;
    public float impulseMagnitude = 5f;

    void Start()
    {
        MovementControl movementControl = FindObjectOfType<MovementControl>();
        // Calculate the impulse force based on projectile properties
        Vector3 impulseForce = transform.forward * -impulseMagnitude;
        // Apply the impulse to the ship
        movementControl.ApplyImpulse(impulseForce);

    }

    void FixedUpdate()
    {
        transform.Translate(Vector3.forward * speed * Time.fixedDeltaTime);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            Enemy enemy = other.GetComponent<Enemy>();
            enemy.TakeDamage(damage);
            Destroy(gameObject);
        }
    }

    public float GetDamage()
    {
        return damage;
    }
}

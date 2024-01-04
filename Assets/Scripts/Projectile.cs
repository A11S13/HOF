using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed = 10f;
    public float damage = 20f; // Adjust the damage value as needed
    public float impulseMagnitude = 5f; // Adjust the impulse magnitude as needed

    void Start()
    {
        // Find the ship's MovementControl script
        MovementControl movementControl = FindObjectOfType<MovementControl>();

        // Check if the script is found
        if (movementControl != null)
        {
            // Calculate the impulse force based on projectile properties
            Vector3 impulseForce = transform.forward * -impulseMagnitude;

            // Apply the impulse to the ship
            movementControl.ApplyImpulse(impulseForce);
        }
        else
        {
            Debug.LogError("MovementControl not found. Make sure the ship has the MovementControl script attached.");
        }

        // Your existing Projectile code...
    }

    void FixedUpdate()
    {
        // Move the projectile forward
        transform.Translate(Vector3.forward * speed * Time.fixedDeltaTime);

        // Add any other projectile behavior here
        // ...
    }

    void OnTriggerEnter(Collider other)
    {
        //Debug.Log("Projectile Trigger Enter");

        // Check if the collision is with an enemy
        if (other.CompareTag("Enemy"))
        {
            // Get the Enemy script and apply damage
            Enemy enemy = other.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage);
            }

            // Destroy the projectile upon hitting an enemy
            Destroy(gameObject);
        }
    }

    public float GetDamage()
    {
        return damage;
    }
}

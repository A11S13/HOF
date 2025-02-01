using UnityEngine;

public class Projectile : MonoBehaviour
{
    public static float speed = 100f;
    public static float damage = 20f;
    public static float impulseMagnitude = 0.005f;

    void FixedUpdate()
    {
        transform.Translate(Vector3.forward * speed * Time.fixedDeltaTime);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            Enemy enemy = other.GetComponent<Enemy>();
            Destroy(gameObject);
        }
    }

    public float GetDamage()
    {
        return damage;
    }
}

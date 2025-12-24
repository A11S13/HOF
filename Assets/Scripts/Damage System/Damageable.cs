using UnityEngine;

public class Damageable : MonoBehaviour
{
    Health health;
    Shield shield;

    void Awake()
    {
        health = GetComponent<Health>();
        shield = GetComponent<Shield>();
    }

    public void Damage(float amount)
    {
        if (shield && shield.Absorb(amount))
            return;

        health?.Damage(amount);
    }
}

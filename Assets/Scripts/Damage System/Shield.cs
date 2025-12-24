using UnityEngine;

public class Shield : MonoBehaviour
{
    public float max = 50f;
    public float current;

    void OnEnable()
    {
        current = max;
    }

    public bool Absorb(float damage)
    {
        if (current <= 0) return false;

        current -= damage;
        return true;
    }
}

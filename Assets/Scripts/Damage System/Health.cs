using UnityEngine;

public class Health : MonoBehaviour
{
    public float max = 100f;
    public float current;

    public bool IsAlive => current > 0;

    void OnEnable()
    {
        current = max;
    }

    public void Damage(float amount)
    {
        if (current <= 0) return;

        current -= amount;

        if (current <= 0)
            SendMessage("OnDeath", SendMessageOptions.DontRequireReceiver);
    }
}

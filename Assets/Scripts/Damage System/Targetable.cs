using UnityEngine;

public class Targetable : MonoBehaviour
{
    public TargetType type;

    public bool IsAlive
    {
        get
        {
            var h = GetComponent<Health>();
            return h != null && h.IsAlive;
        }
    }
}

using UnityEngine;

public class Alignment : MonoBehaviour
{
    public bool isFriendly;

    public void Set(bool friendly)
    {
        isFriendly = friendly;
    }

    void OnEnable()
    {
        isFriendly = true; // SAFE DEFAULT for spatial spawns
    }
}

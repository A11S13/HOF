using UnityEngine;

public class Player : MonoBehaviour
{
    private MovementControl playerController;

    private void Awake()
    {
        playerController = GetComponent<MovementControl>();
    }
    public void DisablePlayerControl()
    {
            playerController.enabled = false; 
            Debug.LogWarning("Disabled player control.");
    }

    public void EnablePlayerControl()
    {
            playerController.enabled = true; 
            Debug.LogWarning("Enable player control.");
    }
}
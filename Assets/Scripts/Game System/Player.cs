using UnityEngine;

/// <summary>
/// Player script - manages player-specific functionality
/// </summary>
public class Player : MonoBehaviour
{
    private PlayerShipMovement playerMovement;

    private void Awake()
    {
        playerMovement = GetComponent<PlayerShipMovement>();

        if (playerMovement == null)
        {
            Debug.LogError("PlayerShipMovement component not found on Player GameObject!");
        }
    }

    public void DisablePlayerControl()
    {
        if (playerMovement != null)
        {
            playerMovement.enabled = false;
            Debug.LogWarning("Disabled player control.");
        }
    }

    public void EnablePlayerControl()
    {
        if (playerMovement != null)
        {
            playerMovement.enabled = true;
            Debug.LogWarning("Enabled player control.");
        }
    }
}

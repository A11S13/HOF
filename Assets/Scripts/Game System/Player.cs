using UnityEngine;

/// <summary>
/// Player script - manages player-specific functionality
/// </summary>
public class Player : MonoBehaviour
{
    private PlayerShipMovement playerMovement;
    private Alignment alignment;

    private void Awake()
    {
        playerMovement = GetComponent<PlayerShipMovement>();
        alignment = GetComponent<Alignment>();

        if (playerMovement == null)
        {
            Debug.LogError("PlayerShipMovement component not found on Player GameObject!");
        }
        
        if (alignment == null)
        {
            Debug.LogError("Alignment component not found on Player GameObject!");
        }
    }

    private void Start()
    {
        // We set this in Start because Alignment.OnEnable() sets isFriendly to true.
        // Since Start() runs after OnEnable(), this overrides the default value.
        if (alignment != null)
        {
            alignment.Set(false);
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

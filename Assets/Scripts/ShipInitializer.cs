using UnityEngine;

/// <summary>
/// Initializes ship components and wires dependencies together.
/// Attach this to the root PLAYER or NPC GameObject.
/// </summary>
public class ShipInitializer : MonoBehaviour
{
    [Header("Ship Type")]
    public bool isPlayerShip;

    [Header("References (Auto-found if null)")]
    public ShipCore shipCore;
    public ShipTargetingSystem targetingSystem;
    public Alignment alignment;

    private IShipController movementController;

    void Start() // Changed from Awake to Start
    {
        var baseController = GetComponent<ShipMovementBase>();
        if (baseController != null)
        {
            movementController = baseController as IShipController;
        }
        else
        {
            Debug.LogError($"ShipInitializer on {gameObject.name}: No ShipMovementBase component found!");
            enabled = false;
            return;
        }

        // Validate Rigidbody through controller
        if (movementController.ShipRigidbody == null)
        {
            Debug.LogError($"ShipInitializer on {gameObject.name}: Movement controller has no Rigidbody!");
            enabled = false;
            return;
        }

        // Find components if not assigned
        if (shipCore == null)
        {
            shipCore = GetComponentInChildren<ShipCore>();
        }

        if (targetingSystem == null)
        {
            targetingSystem = GetComponentInChildren<ShipTargetingSystem>();
        }

        if (alignment == null)
        {
            alignment = GetComponentInChildren<Alignment>();
        }

        // Validation
        if (shipCore == null)
        {
            Debug.LogError($"ShipInitializer on {gameObject.name}: ShipCore not found in children!");
            enabled = false;
            return;
        }

        if (targetingSystem == null)
        {
            Debug.LogError($"ShipInitializer on {gameObject.name}: ShipTargetingSystem not found in children!");
            enabled = false;
            return;
        }

        // Initialize ShipCore with dependencies
        shipCore.Initialize(movementController, targetingSystem);

    }
}

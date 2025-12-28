using UnityEngine;

/// <summary>
/// Controls ship movement - can be player input or AI
/// </summary>
public interface IShipController
{
    void UpdateMovement();
    void SetMaxSpeed(float speed);
    Rigidbody ShipRigidbody { get; }
    Transform ShipTransform { get; }
}

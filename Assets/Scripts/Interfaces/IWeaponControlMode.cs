using UnityEngine;

/// <summary>
/// Interface for weapon control modes (AI, Manual, Hybrid)
/// </summary>
public interface IWeaponControlMode
{
    /// <summary>
    /// Update weapon control and determine if weapon should fire
    /// </summary>
    /// <returns>Aim quality factor (0-1), or -1 if should not fire</returns>
    float UpdateControl(Weapon weapon);

    /// <summary>
    /// Get the aim position for this control mode
    /// </summary>
    Vector3 GetAimPosition(Weapon weapon);
}

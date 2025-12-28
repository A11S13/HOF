using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Provides targeting information for weapons and ship systems
/// </summary>
public interface ITargetProvider
{
    /// <summary>
    /// Get all tracked targets sorted by distance
    /// </summary>
    List<Targetable> GetTrackedTargets();

    /// <summary>
    /// Get target data array for weapon prioritization
    /// </summary>
    ShipTargetingSystem.TargetData[] GetTargetDataArray();

    /// <summary>
    /// Refresh/update target list
    /// </summary>
    void RefreshTargets();

    /// <summary>
    /// The position this target provider is scanning from
    /// </summary>
    Vector3 ScanPosition { get; }
}

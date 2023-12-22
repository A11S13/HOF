// ShipData.cs (Scriptable Object)
using UnityEngine;

[CreateAssetMenu(fileName = "New Ship Data", menuName = "ScriptableObjects/ShipData")]
public class ShipData : ScriptableObject
{
    // Overall energy value (set in the editor)
    public float overallEnergy;
}

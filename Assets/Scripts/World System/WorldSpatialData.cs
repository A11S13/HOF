using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class WorldSpatialData
{
    //public bool IsExplored; // If spatial data exists, then it's explored.
    public List<GameObjectData> Objects = new List<GameObjectData>();
}

[System.Serializable]
public struct GameObjectData
{
    public Vector3 Position;
    public Quaternion Rotation;
    public string PrefabID;
}
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class WorldSpatialData
{
    public bool IsExplored; 
    public List<GameObjectData> Objects = new List<GameObjectData>();
}

[System.Serializable]
public struct GameObjectData
{
    public Vector3 Position;
    public Quaternion Rotation;
    public string PrefabID;
    public int Experience;
}
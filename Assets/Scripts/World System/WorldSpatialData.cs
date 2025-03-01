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
    bool IsOutOfBounds;

    public Vector3 Position;
    public Quaternion Rotation;
    public string PrefabID;
    public string ObjectID;

    public bool Orbit;
    public float Impulse;
    public float Period;
}
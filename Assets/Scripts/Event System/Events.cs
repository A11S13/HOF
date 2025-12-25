using UnityEngine;

// GLOBAL EVENTS
public struct GameStartEvent : IGlobalEvent { }
public struct ClusterTransitionEvent : IGlobalEvent { }
public struct AnimaWaveStartedEvent : IGlobalEvent { }
public struct AnimaWaveClearedEvent : IGlobalEvent { }
public struct AggressionDecayedEvent : IGlobalEvent { }
public struct PlayerShotEvent : IGlobalEvent { }




// LOCAL EVENTS
public struct AnimaSpawnedEvent : ILocalEvent
{
    public Vector3 Location { get; }

    public bool IsFriendly { get; }

    public AnimaSpawnedEvent(Vector3 location, bool isFriendly)
    {
        Location = location;
        IsFriendly = isFriendly;
    }
}

public struct QuadrantTransitionEvent : IQuadrantEvent
{
    public Vector2Int? OldLocation { get; }
    public Vector2Int NewLocation { get; }

    public QuadrantTransitionEvent(Vector2Int? oldLoc, Vector2Int newLoc)
    {
        OldLocation = oldLoc;
        NewLocation = newLoc;
    }
}

public struct AnimaKilledEvent : IGlobalEvent
{
    public Vector3 Position;
    public bool WasFriendly;

    public AnimaKilledEvent(Vector3 pos, bool wasFriendly)
    {
        Position = pos;
        WasFriendly = wasFriendly;
    }
}

public struct StartAnimaWaveEvent : ILocalEvent
{
    public Vector3 Location { get; }
    public int Waves { get; }

    public StartAnimaWaveEvent(Vector3 location, int waves)
    {
        Location = location;
        Waves = waves;
    }
}

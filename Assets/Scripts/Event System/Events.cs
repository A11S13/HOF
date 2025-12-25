using UnityEngine;

// GLOBAL EVENTS
public struct GameStartEvent : IGlobalEvent { }
public struct ClusterTransitionEvent : IGlobalEvent { }

// LOCAL EVENTS
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


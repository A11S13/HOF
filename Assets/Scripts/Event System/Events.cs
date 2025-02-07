using UnityEngine;

// GLOBAL EVENTS
public struct GameStartEvent : IGlobalEvent { }
public struct ClusterEnterEvent : IGlobalEvent { }
public struct ClusterExitEvent : IGlobalEvent { }
public struct ResearchedEvent : IGlobalEvent { }
public struct LevelUpEvent : IGlobalEvent { }
public struct WarningEvent : IGlobalEvent { }
public struct ResolvedEvent : IGlobalEvent { }
public struct GameTimeEvent : IGlobalEvent { }
public struct GameOverEvent : IGlobalEvent { }

// LOCAL EVENTS
public struct PlayerMoveEvent : IQuadrantEvent
{
    public Vector2Int? Location { get; }
    public PlayerMoveEvent(Vector2Int? location) => Location = location;
}
public struct AlienDetectedEvent : ILocalEvent
{
    public Vector3 Location { get; }
    public AlienDetectedEvent(Vector3 location) => Location = location;
}
public struct AsteroidApproachEvent : ILocalEvent
{
    public Vector3 Location { get; }
    public AsteroidApproachEvent(Vector3 location) => Location = location;
}
public struct PlayerDeathEvent : ILocalEvent
{
    public Vector3 Location { get; }
    public PlayerDeathEvent(Vector3 location) => Location = location;
}
public struct NpcDeathEvent : ILocalEvent
{
    public Vector3 Location { get; }
    public NpcDeathEvent(Vector3 location) => Location = location;
}
public struct ItemPickupEvent : ILocalEvent
{
    public Vector3 Location { get; }
    public ItemPickupEvent(Vector3 location) => Location = location;
}
public struct ItemDropEvent : ILocalEvent
{
    public Vector3 Location { get; }
    public ItemDropEvent(Vector3 location) => Location = location;
}
public struct ItemCreateEvent : ILocalEvent
{
    public Vector3 Location { get; }
    public ItemCreateEvent(Vector3 location) => Location = location;
}
public struct ItemUpgradeEvent : ILocalEvent
{
    public Vector3 Location { get; }
    public ItemUpgradeEvent(Vector3 location) => Location = location;
}
public struct ItemDegradeEvent : ILocalEvent
{
    public Vector3 Location { get; }
    public ItemDegradeEvent(Vector3 location) => Location = location;
}
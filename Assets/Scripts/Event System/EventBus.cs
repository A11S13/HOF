using System;
using System.Collections.Generic;
using UnityEngine;

public interface IGameEvent { }
public interface IGlobalEvent : IGameEvent { }
public interface IQuadrantEvent : IGameEvent 
{
    Vector2Int? PreviousLocation { get; }
    Vector2Int? CurrentLocation { get; }
}
public interface ILocalEvent : IGameEvent
{
    Vector3 Location { get; }
}

public class EventBus
{
    // Singleton pattern
    private static EventBus _instance;
    public static EventBus Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new EventBus();
            }
            return _instance;
        }
    }
    private EventBus() { } // ConFuckingStructor

    private Dictionary<Type, Delegate> _eventHandlers = new Dictionary<Type, Delegate>();

    public void Publish<T>(T @event) where T : IGameEvent
    {
        if (_eventHandlers.TryGetValue(typeof(T), out var handlers))
        {
            (handlers as Action<T>)?.Invoke(@event);
        }
    }
    public void Subscribe<T>(Action<T> handler) where T : IGameEvent
    {
        var type = typeof(T);
        if (_eventHandlers.TryGetValue(type, out var existing))
        {
            _eventHandlers[type] = Delegate.Combine(existing, handler);
        }
        else
        {
            _eventHandlers[type] = handler;
        }
    }
}
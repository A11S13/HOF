// DON'T MODIFY THIS FILE, NIGGER!
using UnityEngine;

public class WorldStateManager : MonoBehaviour
{
    [Header("Settings")]
    public Vector2 clusterBounds = new Vector2(200000f, 200000f);

    public float quadrantSize = 200f;
    public bool IsInMappedWorld;
    public bool WasInMappedWorld;
    public bool Awakened;
    public Vector2Int? CurrentQuadrant { get; private set; }

    public Transform Player;

    private void Awake()
    {
        Player = FindFirstObjectByType<Player>()?.transform;
        clusterBounds = new Vector2(200000f, 200000f);
        Awakened = true;
        CheckWorldTransition();
        if (IsInMappedWorld) CalculateCurrentQuadrant();
    }

    void Start()
    {
        if (IsInMappedWorld)
        {
            CheckQuadrantTransition();
        }
    }

    void Update()
    {
        CheckWorldTransition();
        CheckQuadrantTransition();
    }

    public bool IsPlayerInClusterBounds()
    {
        // AABB check
        return Mathf.Abs(Player.position.x) < clusterBounds.x / 2 &&
               Mathf.Abs(Player.position.z) < clusterBounds.y / 2;
    }

    private void CheckWorldTransition()
    {
        IsInMappedWorld = IsPlayerInClusterBounds(); // Update the current state

        if (Awakened) // Handle the initial state transition (only once)
        {
            Awakened = false;

            if (IsInMappedWorld)
            {
                EventBus.Instance.Publish(new ClusterTransitionEvent());
                //Debug.Log("WAKING AT CLUSTER");
                WasInMappedWorld = true;
            }
            else
            {
                EventBus.Instance.Publish(new ClusterTransitionEvent());
                Debug.Log("WAKING AT VOID...");
            }
            return; // Important: Exit after handling the initial state
        }

        if (IsInMappedWorld != WasInMappedWorld) // Check for changes in the mapped world state
        {
            if (IsInMappedWorld)
            {
                EventBus.Instance.Publish(new ClusterTransitionEvent());
                WasInMappedWorld = true;
                Debug.Log("ENTERING HINESSA CLUSTER");
            }
            else
            {
                EventBus.Instance.Publish(new ClusterTransitionEvent());
                WasInMappedWorld = false;
                IsInMappedWorld = false;
                CurrentQuadrant = null;
                Debug.Log("LEAVING HINESSA CLUSTER");
            }
        }
    }
    private void CheckQuadrantTransition()
    {
        //Debug.Log("WTF?");
        IsInMappedWorld = IsPlayerInClusterBounds();
        if (!IsInMappedWorld) return;

        Vector2Int newQuadrant = CalculateCurrentQuadrant();
        if (newQuadrant == CurrentQuadrant) return;

        var oldQuadrant = CurrentQuadrant;
        CurrentQuadrant = newQuadrant;
        //Debug.Log("Dispatching QTE...");
        EventBus.Instance.Publish(new QuadrantTransitionEvent(oldQuadrant, newQuadrant));
        //Debug.Log($"Quadrant transition: {oldQuadrant} -> {newQuadrant}");
    }

    public Vector2Int CalculateCurrentQuadrant()
    {

        // Add bounds clamping
        int maxX = Mathf.FloorToInt(clusterBounds.x / quadrantSize);
        int maxY = Mathf.FloorToInt(clusterBounds.y / quadrantSize);

        int x = Mathf.Clamp(
            Mathf.FloorToInt((Player.position.x + clusterBounds.x / 2) / quadrantSize),
            0,
            maxX - 1
        );

        int y = Mathf.Clamp(
            Mathf.FloorToInt((Player.position.z + clusterBounds.y / 2) / quadrantSize),
            0,
            maxY - 1
        );
        //Debug.Log($"Current Quadrant: {x}, {y}");
        return new Vector2Int(x, y);
    }
}
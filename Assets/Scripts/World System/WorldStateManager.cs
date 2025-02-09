// DON'T MODIFY THIS FILE, NIGGER!
using UnityEngine;

public class WorldStateManager : MonoBehaviour
{
    [Header("Settings")]
    public Vector2 clusterBounds = new Vector2(200000f, 200000f);

    public float quadrantSize= 200f;
    public bool IsInMappedWorld { get; private set; }
    public Vector2Int? CurrentQuadrant { get; private set; }

    public Transform _player;

    void Start()
    {
        IsInMappedWorld = IsPlayerInClusterBounds();
        if (IsInMappedWorld)
        {
            CurrentQuadrant = CalculateCurrentQuadrant();
        }
    }

    void Update()
    {
        CheckWorldTransition();
        CheckQuadrantTransition();
    }

    private bool IsPlayerInClusterBounds()
    {
        // AABB check
        return Mathf.Abs(_player.position.x) < clusterBounds.x / 2 &&
               Mathf.Abs(_player.position.z) < clusterBounds.y / 2;
    }

    private void CheckWorldTransition()
    {
        bool wasInMappedWorld = IsInMappedWorld;
        IsInMappedWorld = IsPlayerInClusterBounds();

        if (IsInMappedWorld != wasInMappedWorld) // If someone or both lied
        {
            if (IsInMappedWorld)
            {
                EventBus.Instance.Publish(new ClusterEnterEvent());
                Debug.Log("ENTERING HINESSA CLUSTER");
            }
            else if (wasInMappedWorld)
            {
                CurrentQuadrant = null; // Reset quadrant when leaving cluster
                EventBus.Instance.Publish(new ClusterExitEvent());
                Debug.Log("EXITING HINESSA CLUSTER");
            }
        }
    }

    private void CheckQuadrantTransition()
    {
        if (!IsInMappedWorld) return;

        Vector2Int newQuadrant = CalculateCurrentQuadrant();
        if (newQuadrant == CurrentQuadrant) return;

        var oldQuadrant = CurrentQuadrant;
        CurrentQuadrant = newQuadrant;

        EventBus.Instance.Publish(new QuadrantTransitionEvent(oldQuadrant, newQuadrant));
        Debug.Log($"Quadrant transition: {oldQuadrant} -> {newQuadrant}");
    }

    private Vector2Int CalculateCurrentQuadrant()
    {
        // Add bounds clamping
        int maxX = Mathf.FloorToInt(clusterBounds.x / quadrantSize);
        int maxY = Mathf.FloorToInt(clusterBounds.y / quadrantSize);

        int x = Mathf.Clamp(
            Mathf.FloorToInt((_player.position.x + clusterBounds.x / 2) / quadrantSize),
            0,
            maxX - 1
        );

        int y = Mathf.Clamp(
            Mathf.FloorToInt((_player.position.z + clusterBounds.y / 2) / quadrantSize),
            0,
            maxY - 1
        );

        return new Vector2Int(x, y);
    }
}
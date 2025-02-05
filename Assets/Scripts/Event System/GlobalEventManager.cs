using UnityEngine;
using System.Collections;

public class GlobalEventManager : MonoBehaviour
{
    // Reference your GameEvent asset in the Inspector.
    public GameEvent enterHinessaClusterEvent;

    // For example, call this when the player enters the cluster.
    public void OnPlayerEnteredCluster()
    {
        // Immediately disable hyperdrive, read local quadrant, etc.
        Debug.Log("Player entered Hinessa Cluster");
        enterHinessaClusterEvent.Raise();
    }

    // Example of delayed event raising (could be used for spawning enemy waves)
    public void RaiseEventWithDelay(GameEvent gameEvent, float minDelay, float maxDelay)
    {
        StartCoroutine(RaiseAfterDelay(gameEvent, Random.Range(minDelay, maxDelay)));
    }

    IEnumerator RaiseAfterDelay(GameEvent gameEvent, float delay)
    {
        yield return new WaitForSeconds(delay);
        gameEvent.Raise();
    }
}

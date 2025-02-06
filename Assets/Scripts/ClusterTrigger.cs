using UnityEngine;

public class ClusterTrigger : MonoBehaviour
{
    GWS _GWS;

    // Reference both GameEvent assets.
    public GameEvent enterClusterEvent;
    public GameEvent leaveClusterEvent;

    private float halfSize = 100000f;
    private bool isInCluster;

    void Awake()
    {
        _GWS = Object.FindAnyObjectByType<GWS>();
    }

    void Start()
    {
        halfSize = _GWS.halfMaxDistance;
    }

    void Update()
    {
        // Check if the player is inside the cluster (using x and z coordinates).
        Vector3 pos = transform.position;
        bool currentlyInCluster = Mathf.Abs(pos.x) <= halfSize && Mathf.Abs(pos.z) <= halfSize;

        if (currentlyInCluster && !isInCluster)
        {
            // Player just entered the cluster.
            isInCluster = true;
            enterClusterEvent.Raise();
            Debug.Log("Player entered the cluster");
        }
        else if (!currentlyInCluster && isInCluster)
        {
            // Player just left the cluster.
            isInCluster = false;
            leaveClusterEvent.Raise();
            Debug.Log("Player left the cluster");
        }
    }
}

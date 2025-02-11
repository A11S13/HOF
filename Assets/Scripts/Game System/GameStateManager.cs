using UnityEngine;

public class GameStateManager : MonoBehaviour
{
    WorldStateManager _WSM;

    public Transform Player;
    public bool StartedInVoid;
    public bool StartedInCluster;

    void Awake()
    {
        _WSM = GetComponent<WorldStateManager>();
        Player = _WSM.Player;
    }

    private void Start()
    {

    }
}

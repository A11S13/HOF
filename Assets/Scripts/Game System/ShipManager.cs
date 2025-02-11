using UnityEngine;

public class ShipManager : MonoBehaviour
{
    private GameStateManager _stateManager;
    public bool Captain;
    public bool Adjutant;

    public bool PlayerAsCaptain;
    public bool AdjutantAsCaptain;

    private Transform _player;

    private void Awake()
    {
        _stateManager = FindFirstObjectByType<GameStateManager>();
        _player = GameObject.FindGameObjectWithTag("Player").transform;

        if (_player == null)
        {
            Debug.LogError("GameObject with tag 'Player' not found!");
        }
        else
        {
            if (_stateManager.StartedInVoid)
            {
                Debug.Log("Fucking void...");
            }
            else if (_stateManager.StartedInCluster) 
            {
                Debug.Log("HINESSA!");
            }
        }
    }
    public void Start()
    {
        InitializeTeam();
    }
    private void InitializeTeam()
    {
        if (_player == null) // Check for null _player
        {
            PlayerAsCaptain = false;
            Debug.Log("Player not assigned!");
            return; // Early exit
        }

        PlayerAsCaptain = true;
        Debug.Log("Initializing captain protocol...");
        CaptainProtocol();
    }

    public void CaptainProtocol()
    {
        AssignCaptain();
        Captain = true;
        AdjutantProtocol();
    }
    private void AssignCaptain()
    {
        if (PlayerAsCaptain)
        {
            Debug.Log("Promoted player to captain!"); // More concise
        }
        else
        {
            Debug.Log("Assigning NPC as captain..."); // More concise
        }
    }
    public void AdjutantProtocol()
    {
        Debug.Log("Adjutant assigning...");
        AssignAdjutant();
        Adjutant = true;

        if (PlayerAsCaptain)
        {
            AdjutantAsCaptain = false;
            Obey();
        }
        else
        {
            AdjutantAsCaptain = true;
            Command();
        }
    }
    private void AssignAdjutant()
    {
        // AI or NPC
        Debug.Log("Adjutant Assigned!");
    }
    private void Obey()
    {
        // Implement instructions here
    }

    private void Command()
    {
        // Implement ship control here
    }
}

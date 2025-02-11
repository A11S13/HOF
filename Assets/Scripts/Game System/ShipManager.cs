using UnityEngine;

public class ShipManager : MonoBehaviour
{
    GameStateManager stateManager;
    public bool Captain;
    public bool Adjutant;

    public bool PlayerAsCaptain;
    public bool AdjutantAsCaptain;

    private Transform _player;

    private void Awake()
    {
        _player = GameObject.FindGameObjectWithTag("Player").transform;

        if (_player == null) // Important: Check if the GameObject was found!
        {
            Debug.LogError("GameObject with tag 'Player' not found!");
        }
    }
    public void Start()
    {
        InitializeTeam();
    }
    public void InitializeTeam()
    {
        if (!_player)
        {
            PlayerAsCaptain = false;
            Debug.Log("Player not assigned!");
        }
        else
        {
            PlayerAsCaptain = true;
            Debug.Log("INITIALIZING CAPTAIN PROTOCOL...");
        }
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
            Debug.Log("Promoted Player to Captain!");
        }
        else
        {
            Debug.Log("Assigning NPC as Captain...");
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
        // do instructions
    }
    private void Command()
    {
        // rule the ship
    }
}

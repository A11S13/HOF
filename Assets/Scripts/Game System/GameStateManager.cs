using UnityEngine;

public class GameStateManager : MonoBehaviour
{
    public WorldStateManager _WSM;

    public bool StartedInVoid { get; private set; } 
    public bool StartedInCluster { get; private set; } 

    public bool GameStarted { get; private set; } 
    public bool GamePaused { get; private set; } 

    public bool CutsceneActive { get; private set; }

    private Player playerComponent;
    private Transform playerTransform;
    private GameObject Player;

    private void Awake()
    {
        _WSM = GetComponent<WorldStateManager>();

        playerComponent = FindFirstObjectByType<Player>();

        if (playerComponent == null)
        {
            Debug.LogError("Player component not found in the scene!");
            return;
        }

        if (!playerComponent.enabled) // Check if the component is enabled
        {
            Debug.LogWarning("Player component is initially disabled.");
            // Decide what to do:
            // - Return: If you require the component to be enabled to start
            // - Proceed: If you'll enable it later
        }

        Player = playerComponent.gameObject;
        playerTransform = Player.transform;

        if (_WSM == null)
        {
            Debug.LogError("WorldStateManager component not found!");
            return; // Important: Early exit
        }

        if (_WSM.IsPlayerInClusterBounds())
        {
            StartedInCluster = true;
        }
        else
        {
            StartedInVoid = true;
        }
    }

    private void Start()
    {
        GameStarted = true; // Game starts in Start()
    }

    // Game Time Control
    public void PauseGame()
    {
        if (!GamePaused) // Prevent pausing if already paused
        {
            Time.timeScale = 0f;
            GamePaused = true;
            Debug.Log("Game Paused");
        }
    }

    public void ResumeGame()
    {
        if (GamePaused) // Prevent resuming if already resumed
        {
            Time.timeScale = 1f; // Or your desired timescale
            GamePaused = false;
            Debug.Log("Game Resumed");
        }
    }

    public void SetTimeScale(float timeScale)
    {
        Time.timeScale = timeScale;
        GamePaused = (timeScale == 0f); // Update GamePaused if timeScale is 0
        Debug.Log($"Time scale set to {timeScale}");
    }

    // Cutscene Control
    public void StartCutscene()
    {
        if (!CutsceneActive)
        {
            CutsceneActive = true;
            // Disable player controls here (e.g., using a PlayerController script)
            playerComponent.DisablePlayerControl(); // See example below
            Debug.Log("Cutscene Started");
        }
    }

    public void EndCutscene()
    {
        if (CutsceneActive)
        {
            CutsceneActive = false;
            // Re-enable player controls here
            playerComponent.EnablePlayerControl(); // See example below
            Debug.Log("Cutscene Ended");
        }
    }
}
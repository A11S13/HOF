using UnityEngine;

public class PlayerAggressionTracker : MonoBehaviour
{
    [Header("Aggression Settings")]
    [SerializeField] float aggressionPerKill = 10f;
    [SerializeField] float maxAggression = 100f;
    [SerializeField] float decayRate = 2f; // Points per second
    [SerializeField] float decayDelay = 3f; // Wait before decay starts
    [SerializeField] float shootingActivityWindow = 2f; // Time window to consider player "actively shooting"

    [Header("Debug")]
    [SerializeField] bool showDebugInfo = true;
    [SerializeField] float debugLogInterval = 0.5f; // Log decay every X seconds

    private float currentAggression = 0f;
    private float timeSinceLastKill = 0f;
    private float timeSinceLastShot = 0f;
    private float lastDecayLogTime = 0f;
    private bool isDecaying = false;
    private bool hasActiveWave = false;

    public float CurrentAggression => currentAggression;
    public float MaxAggression => maxAggression;
    public float AggressionPercent => maxAggression > 0 ? (currentAggression / maxAggression) * 100f : 0f;
    public bool IsAggressive => currentAggression > 0f;
    public bool IsAtMaxAggression => currentAggression >= maxAggression;
    public bool IsActivelyShooting => timeSinceLastShot < shootingActivityWindow;

    void OnEnable()
    {
        EventBus.Instance.Subscribe<AnimaKilledEvent>(OnAnimaKilled);
        EventBus.Instance.Subscribe<AnimaWaveStartedEvent>(OnWaveStarted);
        EventBus.Instance.Subscribe<AnimaWaveClearedEvent>(OnWaveCleared);
        EventBus.Instance.Subscribe<PlayerShotEvent>(OnPlayerShot);
    }

    void Update()
    {
        if (currentAggression <= 0f)
            return;

        // Track time since last shot
        timeSinceLastShot += Time.deltaTime;

        // Only decay after wave is cleared
        if (hasActiveWave)
            return;

        // Stop decay if player is actively shooting
        if (IsActivelyShooting)
        {
            timeSinceLastKill = 0f; // Reset decay delay timer
            if (isDecaying && showDebugInfo)
            {
                Debug.Log("Decay interrupted - player is shooting");
            }
            isDecaying = false;
            return;
        }

        timeSinceLastKill += Time.deltaTime;

        // Start decay after delay
        if (timeSinceLastKill >= decayDelay && !isDecaying)
        {
            isDecaying = true;
            if (showDebugInfo)
                Debug.Log($"Aggression decay started. Current: {currentAggression:F1}/{maxAggression} ({AggressionPercent:F0}%)");
        }

        if (isDecaying)
        {
            currentAggression -= decayRate * Time.deltaTime;

            // Continuous decay logging
            if (showDebugInfo && Time.time - lastDecayLogTime >= debugLogInterval)
            {
                Debug.Log($"Aggression decaying: {currentAggression:F1}/{maxAggression} ({AggressionPercent:F0}%) - Rate: {decayRate}/s");
                lastDecayLogTime = Time.time;
            }

            if (currentAggression <= 0f)
            {
                currentAggression = 0f;
                isDecaying = false;
                EventBus.Instance.Publish(new AggressionDecayedEvent());

                if (showDebugInfo)
                    Debug.Log("Aggression fully decayed - returning to calm");
            }
        }
    }

    private void OnPlayerShot(PlayerShotEvent e)
    {
        timeSinceLastShot = 0f;

        if (showDebugInfo && isDecaying)
        {
            Debug.Log($"Player shot - decay timer reset. Aggression: {currentAggression:F1}/{maxAggression} ({AggressionPercent:F0}%)");
        }
    }

    private void OnAnimaKilled(AnimaKilledEvent e)
    {
        // Only track friendly/neutral kills
        if (!e.WasFriendly)
            return;

        currentAggression += aggressionPerKill;

        // Simple clamp to max
        if (currentAggression > maxAggression)
        {
            currentAggression = maxAggression;
        }

        if (showDebugInfo)
        {
            Debug.Log($"Neutral killed! Aggression: {currentAggression:F1}/{maxAggression} ({AggressionPercent:F0}%) [+{aggressionPerKill}]");
        }

        timeSinceLastKill = 0f;
        isDecaying = false;
    }

    private void OnWaveStarted(AnimaWaveStartedEvent evt)
    {
        hasActiveWave = true;
        isDecaying = false;

        if (showDebugInfo)
            Debug.Log($"Wave started - decay paused. Current aggression: {currentAggression:F1}/{maxAggression} ({AggressionPercent:F0}%)");
    }

    private void OnWaveCleared(AnimaWaveClearedEvent evt)
    {
        hasActiveWave = false;
        timeSinceLastKill = 0f;
        isDecaying = false;

        if (showDebugInfo)
            Debug.Log($"Wave cleared - decay will start in {decayDelay}s. Aggression: {currentAggression:F1}/{maxAggression} ({AggressionPercent:F0}%)");
    }

    public void ResetAggression()
    {
        currentAggression = 0f;
        timeSinceLastKill = 0f;
        timeSinceLastShot = 0f;
        isDecaying = false;
        hasActiveWave = false;
        EventBus.Instance.Publish(new AggressionDecayedEvent());
    }

    // Add aggression manually (useful for scripted events)
    public void AddAggression(float amount)
    {
        if (amount <= 0f) return;

        currentAggression = Mathf.Min(currentAggression + amount, maxAggression);
        timeSinceLastKill = 0f;
        isDecaying = false;

        if (showDebugInfo)
            Debug.Log($"Aggression added: +{amount} -> {currentAggression:F1}/{maxAggression} ({AggressionPercent:F0}%)");
    }

    // Set aggression directly (useful for testing)
    public void SetAggression(float value)
    {
        currentAggression = Mathf.Clamp(value, 0f, maxAggression);

        if (showDebugInfo)
            Debug.Log($"Aggression set to: {currentAggression:F1}/{maxAggression} ({AggressionPercent:F0}%)");
    }
}

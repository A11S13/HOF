using UnityEngine;
using System.Collections;
using System.Text;
using UnityEngine.Networking;

public class ObserverBridge : MonoBehaviour
{
    [Header("Daemon Connection Settings")]
    public string daemonUrl = "http://localhost:8090";
    public float telemetryIntervalSeconds = 0.2f;
    public float directivePollIntervalSeconds = 0.1f;

    [Header("Bridge References")]
    private WorldStateManager _stateManager;
    private WorldProcessor _worldProcessor;
    private WorldDataManager _dataManager;
    private Transform _playerTransform;

    [System.Serializable]
    public struct ObserverStatePayload
    {
        public string observer_name;
        public string player_id;
        public Vector3 player_position;
        public int current_quadrant_x;
        public int current_quadrant_y;
        public int active_quadrant_count;
        public float timestamp;
    }

    [System.Serializable]
    public struct ObjectDestroyedPayload
    {
        public string event_type;
        public string object_id;
        public string prefab_id;
        public Vector3 world_pos;
        public long timestamp;
    }

    [System.Serializable]
    public struct DirectiveResponse
    {
        public string directive_type;
        public string core_id;
        public string ship_class;
        public int wave_count;
        public float morph_weight;
        public float morph_l;
        public float morph_h;
        public float freq_l;
        public float freq_h;
        public bool enable_osc;
        public float phase;
        public string movement_mode;
        public float chaos_weight;
        public float entropy_depth;
        public float lissajous_speed;
        public float perlin_scale;
        public bool morph_motion_sync;
        public float target_x;
        public float target_z;
    }

    /// <summary>
    /// Automatic Runtime Attachment:
    /// Auto-binds to the 'OBSERVER' GameObject at runtime whenever the scene loads,
    /// eliminating the need to manually drag the script onto the GameObject in Unity Editor.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttachToObserver()
    {
        GameObject observerObj = GameObject.Find("OBSERVER");
        if (observerObj != null)
        {
            if (observerObj.GetComponent<ObserverBridge>() == null)
            {
                observerObj.AddComponent<ObserverBridge>();
                Debug.Log("[OBSERVER BRIDGE] Successfully auto-attached ObserverBridge component to OBSERVER GameObject!");
            }
        }
        else
        {
            Debug.LogWarning("[OBSERVER BRIDGE] OBSERVER GameObject not found in current scene.");
        }
    }

    private void Awake()
    {
        _stateManager = GetComponent<WorldStateManager>();
        _worldProcessor = GetComponent<WorldProcessor>();
        _dataManager = GetComponent<WorldDataManager>();
    }

    private void Start()
    {
        if (_stateManager != null && _stateManager.Player != null)
        {
            _playerTransform = _stateManager.Player;
        }
        else
        {
            _playerTransform = FindFirstObjectByType<Player>()?.transform;
        }

        EventBus.Instance.Subscribe<AnimaKilledEvent>(OnAnimaKilled);
        EventBus.Instance.Subscribe<QuadrantTransitionEvent>(OnQuadrantTransition);

        StartCoroutine(TelemetryPushLoop());
        StartCoroutine(DirectivePollLoop());
    }

    private void OnAnimaKilled(AnimaKilledEvent evt)
    {
        Vector3 pos = evt.Position;
        if (pos == Vector3.zero && _playerTransform != null)
        {
            pos = _playerTransform.position;
        }

        int xHash = Mathf.Abs(pos.x.ToString("F2").GetHashCode()) % 0xFFFF;
        int zHash = Mathf.Abs(pos.z.ToString("F2").GetHashCode()) % 0xFFFF;

        string objName = !string.IsNullOrEmpty(evt.ObjectName) ? evt.ObjectName : $"Anima_{xHash:X4}{zHash:X4}";

        ObjectDestroyedPayload payload = new ObjectDestroyedPayload
        {
            event_type = "OBJECT_DESTROYED",
            object_id = objName,
            prefab_id = "Anima",
            world_pos = pos,
            timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        StartCoroutine(PostJsonData($"{daemonUrl}/telemetry/object_destroyed", JsonUtility.ToJson(payload)));
    }

    private void OnQuadrantTransition(QuadrantTransitionEvent evt)
    {
        SendObserverState();
    }

    private IEnumerator TelemetryPushLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(telemetryIntervalSeconds);
            SendObserverState();
        }
    }

    private void SendObserverState()
    {
        if (_playerTransform == null)
        {
            _playerTransform = FindFirstObjectByType<Player>()?.transform;
            if (_playerTransform == null) return;
        }

        Vector2Int currentQ = _stateManager != null && _stateManager.CurrentQuadrant.HasValue 
            ? _stateManager.CurrentQuadrant.Value 
            : Vector2Int.zero;

        ObserverStatePayload state = new ObserverStatePayload
        {
            observer_name = gameObject.name,
            player_id = "P1_PILOT_001",
            player_position = _playerTransform.position,
            current_quadrant_x = currentQ.x,
            current_quadrant_y = currentQ.y,
            active_quadrant_count = _worldProcessor != null ? _worldProcessor._currentActiveQuadrants.Count : 0,
            timestamp = Time.time
        };

        StartCoroutine(PostJsonData($"{daemonUrl}/telemetry/observer_state", JsonUtility.ToJson(state)));
    }

    private IEnumerator DirectivePollLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(directivePollIntervalSeconds);
            StartCoroutine(GetDirectives());
        }
    }

    private IEnumerator GetDirectives()
    {
        bool hasMore = true;
        while (hasMore)
        {
            using (UnityWebRequest req = UnityWebRequest.Get($"{daemonUrl}/observer/directives"))
            {
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    string json = req.downloadHandler.text;
                    DirectiveResponse response = JsonUtility.FromJson<DirectiveResponse>(json);
                    if (response.directive_type != "NONE")
                    {
                        HandleIncomingDirective(response);
                    }
                    else
                    {
                        hasMore = false;
                    }
                }
                else
                {
                    hasMore = false;
                }
            }
        }
    }

    private void HandleIncomingDirective(DirectiveResponse directive)
    {
        if (directive.directive_type == "SPAWN_SENTINEL")
        {
            Debug.Log($"[OBSERVER BRIDGE] Deploying MORGAVE Autonomous Sentinel Core: {directive.core_id} ({directive.ship_class})");
            if (_playerTransform != null && _dataManager != null)
            {
                Vector3 spawnPos = _playerTransform.position + _playerTransform.forward * 15f;
                _dataManager.SpawnAnimaAt(spawnPos, true);
            }
        }
        else if (directive.directive_type == "TRIGGER_WAVE")
        {
            Debug.Log($"[OBSERVER BRIDGE] EIGVS Sector Threat Override: Spawning {directive.wave_count} Anima Waves");
            if (_playerTransform != null)
            {
                EventBus.Instance.Publish(new StartAnimaWaveEvent(_playerTransform.position, directive.wave_count));
            }
        }
        else if (directive.directive_type == "SPAWN_OBSERVER")
        {
            Debug.Log("[OBSERVER BRIDGE] Spawning Standalone Morphable Observer Agent in Scene Hierarchy...");
            GameObject existingAgent = GameObject.Find("OBSERVER_AGENT");
            if (existingAgent == null)
            {
                GameObject observerAgent = new GameObject("OBSERVER_AGENT");
                if (_playerTransform != null)
                {
                    observerAgent.transform.position = _playerTransform.position + _playerTransform.forward * 10f;
                }
                
                ObserverController controller = observerAgent.AddComponent<ObserverController>();
                
                // Instantiate Anima visual morph body under OBSERVER_AGENT
                GameObject animaPrefab = Resources.Load<GameObject>("Prefabs/AAA/Anima");
                if (animaPrefab != null)
                {
                    GameObject visualBody = Instantiate(animaPrefab, observerAgent.transform);
                    visualBody.name = "ObserverBody";
                    visualBody.transform.localPosition = Vector3.zero;
                    visualBody.transform.localRotation = Quaternion.identity;

                    // Clean up gameplay actors & colliders
                    Anima animaScript = visualBody.GetComponent<Anima>();
                    if (animaScript != null) Destroy(animaScript);
                    Collider col = visualBody.GetComponent<Collider>();
                    if (col != null) Destroy(col);
                }

                Debug.Log("[OBSERVER BRIDGE] OBSERVER_AGENT successfully spawned in Scene Hierarchy!");
            }
        }
        else if (directive.directive_type == "MORPH_OBSERVER")
        {
            Debug.Log($"[OBSERVER BRIDGE] Morphing Observer Phenotype Body: weight = {directive.morph_weight}, L = {directive.morph_l}, H = {directive.morph_h}, Osc = {directive.enable_osc}");
            
            // 1. Search for Observer_agent or Observer GameObject in Scene
            GameObject agent = GameObject.Find("Observer_agent") ?? GameObject.Find("OBSERVER_AGENT") ?? GameObject.Find("Observer");
            if (agent == null) agent = gameObject; // Fallback to current OBSERVER object

            ObserverController controller = agent.GetComponent<ObserverController>();
            if (controller == null)
            {
                controller = agent.AddComponent<ObserverController>();
                Debug.Log($"[OBSERVER BRIDGE] Dynamic Attachment: Attached ObserverController script to {agent.name}");
            }

            controller.ConfigureOscillators(
                directive.morph_l, 
                directive.morph_h, 
                directive.freq_l, 
                directive.freq_h, 
                directive.enable_osc, 
                directive.phase
            );

            if (!string.IsNullOrEmpty(directive.movement_mode) || directive.chaos_weight > 0f)
            {
                controller.ConfigureBionicMovement(
                    directive.movement_mode,
                    directive.chaos_weight,
                    directive.entropy_depth,
                    directive.lissajous_speed,
                    directive.perlin_scale,
                    directive.morph_motion_sync
                );
            }
        }
        else if (directive.directive_type == "NAVIGATE_OBSERVER")
        {
            Vector3 targetPos = new Vector3(directive.target_x, 0f, directive.target_z);
            Debug.Log($"[OBSERVER BRIDGE] Navigating Observer to Locus: ({directive.target_x}, {directive.target_z})");
            
            GameObject agent = GameObject.Find("Observer_agent") ?? GameObject.Find("OBSERVER_AGENT") ?? GameObject.Find("Observer");
            ObserverController controller = agent != null ? agent.GetComponent<ObserverController>() : GetComponent<ObserverController>();
            
            if (controller != null)
            {
                controller.NavigateTo(targetPos);
            }
            else if (agent != null)
            {
                // Fallback direct movement if controller component not manually attached
                agent.transform.position = targetPos;
                Debug.Log($"[OBSERVER BRIDGE] Teleported {agent.name} directly to {targetPos}");
            }
        }
    }

    private IEnumerator PostJsonData(string url, string jsonBody)
    {
        using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();
        }
    }
}

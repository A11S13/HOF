using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

// 1. Define the Ship Classes
public enum ShipClass
{
    Drone,
    Fighter,
    Corvette,
    Frigate,
    Destroyer,
    Cruiser,
    Battlecruiser,
    Mothership,
    Dreadnought,
    Carrier,
    Battleship
}

public enum FireSequenceMode
{
    Independent,
    Ripple_Auto,
    Ripple_Fixed
}

public enum BurstTimeGateMode
{
    Off,
    GlobalTimeGate
}

public class ShipCore : MonoBehaviour
{
    [Header("Hull Classification")]
    [Tooltip("Changing this in Inspector will instantly update Rigidbody Mass on the parent.")]
    public ShipClass hullClass = ShipClass.Frigate;

    [Header("Identity")]
    [Tooltip("Leave empty to generate a random name.")]
    public string shipName;

    [Header("Ship Configuration")]
    public string unitTech;
    public ShipData shipData;

    [Header("Weapon Sequencing")]
    public FireSequenceMode fireMode = FireSequenceMode.Ripple_Auto;
    public float fixedRippleDelay = 0.1f;

    // Internal timing
    private float _calculatedRippleDelay;
    private float _lastGlobalFireTime;

    [Header("Burst Time Gate (Global)")]
    public BurstTimeGateMode burstGateMode = BurstTimeGateMode.Off;
    [Min(0f)] public float burstOnTime = 1.2f;
    [Min(0f)] public float burstCooldownTime = 0.8f;
    [Range(0f, 1f)] public float burstTimeVariation = 0.2f;
    public bool startInCooldown = false;

    private bool _burstGateIsOn = true;
    private float _burstGateNextSwitchTime = 0f;

    [Header("Targeting")]
    public float firingThresholdAngle = 5f;
    public float distanceThreshold = 1f;
    public float angularThreshold = 1f;
    public float angularMarging = 10f;

    [Header("Energy Distribution UI")]
    public Slider shieldsSlider;
    public Slider enginesSlider;
    public Slider weaponsSlider;
    public Slider commandSlider;

    private IShipController shipController;
    private ITargetProvider targetProvider;
    public Weapon[] Weapons { get; private set; }
    private float enginesDistributedEnergy;
    private float weaponsDistributedEnergy;
    private bool isInitialized = false;

    // --- STATIC NAME DATABASE CACHE ---
    private static Dictionary<string, List<string>> _cachedNameDatabase;

    private struct HullStats
    {
        public float mass;
        public float turnSpeed;
        public HullStats(float m, float t) { mass = m; turnSpeed = t; }
    }

    // ---------------------------------------------------------
    // EDITOR VALIDATION
    // ---------------------------------------------------------
    // This runs whenever you change values in the Inspector!
    private void OnValidate()
    {
        ApplyMassOnlyToParent();
    }

    // ---------------------------------------------------------
    // INITIALIZATION
    // ---------------------------------------------------------
    public void Initialize(IShipController controller, ITargetProvider targetProvider)
    {
        if (isInitialized) return;

        shipController = controller;
        this.targetProvider = targetProvider;

        // --- IDENTITY ---
        AssignShipName();

        // --- PHYSICS ---
        // 1. Update Mass (Hierarchy friendly)
        ApplyMassOnlyToParent();

        // 2. Update Controller Settings (Turn Speed)
        if (shipController is ShipMovementBase movementBase)
        {
            ApplyHullPhysicsToController(movementBase);
        }

        // --- WEAPONS ---
        Weapons = GetComponentsInChildren<Weapon>();
        if (Weapons.Length > 0)
        {
            bool isPlayerShip = controller.GetType() == typeof(PlayerShipMovement);
            foreach (var weapon in Weapons)
            {
                if (weapon != null)
                {
                    bool useAI = !isPlayerShip;
                    weapon.Initialize(this, controller, targetProvider, useAI);
                }
            }
        }

        SetupEnergyDistribution();
        _lastGlobalFireTime = Time.time;
        RecalculateRippleTiming();
        InitializeBurstGate();

        isInitialized = true;
    }

    // ---------------------------------------------------------
    // PHYSICS LOGIC
    // ---------------------------------------------------------
    private void ApplyMassOnlyToParent()
    {
        // Finds Rigidbody on this object or any parent
        Rigidbody targetRB = GetComponentInParent<Rigidbody>();
        if (targetRB != null)
        {
            HullStats stats = GetStatsForClass(hullClass);
            targetRB.mass = stats.mass;
        }
    }

    private void ApplyHullPhysicsToController(ShipMovementBase movement)
    {
        HullStats stats = GetStatsForClass(hullClass);
        // We do NOT set RB mass here anymore to avoid conflicts; OnValidate/Initialize handles mass.
        movement.rotationSpeed = stats.turnSpeed;
    }

    private HullStats GetStatsForClass(ShipClass sClass)
    {
        // Adjusted values for clear distinction
        switch (sClass)
        {
            case ShipClass.Drone: return new HullStats(0.1f, 4.0f);
            case ShipClass.Fighter: return new HullStats(0.2f, 3.0f);
            case ShipClass.Corvette: return new HullStats(0.3f, 2.5f);
            case ShipClass.Frigate: return new HullStats(0.5f, 2.0f);
            case ShipClass.Destroyer: return new HullStats(8.0f, 0.7f);
            case ShipClass.Cruiser: return new HullStats(10.0f, 0.6f);

            // --- HEAVY CLASS DISTINCTION ---
            case ShipClass.Battlecruiser: return new HullStats(15f, 0.5f);  // Fast Capital
            case ShipClass.Mothership: return new HullStats(30.0f, 0.4f);
            case ShipClass.Dreadnought: return new HullStats(40.0f, 0.3f);
            case ShipClass.Carrier: return new HullStats(60.0f, 0.2f);
            case ShipClass.Battleship: return new HullStats(82.5f, 0.1f); // Heavy Tank
            // -------------------------------

            default: return new HullStats(0.5f, 2.0f);
        }
    }

    // ---------------------------------------------------------
    // NAME GENERATION LOGIC
    // ---------------------------------------------------------
    private void AssignShipName()
    {
        string classPrefix = hullClass.ToString().ToUpper();
        string finalName = "";

        if (!string.IsNullOrEmpty(shipName))
        {
            finalName = shipName;
        }
        else
        {
            LoadNamesIfNeeded();
            finalName = $"Unit-{Random.Range(1000, 9999)}"; // Fallback

            if (_cachedNameDatabase != null && _cachedNameDatabase.ContainsKey(classPrefix))
            {
                List<string> names = _cachedNameDatabase[classPrefix];
                if (names.Count > 0)
                {
                    finalName = names[Random.Range(0, names.Count)];
                }
            }
            shipName = finalName;
        }

        // Renames THIS GameObject (ShipCore holder)
        gameObject.name = $"{classPrefix} {finalName}";
    }

    private static void LoadNamesIfNeeded()
    {
        if (_cachedNameDatabase != null) return;
        _cachedNameDatabase = new Dictionary<string, List<string>>();

        TextAsset textFile = Resources.Load<TextAsset>("ship_names");
        if (textFile == null) return;

        string[] lines = textFile.text.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        string currentCategory = "";

        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
            {
                currentCategory = trimmed.Substring(1, trimmed.Length - 2).ToUpper();
                if (!_cachedNameDatabase.ContainsKey(currentCategory))
                    _cachedNameDatabase[currentCategory] = new List<string>();
            }
            else if (!string.IsNullOrEmpty(currentCategory))
            {
                _cachedNameDatabase[currentCategory].Add(trimmed);
            }
        }
    }

    // ---------------------------------------------------------
    // HELPERS (Burst Gate, Energy, etc)
    // ---------------------------------------------------------

    private float GetRandomizedDuration(float baseSeconds)
    {
        baseSeconds = Mathf.Max(0f, baseSeconds);
        float v = Mathf.Clamp01(burstTimeVariation);
        if (v <= 0f || baseSeconds <= 0f) return baseSeconds;
        return Random.Range(baseSeconds * (1f - v), baseSeconds * (1f + v));
    }

    private float GetNextGateDurationSeconds(bool gateIsOn)
    {
        return gateIsOn
            ? ((burstOnTime <= 0f) ? 0f : GetRandomizedDuration(burstOnTime))
            : ((burstCooldownTime <= 0f) ? 0f : GetRandomizedDuration(burstCooldownTime));
    }

    private void InitializeBurstGate()
    {
        if (burstGateMode == BurstTimeGateMode.GlobalTimeGate)
        {
            _burstGateIsOn = !startInCooldown;
            _burstGateNextSwitchTime = Time.time + GetNextGateDurationSeconds(_burstGateIsOn);
        }
        else
        {
            _burstGateIsOn = true;
            _burstGateNextSwitchTime = 0f;
        }
    }

    private void TickBurstGate()
    {
        if (burstGateMode != BurstTimeGateMode.GlobalTimeGate) return;
        float now = Time.time;
        if (now >= _burstGateNextSwitchTime)
        {
            _burstGateIsOn = !_burstGateIsOn;
            _burstGateNextSwitchTime = now + GetNextGateDurationSeconds(_burstGateIsOn);
        }
    }

    public void RecalculateRippleTiming()
    {
        if (Weapons == null || Weapons.Length == 0) return;
        if (fireMode == FireSequenceMode.Ripple_Auto)
        {
            float totalFireRateSum = 0f;
            foreach (var w in Weapons) totalFireRateSum += (w.fireRate > 0 ? w.fireRate : w.initialFireRate);
            if (totalFireRateSum > 0) _calculatedRippleDelay = 1f / (totalFireRateSum * 1.05f);
            else _calculatedRippleDelay = 0.1f;
        }
    }

    public bool RequestFirePermission()
    {
        TickBurstGate();
        if (burstGateMode == BurstTimeGateMode.GlobalTimeGate && !_burstGateIsOn) return false;
        if (fireMode == FireSequenceMode.Independent) return true;

        float delay = (fireMode == FireSequenceMode.Ripple_Auto) ? _calculatedRippleDelay : fixedRippleDelay;
        float frameThreshold = Time.smoothDeltaTime;

        if (delay < frameThreshold)
        {
            if (Time.time >= _lastGlobalFireTime + delay)
            {
                _lastGlobalFireTime += delay;
                if (_lastGlobalFireTime < Time.time - frameThreshold * 2f) _lastGlobalFireTime = Time.time;
                return true;
            }
        }
        else
        {
            if (Time.time >= _lastGlobalFireTime + delay)
            {
                _lastGlobalFireTime = Time.time;
                return true;
            }
        }
        return false;
    }

    private void UpdateWeaponsPower(float distributedEnergy)
    {
        foreach (var weapon in Weapons) if (weapon != null) weapon.UpdateFirePower(distributedEnergy);
        RecalculateRippleTiming();
    }

    private void SetupEnergyDistribution()
    {
        if (shipData == null) return;
        ApplyInitialEnergyDistribution();
        if (enginesSlider != null) enginesSlider.onValueChanged.AddListener(OnEnginesSliderChanged);
        if (weaponsSlider != null) weaponsSlider.onValueChanged.AddListener(OnWeaponsSliderChanged);
    }

    private void ApplyInitialEnergyDistribution()
    {
        float enginesValue = enginesSlider != null ? enginesSlider.value : 0f;
        float weaponsValue = weaponsSlider != null ? weaponsSlider.value : 0f;

        if (enginesSlider != null)
        {
            enginesDistributedEnergy = shipData.overallEnergy * enginesValue;
            shipController?.SetMaxSpeed(enginesDistributedEnergy);
        }

        if (weaponsSlider != null)
        {
            weaponsDistributedEnergy = shipData.overallEnergy * weaponsValue;
            UpdateWeaponsPower(weaponsDistributedEnergy);
        }
    }

    void FixedUpdate() { if (isInitialized) targetProvider?.RefreshTargets(); }

    public void OnEnginesSliderChanged(float value)
    {
        if (!Application.isPlaying || shipData == null) return;
        enginesDistributedEnergy = shipData.overallEnergy * value;
        shipController?.SetMaxSpeed(enginesDistributedEnergy);
    }

    public void OnWeaponsSliderChanged(float value)
    {
        if (!Application.isPlaying || shipData == null) return;
        weaponsDistributedEnergy = shipData.overallEnergy * value;
        UpdateWeaponsPower(weaponsDistributedEnergy);
    }

    void OnDestroy()
    {
        if (enginesSlider != null) enginesSlider.onValueChanged.RemoveListener(OnEnginesSliderChanged);
        if (weaponsSlider != null) weaponsSlider.onValueChanged.RemoveListener(OnWeaponsSliderChanged);
    }
}

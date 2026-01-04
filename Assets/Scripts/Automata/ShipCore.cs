using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public enum FireSequenceMode
{
    Independent,    // Chaos
    Ripple_Auto,    // Core calculates timing
    Ripple_Fixed    // Manual fixed delay
}

public enum BurstTimeGateMode
{
    Off,
    GlobalTimeGate  // Fire allowed for burstOnTime, then blocked for burstCooldownTime
}

public class ShipCore : MonoBehaviour
{
    [Header("Ship Configuration")]
    public string unitTech;
    public ShipData shipData;

    [Header("Weapon Sequencing")]
    public FireSequenceMode fireMode = FireSequenceMode.Ripple_Auto;

    [Tooltip("Used only if Mode is Ripple_Fixed.")]
    public float fixedRippleDelay = 0.1f;

    // Internal timing
    private float _calculatedRippleDelay;
    private float _lastGlobalFireTime;

    // ==========================================================
    // TIME-GATED BURST (GLOBAL) + RANDOM VARIATION
    // ==========================================================
    [Header("Burst Time Gate (Global)")]
    public BurstTimeGateMode burstGateMode = BurstTimeGateMode.Off;

    [Tooltip("How long the ship is allowed to fire (seconds).")]
    [Min(0f)]
    public float burstOnTime = 1.2f;

    [Tooltip("How long the ship is NOT allowed to fire after burst ends (seconds).")]
    [Min(0f)]
    public float burstCooldownTime = 0.8f;

    [Tooltip("Random +/- variation applied per cycle. 0.2 = 20% shorter to 20% longer.")]
    [Range(0f, 1f)]
    public float burstTimeVariation = 0.2f;

    [Tooltip("If true, the ship starts in cooldown (blocked) instead of a firing window.")]
    public bool startInCooldown = false;

    // Gate state
    private bool _burstGateIsOn = true;
    private float _burstGateNextSwitchTime = 0f;

    // [Existing Headers...]
    [Header("Targeting")]
    public float firingThresholdAngle = 5f;
    public float distanceThreshold = 1f;
    public float angularThreshold = 1f;
    public float angularMarging = 10f;

    [Header("Weapon AI Settings")]
    public int maxWeaponsPerTarget => Weapons != null ? Weapons.Length : 0;

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

    public void Initialize(IShipController controller, ITargetProvider targetProvider)
    {
        if (isInitialized) return;

        shipController = controller;
        this.targetProvider = targetProvider;
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

        // Initialize timer to NOW so we can fire immediately
        _lastGlobalFireTime = Time.time;
        RecalculateRippleTiming();

        // Initialize burst time gate
        InitializeBurstGate();

        isInitialized = true;
    }

    private float GetRandomizedDuration(float baseSeconds)
    {
        baseSeconds = Mathf.Max(0f, baseSeconds);

        float v = Mathf.Clamp01(burstTimeVariation); // 0.2 = 20%
        if (v <= 0f || baseSeconds <= 0f) return baseSeconds;

        float min = baseSeconds * (1f - v);
        float max = baseSeconds * (1f + v);
        return Random.Range(min, max);
    }

    private float GetNextGateDurationSeconds(bool gateIsOn)
    {
        // Define zero-time behavior clearly:
        // - burstOnTime <= 0 => never allow firing (always cooldown)
        // - burstCooldownTime <= 0 => always allow firing (always on)
        if (gateIsOn)
        {
            if (burstOnTime <= 0f) return 0f;
            return GetRandomizedDuration(burstOnTime);
        }
        else
        {
            if (burstCooldownTime <= 0f) return 0f;
            return GetRandomizedDuration(burstCooldownTime);
        }
    }

    private void InitializeBurstGate()
    {
        if (burstGateMode == BurstTimeGateMode.GlobalTimeGate)
        {
            _burstGateIsOn = !startInCooldown;

            float now = Time.time;
            float dur = GetNextGateDurationSeconds(_burstGateIsOn);

            // If dur is 0, switch immediately on next TickBurstGate call.
            _burstGateNextSwitchTime = now + dur;
        }
        else
        {
            _burstGateIsOn = true;
            _burstGateNextSwitchTime = 0f;
        }
    }

    /// <summary>
    /// Optional: Call this if you change times/variation at runtime and want the gate to restart cleanly.
    /// </summary>
    public void ResetBurstGate(bool startOn = true)
    {
        _burstGateIsOn = startOn;
        _burstGateNextSwitchTime = Time.time + GetNextGateDurationSeconds(_burstGateIsOn);
    }

    private void TickBurstGate()
    {
        if (burstGateMode != BurstTimeGateMode.GlobalTimeGate)
            return;

        float now = Time.time;

        // Enforce the "zero-time" behaviors:
        if (_burstGateIsOn && burstOnTime <= 0f)
        {
            _burstGateIsOn = false;
            _burstGateNextSwitchTime = now + GetNextGateDurationSeconds(_burstGateIsOn);
            return;
        }

        if (!_burstGateIsOn && burstCooldownTime <= 0f)
        {
            _burstGateIsOn = true;
            _burstGateNextSwitchTime = now + GetNextGateDurationSeconds(_burstGateIsOn);
            return;
        }

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
            foreach (var w in Weapons)
            {
                float rate = w.fireRate > 0 ? w.fireRate : w.initialFireRate;
                totalFireRateSum += rate;
            }

            if (totalFireRateSum > 0)
                _calculatedRippleDelay = 1f / (totalFireRateSum * 1.05f);
            else
                _calculatedRippleDelay = 0.1f;
        }
    }

    /// <summary>
    /// Hybrid Permission System:
    /// - Time-gated burst (global): burstOnTime allows, burstCooldownTime blocks (with random variation)
    /// - Ripple sequencing: same as before (Independent / Ripple_Auto / Ripple_Fixed)
    /// </summary>
    public bool RequestFirePermission()
    {
        // 0) Time-gated burst window (GLOBAL)
        TickBurstGate();
        if (burstGateMode == BurstTimeGateMode.GlobalTimeGate && !_burstGateIsOn)
            return false;

        // Note:
        // This keeps your original behavior: Independent ignores ripple sequencing.
        // Burst gate still applies because we check it above.
        if (fireMode == FireSequenceMode.Independent)
            return true;

        float delay = (fireMode == FireSequenceMode.Ripple_Auto)
            ? _calculatedRippleDelay
            : fixedRippleDelay;

        float frameThreshold = Time.smoothDeltaTime;

        // === MODE A: HIGH SPEED GATLING ===
        if (delay < frameThreshold)
        {
            if (Time.time >= _lastGlobalFireTime + delay)
            {
                _lastGlobalFireTime += delay;

                if (_lastGlobalFireTime < Time.time - frameThreshold * 2f)
                    _lastGlobalFireTime = Time.time;

                return true;
            }
        }
        // === MODE B: SLOW "THUD" ===
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

    // [Rest unchanged]
    private void UpdateWeaponsPower(float distributedEnergy)
    {
        foreach (var weapon in Weapons)
        {
            if (weapon != null)
                weapon.UpdateFirePower(distributedEnergy);
        }
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

    void FixedUpdate()
    {
        if (!isInitialized) return;
        targetProvider?.RefreshTargets();
    }

    public void SetWeaponsID()
    {
        if (Weapons == null || Weapons.Length == 0) return;
        int middle = Weapons.Length / 2;
        for (int i = 0; i < Weapons.Length; i++)
        {
            int id = Weapons.Length % 2 == 1 ? i - middle : i - middle + 1;
            Weapons[i].ID = id;
        }
    }

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

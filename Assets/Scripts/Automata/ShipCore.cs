using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public enum FireSequenceMode
{
    Independent,    // Chaos
    Ripple_Auto,    // Core calculates timing
    Ripple_Fixed    // Manual fixed delay
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

        isInitialized = true;
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
            {
                // CRITICAL FIX 1:
                // We use a 1.05f buffer (tighter than 1.1f) to ensure we clear the queue just fast enough.
                _calculatedRippleDelay = 1f / (totalFireRateSum * 1.05f);
            }
            else
            {
                _calculatedRippleDelay = 0.1f;
            }

            // CRITICAL FIX 2: 
            // REMOVED "Mathf.Max(..., Time.fixedDeltaTime)". 
            // That line was artificially capping your fire rate to 50fps.
            // We MUST allow delays smaller than a frame (e.g. 0.005s) for Gatling mode.
        }
    }

    /// <summary>
    /// Hybrid Permission System:
    /// - Slow Rate: Resets timer to 'Now' (Feels heavy/punchy, your preference)
    /// - Fast Rate: Increments timer (Allows multiple shots per frame, prevents skipping)
    /// </summary>
    public bool RequestFirePermission()
    {
        if (fireMode == FireSequenceMode.Independent)
            return true;

        float delay = (fireMode == FireSequenceMode.Ripple_Auto)
            ? _calculatedRippleDelay
            : fixedRippleDelay;

        // Dynamic Threshold:
        // If the requested delay is smaller than the current frame time (e.g. 0.016s),
        // we are in "High Speed / Gatling" territory.
        // We use SmoothDeltaTime to avoid jitter during lag spikes.
        float frameThreshold = Time.smoothDeltaTime;

        // === MODE A: HIGH SPEED GATLING ===
        if (delay < frameThreshold)
        {
            // We use "Accumulator Logic" here. 
            // This allows the timer to advance multiple times inside a single Update frame.
            if (Time.time >= _lastGlobalFireTime + delay)
            {
                _lastGlobalFireTime += delay;

                // Safety: If lag caused us to fall waaaay behind, snap to present
                // so we don't dump 50 shots in one frame.
                if (_lastGlobalFireTime < Time.time - frameThreshold * 2f)
                {
                    _lastGlobalFireTime = Time.time;
                }
                return true;
            }
        }
        // === MODE B: SLOW "THUD" (Your Favorite) ===
        else
        {
            // We use "Reset Logic" here.
            // This snaps the rhythm to the frame grid, creating that distinct "Gamey" feel.
            if (Time.time >= _lastGlobalFireTime + delay)
            {
                _lastGlobalFireTime = Time.time;
                return true;
            }
        }

        return false;
    }

    // [Rest of your class code is fine...]
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

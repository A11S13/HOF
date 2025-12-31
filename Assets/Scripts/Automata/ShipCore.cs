using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Core ship systems - manages weapons, energy, and targeting.
/// Now decoupled and reusable for both player and NPC ships.
/// </summary>
public class ShipCore : MonoBehaviour
{
    [Header("Ship Configuration")]
    public string unitTech;
    public ShipData shipData;

    [Header("Targeting")]
    public float firingThresholdAngle = 5f;
    public float distanceThreshold = 1f;
    public float angularThreshold = 1f;
    public float angularMarging = 10f;

    [Header("Weapon AI Settings")]
    public int maxWeaponsPerTarget => Weapons != null ? Weapons.Length : 0;

    [Header("Energy Distribution UI (Player Only - Leave null for NPCs)")]
    public Slider shieldsSlider;
    public Slider enginesSlider;
    public Slider weaponsSlider;
    public Slider commandSlider;

    // Dependencies (injected)
    private IShipController shipController;
    private ITargetProvider targetProvider;

    // Weapon array
    public Weapon[] Weapons { get; private set; }

    // Energy distribution
    private float enginesDistributedEnergy;
    private float weaponsDistributedEnergy;

    // Initialization flag
    private bool isInitialized = false;

    /// <summary>
    /// Initialize the ship core with dependencies.
    /// Call this after setting up the ship hierarchy.
    /// </summary>
    public void Initialize(IShipController controller, ITargetProvider targetProvider)
    {
        if (isInitialized)
        {
            Debug.LogWarning($"ShipCore on {gameObject.name} already initialized!");
            return;
        }

        shipController = controller;
        this.targetProvider = targetProvider;

        // Find all weapons
        Weapons = GetComponentsInChildren<Weapon>();

        if (Weapons.Length == 0)
        {
            Debug.LogWarning($"ShipCore on {gameObject.name}: No weapons found in children!");
        }
        else
        {
            // Determine if this is a player ship
            bool isPlayerShip = controller.GetType() == typeof(PlayerShipMovement);

            // Initialize each weapon
            foreach (var weapon in Weapons)
            {
                if (weapon != null)
                {
                    // NPCs always use AI, Players default to Manual (can toggle)
                    bool useAI = !isPlayerShip;
                    weapon.Initialize(this, controller, targetProvider, useAI);
                }
            }

            Debug.Log($"ShipCore initialized on {gameObject.name} with {Weapons.Length} weapons");
        }

        // Setup UI listeners and apply initial energy distribution from slider values
        SetupEnergyDistribution();

        isInitialized = true;
    }

    /// <summary>
    /// Setup UI listeners and apply initial energy distribution from slider starting values
    /// </summary>
    private void SetupEnergyDistribution()
    {
        if (shipData == null)
        {
            Debug.LogWarning($"ShipCore on {gameObject.name}: shipData is null, cannot setup energy distribution");
            return;
        }

        // Apply initial energy from current slider values BEFORE adding listeners
        ApplyInitialEnergyDistribution();

        // Setup UI listeners if sliders exist (Player ship)
        if (enginesSlider != null)
        {
            enginesSlider.onValueChanged.AddListener(OnEnginesSliderChanged);
        }

        if (weaponsSlider != null)
        {
            weaponsSlider.onValueChanged.AddListener(OnWeaponsSliderChanged);
        }
    }

    /// <summary>
    /// Apply initial energy distribution from slider starting values
    /// </summary>
    private void ApplyInitialEnergyDistribution()
    {
        // Read initial values from sliders (use their current value set in Inspector/Scene)
        float enginesValue = enginesSlider != null ? enginesSlider.value : 0f;
        float weaponsValue = weaponsSlider != null ? weaponsSlider.value : 0f;

        // Apply engines energy
        if (enginesSlider != null)
        {
            enginesDistributedEnergy = shipData.overallEnergy * enginesValue;
            shipController?.SetMaxSpeed(enginesDistributedEnergy);
            Debug.Log($"{gameObject.name} - Initial Engines Energy: {enginesDistributedEnergy:F2} (slider value: {enginesValue:F2})");
        }

        // Apply weapons energy
        if (weaponsSlider != null)
        {
            weaponsDistributedEnergy = shipData.overallEnergy * weaponsValue;
            UpdateWeaponsPower(weaponsDistributedEnergy);
            Debug.Log($"{gameObject.name} - Initial Weapons Energy: {weaponsDistributedEnergy:F2} (slider value: {weaponsValue:F2})");
        }
    }

    void FixedUpdate()
    {
        if (!isInitialized) return;

        // Update targeting system
        targetProvider?.RefreshTargets();
    }

    public void SetWeaponsID()
    {
        if (Weapons == null || Weapons.Length == 0) return;

        int middle = Weapons.Length / 2;

        for (int i = 0; i < Weapons.Length; i++)
        {
            int id = Weapons.Length % 2 == 1
                ? i - middle
                : i - middle + 1;

            Weapons[i].ID = id;
        }
    }

    private void UpdateWeaponsPower(float distributedEnergy)
    {
        foreach (var weapon in Weapons)
        {
            if (weapon != null)
                weapon.UpdateFirePower(distributedEnergy);
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
        // Cleanup UI listeners
        if (enginesSlider != null)
            enginesSlider.onValueChanged.RemoveListener(OnEnginesSliderChanged);
        if (weaponsSlider != null)
            weaponsSlider.onValueChanged.RemoveListener(OnWeaponsSliderChanged);
    }
}

// ShipCore.cs
using UnityEngine;
using UnityEngine.UI;

public class ShipCore : MonoBehaviour
{
    // Reference to ShipData Scriptable Object
    public ShipData shipData;

    // MovementControl instance
    private MovementControl movementControl;

    // Reference to Weapon scripts
    public Weapon[] Weapons;
    public float distanceThreshold = 1; 
    public float angularThreshold = 1;

    // References to sliders (make sure to assign them in the Unity Editor)
    public Slider shieldsSlider;
    public Slider enginesSlider;
    public Slider weaponsSlider;
    public Slider commandSlider;

    float enginesDistributedEnergy;
    float weaponsDistributedEnergy;

    void Awake() { }

    void Start()
    {
        Weapons = FindObjectsOfType<Weapon>();
        // Assuming the MovementControl component is attached to the same GameObject as ShipCore
        movementControl = GetComponent<MovementControl>();

        // Calculate initial distributed energy based on the overall energy from ShipData and the initial value of engines slider
        float initialEnginesValue = enginesSlider.value;
        enginesDistributedEnergy = shipData.overallEnergy * initialEnginesValue;

        // Calculate initial distributed energy for weapons based on ShipData and the initial value of weapons slider
        float initialWeaponsValue = weaponsSlider.value;
        weaponsDistributedEnergy = shipData.overallEnergy * initialWeaponsValue;

        // Update acceleration in MovementControl with the initial value
        movementControl.UpdateMaxSpeed(enginesDistributedEnergy);

        // Find all Weapon scripts in the scene and store references dynamically



        // Notify Weapons about initial energy distribution
        foreach (var Weapon in Weapons)
        {
            Weapon.UpdateFirePower(weaponsDistributedEnergy);
        }
    }

    // Method to handle engines slider change
    public void OnEnginesSliderChanged(float enginesValue)
    {
        if (movementControl != null)
        {
            // Calculate distributed energy based on the overall energy from ShipData and the engines value
            enginesDistributedEnergy = shipData.overallEnergy * enginesValue;

            // Update acceleration in MovementControl
            movementControl.UpdateMaxSpeed(enginesDistributedEnergy);
        }
    }

    // Method to handle shields slider change
    public void OnShieldsSliderChanged(float shieldsValue)
    {
        // Your implementation for handling shields slider change
    }

    // Method to handle weapons slider change
    public void OnWeaponsSliderChanged(float weaponsValue)
    {
        weaponsDistributedEnergy = shipData.overallEnergy * weaponsValue;
        foreach (var Weapon in Weapons)
        {
            Weapon.UpdateFirePower(weaponsDistributedEnergy);
        }
    }

    // Method to handle command slider change
    public void OnCommandSliderChanged(float commandValue)
    {
        // Your implementation for handling command slider change
    }
}

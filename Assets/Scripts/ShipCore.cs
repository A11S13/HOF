// ShipCore.cs
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class ShipCore : MonoBehaviour
{
    // Reference to ShipData Scriptable Object
    public ShipData shipData;
    Sonar _sonar;
    public float sonarDetectionRadius = 100f;

    public Weapon[] Weapons;
    public float firingThresholdAngle = 5f;
    public float distanceThreshold = 1;
    public float angularThreshold = 1;
    public float angularMarging = 10; //to not intercept on rotation

    public Slider shieldsSlider;
    public Slider enginesSlider;
    public Slider weaponsSlider;
    public Slider commandSlider;
    float enginesDistributedEnergy;
    float weaponsDistributedEnergy;

    private MovementControl movementControl;
    private TargetData[][] AllTargetsArrays;
    public TargetData[] targetDataArray;

    void Awake()
    {
        _sonar = GameObject.Find("Sonar").GetComponent<Sonar>();
    }
    void Update()
    {
        // Cleaning ATA
        //AllTargetsArrays = new TargetData[][] { };

    }
    void FixedUpdate()
    {
        ActualizeTargets();
        AllTargetsArrays = new TargetData[][] { };
    }

    void Start()
    {
        ActualizeTargets();
        AllTargetsArrays = new TargetData[][] { }; // Initialize Array of input Targets Arrays
        Weapons = FindObjectsOfType<Weapon>(); // Creates Weapons Array

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

        // Notify Weapons about initial energy distribution
        foreach (var Weapon in Weapons)
        {
            Weapon.UpdateFirePower(weaponsDistributedEnergy);
        }
    }

    public void SetWeaponsID()
    {
        int middleIndex = Weapons.Length / 2;

        for (int i = 0; i < Weapons.Length; i++)
        {
            int id;

            if (Weapons.Length % 2 == 1) // Odd number of weapons
            {
                id = i - middleIndex;
            }
            else // Even number of weapons
            {
                id = i - middleIndex + 1;
            }

            Weapons[i].ID = id;
        }
    }

    public void ActualizeTargets()
    {
        _sonar.DriveSonar();
        FormClosestEnemiesArray();
    }

    public void FormClosestEnemiesArray()
    {
        int length = Mathf.Min(_sonar.trackedColliders.Count, Weapons.Length);

        targetDataArray = new TargetData[length];

        for (int i = 0; i < length; i++)
        {
            float unifiedDistance = Vector3.Distance(
                transform.position,
                _sonar.trackedColliders[i].transform.position
            );
            targetDataArray[i] = new TargetData(
                _sonar.trackedColliders[i].gameObject,
                unifiedDistance
            );
        }
    }

    public TargetData[] ArrangeTargets(TargetData[] passedCombinedTargets) // DELETES DUBLICATES
    {
        //Debug.Log("RECEIVING TARGET DATA");

        if (AllTargetsArrays.Length == 0) // 1) ATA IS EMPTY
        {
            passedCombinedTargets = new TargetData[] { passedCombinedTargets[0] };
            AllTargetsArrays = AllTargetsArrays
                .Concat(new TargetData[][] { passedCombinedTargets })
                .ToArray();

            return passedCombinedTargets;
        }
        else // 2) TARGET MATCH
        {
            while ( // REMOVE MATCHING TARGETS
                passedCombinedTargets.Length != 0
                && AllTargetsArrays.Any(
                    array => passedCombinedTargets[0].TargetObject == array[0].TargetObject
                )
            )
            {
                //Debug.Log("SKIPPING...");
                passedCombinedTargets = passedCombinedTargets.Skip(1).ToArray();
            }
            if (passedCombinedTargets.Length != 0)
            {
                passedCombinedTargets = new TargetData[] { passedCombinedTargets[0] };
                AllTargetsArrays = AllTargetsArrays
                    .Concat(new TargetData[][] { passedCombinedTargets })
                    .ToArray();

                return passedCombinedTargets;
            }
            return passedCombinedTargets;
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

    public struct TargetData
    {
        public GameObject TargetObject { get; set; }
        public float UnifiedDistance { get; } // Unified distance for both linear and angular distances

        // Add a property for the GameObject
        public GameObject GameObject
        {
            get { return TargetObject; }
            set { TargetObject = value; }
        }

        public TargetData(GameObject targetObject, float unifiedDistance)
        {
            TargetObject = targetObject;
            UnifiedDistance = unifiedDistance;
        }
    }
}

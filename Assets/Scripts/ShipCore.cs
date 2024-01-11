// ShipCore.cs
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class ShipCore : MonoBehaviour
{
    // Reference to ShipData Scriptable Object
    public ShipData shipData;
    public Weapon[] Weapons;
    public Collider[] enemyColliders;
    public float firingThresholdAngle = 5f;
    public float distanceThreshold = 1;
    public float angularThreshold = 1;
    public float angularMarging = 10; //to not intercept on rotation
    public float detectionRadius = 100f;

    public Slider shieldsSlider;
    public Slider enginesSlider;
    public Slider weaponsSlider;
    public Slider commandSlider;
    float enginesDistributedEnergy;
    float weaponsDistributedEnergy;

    private MovementControl movementControl;
    private TargetData[][] AllTargetsArrays;
    public TargetData[] targetDataArray;

    void Awake() { }

    void EarlyUpdate() { }

    void Update()
    {
        FormClosestEnemiesArray();
        // Assign the sorted array to AllTargetsArrays
        AllTargetsArrays = new TargetData[][] { };
    }

    void LateUpdate() { }

    void FixedUpdate()
    {
        AllTargetsArrays = new TargetData[][] { };
    }

    void Start()
    {
        FormClosestEnemiesArray();
        AllTargetsArrays = new TargetData[][] { }; // Initialize it as an empty array
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

    public void FormClosestEnemiesArray()
    {
        enemyColliders = Physics.OverlapSphere(
            transform.position,
            detectionRadius,
            LayerMask.GetMask("Enemy")
        );

        targetDataArray = new TargetData[enemyColliders.Length];

        for (int i = 0; i < enemyColliders.Length; i++)
        {
            float unifiedDistance = Vector3.Distance(
                transform.position,
                enemyColliders[i].transform.position
            );
            targetDataArray[i] = new TargetData(enemyColliders[i].gameObject, unifiedDistance);
        }

        // Sort the array of TargetData based on UnifiedDistance
        System.Array.Sort(
            targetDataArray,
            (a, b) => a.UnifiedDistance.CompareTo(b.UnifiedDistance)
        );

        Debug.Log("Colliders: " + enemyColliders.Length);
        Debug.Log("Targets: " + targetDataArray.Length);
    }

    public TargetData[] ArrangeTargets(TargetData[] passedCombinedTargets) // DELETES DUBLICATES
    {
        Debug.Log("RECEIVING TARGET DATA");

        if (AllTargetsArrays.Length == 0) // 1) ATA IS EMPTY
        {
            passedCombinedTargets = new TargetData[] { passedCombinedTargets[0] };
            AllTargetsArrays = AllTargetsArrays
                .Concat(new TargetData[][] { passedCombinedTargets })
                .ToArray();

            passedCombinedTargets[0]
                .TargetObject.transform.Find("IndicatorRed")
                .gameObject.SetActive(true);

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
                Debug.Log("SKIPPING...");
                passedCombinedTargets = passedCombinedTargets.Skip(1).ToArray();
            }
            if (passedCombinedTargets.Length != 0)
            {
                passedCombinedTargets = new TargetData[] { passedCombinedTargets[0] };
                AllTargetsArrays = AllTargetsArrays
                    .Concat(new TargetData[][] { passedCombinedTargets })
                    .ToArray();
                passedCombinedTargets[0]
                    .TargetObject.transform.Find("IndicatorRed")
                    .gameObject.SetActive(true);
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

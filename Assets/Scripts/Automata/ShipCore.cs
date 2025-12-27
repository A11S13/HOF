using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class ShipCore : MonoBehaviour
{
    public string unitTech;
    public ShipData shipData;

    Sonar _sonar;

    public float sonarRadius = 100f;
    public TargetingConfig targetingConfig;

    public Weapon[] Weapons;

    public float firingThresholdAngle = 5f;
    public float distanceThreshold = 1;
    public float angularThreshold = 1;
    public float angularMarging = 10;

    [Header("Weapon AI Settings")]
    public int maxWeaponsPerTarget => Weapons.Length;

    public Slider shieldsSlider;
    public Slider enginesSlider;
    public Slider weaponsSlider;
    public Slider commandSlider;

    float enginesDistributedEnergy;
    float weaponsDistributedEnergy;

    MovementControl movementControl;

    TargetData[][] AllTargetsArrays;
    public TargetData[] targetDataArray;

    void Awake()
    {
        _sonar = GameObject.Find("Sonar").GetComponent<Sonar>();
        Weapons = Object.FindObjectsByType<Weapon>(FindObjectsSortMode.None);
    }

    void Start()
    {
        ActualizeTargets();
        AllTargetsArrays = new TargetData[][] { };

        movementControl = GetComponentInParent<MovementControl>();

        enginesDistributedEnergy = shipData.overallEnergy * enginesSlider.value;
        weaponsDistributedEnergy = shipData.overallEnergy * weaponsSlider.value;

        movementControl.UpdateMaxSpeed(enginesDistributedEnergy);

        foreach (var w in Weapons)
        {
            if (w != null)
                w.UpdateFirePower(weaponsDistributedEnergy);
        }
    }

    void FixedUpdate()
    {
        ActualizeTargets();
        AllTargetsArrays = new TargetData[][] { };
    }

    public void SetWeaponsID()
    {
        int middle = Weapons.Length / 2;

        for (int i = 0; i < Weapons.Length; i++)
        {
            int id = Weapons.Length % 2 == 1
                ? i - middle
                : i - middle + 1;

            Weapons[i].ID = id;
        }
    }

    public void ActualizeTargets()
    {
        _sonar.DriveSonar(50);
        FormClosestTargetsArray();
    }

    void FormClosestTargetsArray()
    {
        var targets = _sonar.trackedTargets;

        targetDataArray = new TargetData[targets.Count];

        for (int i = 0; i < targets.Count; i++)
        {
            var t = targets[i];
            float d = (t.transform.position - transform.position).sqrMagnitude;
            targetDataArray[i] = new TargetData(t.gameObject, d);
        }
    }

    public TargetData[] ArrangeTargets(TargetData[] passedCombinedTargets)
    {
        if (AllTargetsArrays.Length == 0)
        {
            passedCombinedTargets = new[] { passedCombinedTargets[0] };
            AllTargetsArrays = AllTargetsArrays.Concat(new[] { passedCombinedTargets }).ToArray();
            return passedCombinedTargets;
        }

        while (
            passedCombinedTargets.Length != 0 &&
            AllTargetsArrays.Any(a => passedCombinedTargets[0].TargetObject == a[0].TargetObject)
        )
        {
            passedCombinedTargets = passedCombinedTargets.Skip(1).ToArray();
        }

        if (passedCombinedTargets.Length != 0)
        {
            passedCombinedTargets = new[] { passedCombinedTargets[0] };
            AllTargetsArrays = AllTargetsArrays.Concat(new[] { passedCombinedTargets }).ToArray();
        }

        return passedCombinedTargets;
    }

    public void OnEnginesSliderChanged(float v)
    {
        if (!Application.isPlaying || !this) return;
        enginesDistributedEnergy = shipData.overallEnergy * v;
        movementControl.UpdateMaxSpeed(enginesDistributedEnergy);
    }

    public void OnWeaponsSliderChanged(float v)
    {
        weaponsDistributedEnergy = shipData.overallEnergy * v;

        foreach (var w in Weapons)
        {
            if (w != null)
            {
                float oldRate = w.fireRate;
                w.UpdateFirePower(weaponsDistributedEnergy);
            }
        }
    }

    public struct TargetData
    {
        public GameObject TargetObject { get; }
        public float UnifiedDistance { get; }

        public TargetData(GameObject obj, float d)
        {
            TargetObject = obj;
            UnifiedDistance = d;
        }
    }
}

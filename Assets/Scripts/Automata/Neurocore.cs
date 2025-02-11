using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Neurocore : MonoBehaviour
{
    public int simulationDelay = 1;

    public string unitInfo;
    public List<Weapon> attachedWeapons = new List<Weapon>();
    public string shipCoreTechnology;
    public string populationData;
    public List<Automata> automataArray = new List<Automata>();
    public string automataData;

    [SerializeField]
    private bool initialized;
    [SerializeField]
    private bool online;
    private bool isActivationFailed;

    public bool engaging;
    public bool retreating;

    [SerializeField]
    private bool strafing;
    [SerializeField]
    private bool orbiting;
    [SerializeField]
    private bool charging;
    [SerializeField]
    private bool firing;

    public bool gathering;

    private void Awake()
    {

    }
    private void OnEnable()
    {
        Initialize(() =>
        {
            initialized = true;
        });
    }

    private void Update()
    {
        if (!online) Activate();
    }

    private bool Initialize(Action onInitializationComplete) // Add a callback parameter
    {
        StartCoroutine(InitializeCoroutine(onInitializationComplete)); // Pass the callback to the coroutine
        return false; // Initialize() returns false immediately (coroutine will set it later)
    }
    private IEnumerator InitializeCoroutine(Action onInitializationComplete)
    {
        string _name = gameObject.name;
        Debug.Log($"Initializing Neurocore of {_name} Unit...");
        int initialSkip = 3 * simulationDelay;
        for (int i = 0; i < initialSkip; i++)
        {
            yield return null;
        }
        Unit unit = GetComponentInParent<Unit>();
        if (unit != null)
        {
            unitInfo = $"Type: {unit.unitType}\n" +
                $"Class: {unit.unitClass}\n" +
                $"Role: {unit.unitRole}\n" +
                $"Name: {unit.unitName}";

        }
        else
        {
            Debug.Log("No info available");
            unitInfo = "Unknown";
        }
        Debug.Log($"unit Information: {unitInfo}");

        Debug.Log($"Searching for subunits...");
        int subunitsSkip = 15 * simulationDelay;
        for (int i = 0; i < subunitsSkip; i++)
        {
            yield return null;
        }
        attachedWeapons.Clear();
        FindAllWeaponsInChildren(transform); // Start the recursive search from the Neurocore's transform
        Debug.Log($"Found {attachedWeapons.Count} weapons attached to the unit.");

        Debug.Log($"Examining the origin...");
        int originSkip = 30;
        for (int i = 0; i < originSkip; i++)
        {
            yield return null;
        }
        // Find ShipCore component and get technology string
        ShipCore shipCore = GetComponentInParent<ShipCore>(); // Search up the hierarchy
        if (shipCore != null)
        {
            shipCoreTechnology = shipCore.technology; // Assign the public string
        }
        else
        {
            Debug.LogError("ShipCore component not found in parent GameObjects!");
            shipCoreTechnology = "Unknown"; // Or handle the error as appropriate
        }
        Debug.Log($"Ship's Technology is {shipCoreTechnology}.");

        Debug.Log($"Examining population...");
        int populationSkip = 60 * simulationDelay;
        for (int i = 0; i < populationSkip; i++)
        {
            yield return null;
        }
        Population population = GetComponentInParent<Population>();
        if (population != null)
        {
            // Store population data in a single string
            populationData = $"Race: {population.race}\n" +
                            $"Number: {population.number}\n" +
                            $"Governance: {population.governance}\n" +
                            $"Tech Level: {population.techLevel}\n" +
                            $"Enlightenment Level: {population.enlightenmentLevel}";
        }
        else
        {
            Debug.LogError("Population component not found in parent GameObjects!");
            populationData = "Unknown";
        }
        Debug.Log($"Population Data: {populationData}");

        Debug.Log($"Accesing automata array...");
        int sutomataSkip = 90 * simulationDelay;
        for (int i = 0; i < sutomataSkip; i++)
        {
            yield return null;
        }
        AutomataList automataList = GetComponent<AutomataList>();
        if (automataList != null)
        {
            automataData = ""; // New variable to store concatenated automata data
            foreach (Automata automata in automataList.automatas)
            {
                automataArray.Add(new Automata(automata.name, automata.type));
                //Debug.Log($"Added Automation: {automata.name} - Type: {automata.type}");
                // Build the concatenated string
                automataData += $"{automata.name} | {automata.type}\n";
            }
            // Remove the last newline character
            if (automataData.Length > 0)
            {
                automataData = automataData.TrimEnd('\n');
            }
        }
        else
        {
            Debug.LogError("Automata array unaccessible!");
            automataData = "Unknown";
        }
        Debug.Log($"Automata Array: {automataData}");

        initialized = true;
        isActivationFailed = false;
        Debug.Log("Initialization is complete.");

        onInitializationComplete?.Invoke(); // Call the callback function when done

        yield return null;
    }

    private void FindAllWeaponsInChildren(Transform parent)
    {
        foreach (Transform child in parent)
        {
            Weapon weapon = child.GetComponent<Weapon>();
            if (weapon != null)
            {
                attachedWeapons.Add(weapon);
            }

            // Recursively search the child's children:
            FindAllWeaponsInChildren(child); // Call the function again for the child
        }
    }

    private bool Activate()
    {
        if (!isActivationFailed)
        {
            Debug.Log("Attempting to activate NC...");

            if (initialized) //Check here too, just in case
            {
                Debug.Log("Neurocore activated.");
                online = true;
                return online;
            }
            else
            {
                Debug.Log("Neurocore activation failed, waiting for initialization...");
                online = false;
                isActivationFailed = true;
                return online;
            }

        }
        return online;
    }
}




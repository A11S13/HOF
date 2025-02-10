using UnityEngine;
using System.Collections;

public class ProceduralGenerator : MonoBehaviour
{
    private WorldProcessor _processor;
    private WorldStateManager _stateManager;
    private float AOIR;
    private float AOI;

    public float exclusionRadius; // to spawn outside the screen

    void Awake()
    {
        _stateManager = GetComponent<WorldStateManager>();
        _processor = GetComponent<WorldProcessor>();
    }

    private void Start()
    {
        AOIR = _processor.areaOfInterestRadius;
        AOI = AOIR * AOIR * 3.14f;
        exclusionRadius = 200;
        GenerateInsideAreaOfInterest();
        SimulateOutsideOfBounds();
    }
    public void GenerateInsideAreaOfInterest()
    {
        //Debug.Log($"AOI is: {AOI}");
        float exclusionArea = exclusionRadius * exclusionRadius * 3.14f;
        float areaToGenerate = AOI - exclusionArea;

        //Debug.Log($"ALWAYS GENERATING SHIT FOR THE AREA: {areaToGenerate}!");
        // TODO: Actual procedural generation implementation
    }

    public void SimulateOutsideOfBounds()
    {
        //Debug.Log("Simulating shit every 10 frames");
        // TODO: Actual simulation implementation
        //_stateManager.Awakened = false;
    }
}
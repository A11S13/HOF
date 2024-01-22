using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Oscilator : MonoBehaviour
{
    ChipSynth _cS;

    public float phase1 = 0f;
    public float phase2 = 0f;

    private Vector3 initialPosition0;
    private Vector3 initialPosition1;
    private LineRenderer oscilatorVisual;

    // Start is called before the first frame update
    void Start()
    {
        _cS = GameObject.Find("ChipSynth").GetComponent<ChipSynth>();
        oscilatorVisual = GameObject.Find("Oscilator").GetComponent<LineRenderer>();
        initialPosition0 = oscilatorVisual.GetPosition(0);
        initialPosition1 = oscilatorVisual.GetPosition(1);
    }

    // Update is called once per frame
    void Update()
    {
        OSC();
    }

    void OSC()
    {
        phase1 = Math.Abs(_cS.currentPhase1 % (1));
        phase2 = Math.Abs(_cS.currentPhase2 % (1));

        float x0 = initialPosition0.x;
        float z0 = initialPosition0.z + phase1;
        float x1 = initialPosition1.x;
        float z1 = initialPosition1.z + phase2;

        oscilatorVisual.SetPosition(0, new Vector3(x0, 0, z0));
        oscilatorVisual.SetPosition(1, new Vector3(x1, 0, z1));
    }
}

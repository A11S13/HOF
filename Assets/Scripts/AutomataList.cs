using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Automata
{
    public string name;
    public string type;

    public Automata(string name, string type)
    {
        this.name = name;
        this.type = type;
    }
}

public class AutomataList : MonoBehaviour  // <--- This is the crucial line that was missing!
{
    public List<Automata> automatas = new List<Automata>();

    public void PrintAutomations()
    {
        foreach (Automata automation in automatas)
        {
            Debug.Log($"Automation: {automation.name} - Type: {automation.type}");
        }
    }
}
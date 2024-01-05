using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CanvasDebug : MonoBehaviour
{
    private List<string> resultHistory = new List<string>();
    private List<string> matchedHistory = new List<string>();
    private List<string> randomizedHistory = new List<string>();
    private int maxHistoryCount = 32;
    private int maxMatchedHistoryCount = 8;
    private int maxRandomizedHistoryCount = 8;
    private Text extractedTextComponent;
    private Text matchedTextComponent;
    private Text randomizedTextComponent;

    void Start()
    {
        extractedTextComponent = transform.Find("Extracted")?.GetComponent<Text>();
        matchedTextComponent = transform.Find("Matched")?.GetComponent<Text>();
        randomizedTextComponent = transform.Find("Randomized")?.GetComponent<Text>();
    }

    void Update() { }

    public void ShowResult(char[] result)
    {
        string newResult = new string(result);
        resultHistory.Add(newResult);
        if (resultHistory.Count > maxHistoryCount)
        {
            resultHistory.RemoveAt(0);
        }
        extractedTextComponent.text = string.Join(" ", resultHistory);
    }

    public void ShowMatched(char[] result, string name)
    {
        string newResult = new string(result);
        string combinedResult = $"{name}:{newResult}";
        matchedHistory.Add(combinedResult);
        if (matchedHistory.Count > maxMatchedHistoryCount)
        {
            matchedHistory.RemoveAt(0);
        }
        matchedTextComponent.text = string.Join(" ", matchedHistory);
    }

    public void ShowRandomized(char[] result, string name)
    {
        string newResult = new string(result);
        string combinedResult = $"{name}:{newResult}";
        randomizedHistory.Add(combinedResult);
        if (randomizedHistory.Count > maxRandomizedHistoryCount)
        {
            randomizedHistory.RemoveAt(0);
        }
        randomizedTextComponent.text = string.Join(" ", randomizedHistory);
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CanvasDebug : MonoBehaviour
{
    private List<string> resultHistory = new List<string>();
    private List<string> matchedHistory = new List<string>();
    private int maxHistoryCount = 32;
    private int maxMatchedHistoryCount = 8;
    private Text extractedTextComponent;
    private Text matchedTextComponent;

    void Start()
    {
        extractedTextComponent = transform.Find("Extracted")?.GetComponent<Text>();
        matchedTextComponent = transform.Find("Matched")?.GetComponent<Text>();
    }

    void Update() { }

    public void ShowResult(char[] result)
{
    // Convert the char array to string
    string newResult = new string(result);

    // Add the new result to the history
    resultHistory.Add(newResult);

    // If the history count exceeds the maximum, trim it
    if (resultHistory.Count > maxHistoryCount)
    {
        resultHistory.RemoveAt(0);
    }

    // Update the extractedTextComponent.text with the last results
    extractedTextComponent.text = string.Join(" ", resultHistory);
}

    public void ShowMatched(char[] result, string name)
    {
        // Convert the char array to a string
        string newResult = new string(result);

        // Combine the name and the new result into a single string
        string combinedResult = $"{name}:{newResult}";

        // Add the new result to the history
        matchedHistory.Add(combinedResult);

        // If the history count exceeds the maximum, trim it
        if (matchedHistory.Count > maxMatchedHistoryCount)
        {
            matchedHistory.RemoveAt(0);
        }

        // Update the matchedTextComponent.text with the last 8 results
        matchedTextComponent.text = string.Join(" ", matchedHistory);
    }
}

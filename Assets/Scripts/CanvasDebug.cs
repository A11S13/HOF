using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CanvasDebug : MonoBehaviour
{
    private List<string> femalesHistory = new List<string>();
    private List<string> malesHistory = new List<string>();
    private List<string> matchedHistory = new List<string>();
    private List<string> heroeHistory = new List<string>();
    private List<string> citizenHistory = new List<string>();
    private int maxFemalesHistoryCount = 8;
    private int maxMalesHistoryCount = 8;
    private int maxMatchedHistoryCount = 8;
    private int maxHeroeHistoryCount = 8;
    private int maxCitizenHistoryCount = 8;
    private Text femalesTextComponent;
    private Text malesTextComponent;
    private Text matchedTextComponent;
    private Text heroeTextComponent;
    private Text citizenTextComponent;
    private Text femalesCountTextComponent;
    private Text malesCountTextComponent;
    private Text femalesHeroesCountTextComponent;
    private Text malesHeroesCountTextComponent;
    private Text femalesCitizensCountTextComponent;
    private Text malesCitizensCountTextComponent;

    void Start()
    {
        femalesTextComponent = transform.Find("Females")?.GetComponent<Text>();
        malesTextComponent = transform.Find("Males")?.GetComponent<Text>();
        matchedTextComponent = transform.Find("Matched")?.GetComponent<Text>();
        heroeTextComponent = transform.Find("Heroes")?.GetComponent<Text>();
        citizenTextComponent = transform.Find("Citizens")?.GetComponent<Text>();
        femalesCountTextComponent = transform.Find("F")?.GetComponent<Text>();
        malesCountTextComponent = transform.Find("M")?.GetComponent<Text>();
        femalesHeroesCountTextComponent = transform.Find("FH")?.GetComponent<Text>();
        malesHeroesCountTextComponent = transform.Find("MH")?.GetComponent<Text>();
        femalesCitizensCountTextComponent = transform.Find("FC")?.GetComponent<Text>();
        malesCitizensCountTextComponent = transform.Find("MC")?.GetComponent<Text>();
    }

    public void UpdateFCCount(int fc)
    {
        string newResult = fc.ToString();
        string combinedResult = $"F: {newResult}";
        femalesCitizensCountTextComponent.text = combinedResult;
    }

    public void UpdateMCCount(int mc)
    {
        string newResult = mc.ToString();
        string combinedResult = $"M: {newResult}";
        malesCitizensCountTextComponent.text = combinedResult;
    }

    public void UpdateFHCount(int fh)
    {
        string newResult = fh.ToString();
        string combinedResult = $"F: {newResult}";
        femalesHeroesCountTextComponent.text = combinedResult;
    }

    public void UpdateMHCount(int mh)
    {
        string newResult = mh.ToString();
        string combinedResult = $"M: {newResult}";
        malesHeroesCountTextComponent.text = combinedResult;
    }

    public void UpdateF(int f)
    {
        femalesCountTextComponent.text = f.ToString();
    }

    public void UpdateM(int m)
    {
        malesCountTextComponent.text = m.ToString();
    }

    void Update() { }

    public void ShowCitizenResult(char[] result, string sex)
    {
        string newResult = new string(result);
        string combinedResult = $"{sex}: {newResult}";
        citizenHistory.Add(combinedResult);
        if (citizenHistory.Count > maxCitizenHistoryCount)
        {
            citizenHistory.RemoveAt(0);
        }
        citizenTextComponent.text = string.Join(" ", citizenHistory);
    }

    public void ShowHeroeResult(char[] result, string sex)
    {
        string newResult = new string(result);
        string combinedResult = $"{sex}: {newResult}";
        heroeHistory.Add(combinedResult);
        if (heroeHistory.Count > maxHeroeHistoryCount)
        {
            heroeHistory.RemoveAt(0);
        }
        heroeTextComponent.text = string.Join(" ", heroeHistory);
    }

    public void ShowFemalesResult(char[] result)
    {
        string newResult = new string(result);
        femalesHistory.Add(newResult);
        if (femalesHistory.Count > maxFemalesHistoryCount)
        {
            femalesHistory.RemoveAt(0);
        }
        femalesTextComponent.text = string.Join(" ", femalesHistory);
    }

    public void ShowMalesResult(char[] result)
    {
        string newResult = new string(result);
        malesHistory.Add(newResult);
        if (malesHistory.Count > maxMalesHistoryCount)
        {
            malesHistory.RemoveAt(0);
        }
        malesTextComponent.text = string.Join(" ", malesHistory);
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
}

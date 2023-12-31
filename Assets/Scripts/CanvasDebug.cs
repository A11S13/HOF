using UnityEngine.UI;
using UnityEngine;

public class CanvasDebug : MonoBehaviour
{
    private Text textComponent;

    void Start()
    {
        textComponent = GetComponent<Text>();
    }

    void Update()
    {
        textComponent.text = "ROLLING";
    }

    public void ShowResult(char[] result)
    {
        // Update the textComponent with the passed result
        textComponent.text = new string(result);
    }
}

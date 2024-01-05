using UnityEngine;
using UnityEngine.UI;

public class FPSDisplay : MonoBehaviour
{
    private float fps = 0f;
    private float updateInterval = 0.1f;
    private float timeElapsed = 0f;
    private int frameCount = 0;

    void Update()
    {
        timeElapsed += Time.deltaTime;
        frameCount++;
        
        if (timeElapsed >= updateInterval)
        {
            UpdateFPS();
        }
    }

    void UpdateFPS()
    {
        fps = frameCount / timeElapsed;
        GetComponent<Text>().text = fps.ToString("0");  // Access Text component using GetComponent

        // Reset counters
        frameCount = 0;
        timeElapsed = 0f;
    }
}

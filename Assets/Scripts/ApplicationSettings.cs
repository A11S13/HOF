using UnityEngine;

public class ApplicationSettings : MonoBehaviour
{

    public int FPS;
    public float  TimeScale;

    void Start()
    {
        FPS = -1;
        TimeScale = 1f;
        // Lock the screen orientation to Landscape
        Screen.orientation = ScreenOrientation.Landscape;

        // Set the target frame rate
        Application.targetFrameRate = FPS;

        // Calculate the desired time scale based on the target frame rate
        //float desiredTimeScale = 1f / targetFrameRate;

        // Set the time scale to synchronize with the frame rate
        Time.timeScale = TimeScale;
    }

    void Update()
    {
        Application.targetFrameRate = FPS;
        Time.timeScale = TimeScale;
    }
}

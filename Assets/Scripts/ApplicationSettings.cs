using UnityEngine;

public class ApplicationSettings : MonoBehaviour
{
    void Start()
    {
        // Lock the screen orientation to Landscape
        Screen.orientation = ScreenOrientation.Landscape;
        Application.targetFrameRate = -1;
    }
}

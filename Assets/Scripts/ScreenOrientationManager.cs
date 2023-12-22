using UnityEngine;

public class ScreenOrientationManager : MonoBehaviour
{
    void Start()
    {
        // Lock the screen orientation to Landscape
        Screen.orientation = ScreenOrientation.Landscape;
    }
}

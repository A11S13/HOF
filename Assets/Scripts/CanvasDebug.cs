using UnityEngine.UI;
using UnityEngine;

public class CanvasDebug : MonoBehaviour
{
    private Gyroscope _gyro;
    // Start is called before the first frame update
    void Start()
    {
        ChaosGenerator _chaosGenerator = FindObjectOfType<ChaosGenerator>();
        _gyro = Input.gyro;
        
    }

    // Update is called once per frame
    void Update()
    {
        Text textComponent = GetComponent<Text>();
        textComponent.text = "Gyro Attitude X: " + _gyro.attitude.x.ToString("F12");
    }
}

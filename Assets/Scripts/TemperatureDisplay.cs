using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public class TemperatureDisplay : MonoBehaviour
{
    private float tempValue;

    void Start()
    {
        if (UnityEngine.InputSystem.AmbientTemperatureSensor.current != null)
        {
            InputSystem.EnableDevice(UnityEngine.InputSystem.AmbientTemperatureSensor.current);
            tempValue =
                UnityEngine.InputSystem.AmbientTemperatureSensor.current.ambientTemperature.ReadValue();
        }
    }

    void Update()
    {
        if (AmbientTemperatureSensor.current.enabled)
        {
            Debug.Log("Gyroscope is enabled");
            tempValue = UnityEngine.InputSystem.AmbientTemperatureSensor.current.ambientTemperature.ReadValue();
        }
        Debug.Log(tempValue);
    }
}

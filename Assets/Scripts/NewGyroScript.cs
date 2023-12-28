using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public class NewGyroScript : MonoBehaviour
{
    private UnityEngine.InputSystem.Gyroscope gyro; // Using the alias for clarity
    private Vector3 gyroValue;

    void Start()
    {
        
        if (UnityEngine.InputSystem.Gyroscope.current != null)
        {
            InputSystem.EnableDevice(UnityEngine.InputSystem.Gyroscope.current);
            gyroValue = UnityEngine.InputSystem.Gyroscope.current.angularVelocity.ReadValue();
        }
    }


    void Update()
    {
        if (UnityEngine.InputSystem.Gyroscope.current != null)
        {
            InputSystem.EnableDevice(UnityEngine.InputSystem.Gyroscope.current);
            // Cache the current gyroscope value
            Vector3 currentGyroValue = UnityEngine.InputSystem.Gyroscope.current.angularVelocity.ReadValue();

            if (!currentGyroValue.Equals(gyroValue))
            {
                gyroValue = currentGyroValue;
                Debug.Log(gyroValue);
            }
        }
    }
}

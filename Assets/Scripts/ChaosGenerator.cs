using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChaosGenerator : MonoBehaviour
{
    RandomSounds _rs;
    public float xRotation { get; private set; }
    public float yRotation { get; private set; }
    public float zRotation { get; private set; }
    public float wRotation { get; private set; }
    public float xRotationRate { get; private set; }
    public float yRotationRate { get; private set; }
    public float zRotationRate { get; private set; }
    public float xRotationRateUnbiased { get; private set; }
    public float yRotationRateUnbiased { get; private set; }
    public float zRotationRateUnbiased { get; private set; }
    public float xGravity { get; private set; }
    public float yGravity { get; private set; }
    public float zGravity { get; private set; }
    public float xUserAcceleration { get; private set; }
    public float yUserAcceleration { get; private set; }
    public float zUserAcceleration { get; private set; }

    public float delay = 1f;
    public bool isWaiting;
    public float value;
    public char[] firstChar;
    public char[] Extracted;
    public float lastBeautyValue = 0;
    public int beautyNumbers = 0;
    public int matches = 0;
    public char[] lastBeautyNumber = new char[5];

    [SerializeField]
    public List<char[]> extractedNumbers = new List<char[]>();

    private bool gyroIsInitialized;
    private float previousValue;
    private char[] prevResult = new char[5];

    public Gyroscope _gyro;
    private CanvasDebug _canvasDebug;

    void Awake()
    {
        if (SystemInfo.supportsGyroscope)
        {
            _gyro = Input.gyro;
            StartCoroutine(InitializeGyro());
        }
        else
        {
            _gyro = null;
        }

        extractedNumbers = new List<char[]>();
    }

    void Start()
    {
        

        _rs = GetComponent<RandomSounds>();
        StartCoroutine(InitializeGyro());
        previousValue = _gyro.attitude.x;
        value = previousValue;
    }

    void Update()
    {
        ProcessGyroData();
        //ProcessSingleGyroData();
    }

    void ProcessGyroData()
    {
        value = _gyro.attitude.x;
        if (_gyro != null && value != 0 && value != 1)
        {
            if (!isWaiting)
            {
                if (!Mathf.Approximately(previousValue, value))
                {
                    ExtractAllNumbers();

                    previousValue = value;
                }
                StartCoroutine(Wait(delay));
            }
        }
    }

    void ExtractAllNumbers()
    {
        extractedNumbers.Clear();
        extractedNumbers.Add(ExtractNumber(Input.gyro.attitude.x));
        extractedNumbers.Add(ExtractNumber(Input.gyro.attitude.y));
        extractedNumbers.Add(ExtractNumber(Input.gyro.attitude.z));
        extractedNumbers.Add(ExtractNumber(Input.gyro.attitude.w));
        extractedNumbers.Add(ExtractNumber(Input.gyro.rotationRate.x));
        extractedNumbers.Add(ExtractNumber(Input.gyro.rotationRate.y));
        extractedNumbers.Add(ExtractNumber(Input.gyro.rotationRate.z));
        extractedNumbers.Add(ExtractNumber(Input.gyro.rotationRateUnbiased.x));
        extractedNumbers.Add(ExtractNumber(Input.gyro.rotationRateUnbiased.y));
        extractedNumbers.Add(ExtractNumber(Input.gyro.rotationRateUnbiased.z));
        extractedNumbers.Add(ExtractNumber(Input.gyro.gravity.x));
        extractedNumbers.Add(ExtractNumber(Input.gyro.gravity.y));
        extractedNumbers.Add(ExtractNumber(Input.gyro.gravity.z));
        extractedNumbers.Add(ExtractNumber(Input.gyro.userAcceleration.x));
        extractedNumbers.Add(ExtractNumber(Input.gyro.userAcceleration.y));
        extractedNumbers.Add(ExtractNumber(Input.gyro.userAcceleration.z));
    }

    public char[] ExtractNumber(float value)
    {
        value = Math.Abs(value);
        char[] result = new char[5];
        char[] prevResult = new char[5];
        if (value == 1)
        {
            for (int i = 0; i < 5; i++)
            {
                result[i] = '0';
            }
        }
        else
        {
            char[] charValue = value.ToString("F12").ToCharArray();
            firstChar = charValue; //Debug purpose
            int lastIndex = 11;
            // Find the index of the last non-zero digit
            while (lastIndex >= 0 && charValue[lastIndex] == '0')
            {
                lastIndex--;
            }
            if (lastIndex < 6)
            {
                for (int i = 0; i < lastIndex - 1; i++)
                {
                    result[i] = charValue[lastIndex - i];
                }
                for (int i = 0; i < 5; i++)
                {
                    if (result[i] == default(char))
                        result[i] = '0';
                }
            }
            else
            {
                for (int i = 0; i < 5; i++)
                {
                    result[i] = charValue[lastIndex - i];
                }
            }
        }
        Reverse5Digits(result);
        if (Match5Digits(prevResult, result))
        {
            matches++;
            Debug.Log("OOOPS, MATCH! " + string.Join("", prevResult));
        }
        Extracted = result;
        prevResult = result;
        _rs.PlaySound(result);
        return result;
    }

    void ProcessSingleGyroData()
    {
        //value = 1.0f;
        if (_gyro != null && value != 0)
        {
            if (!isWaiting)
            {
                if (!Mathf.Approximately(previousValue, value))
                {
                    char[] extractedXRotation = ExtractNumber(value);
                    Extracted = extractedXRotation;
                    previousValue = value;
                }
                StartCoroutine(Wait(delay));
            }
        }
    }

    private bool Match5Digits(char[] prev, char[] curr)
    {
        int matchedCount = 0;

        for (int i = 0; i < 5; i++)
        {
            if (prev[i] == curr[i])
            {
                matchedCount++;
            }
            else
            {
                break; // Stop comparison if a mismatch is found
            }
        }

        return matchedCount == 5; // Return true only if all 5 digits match
    }

    private char[] Reverse5Digits(char[] array)
    {
        if (CheckAllElementsEqual(array))
        {
            lastBeautyNumber = array;
            lastBeautyValue = value;
            beautyNumbers++;
            Debug.Log("BINGO! " + string.Join("", array));
            return array; // Early exit if all elements are the same
        }

        if (array[0] != array[4])
        {
            char temp = array[0];
            array[0] = array[4];
            array[4] = temp;
        }

        if (array[1] != array[3])
        {
            char temp = array[1];
            array[1] = array[3];
            array[3] = temp;
        }

        return array;
    }

    private bool CheckAllElementsEqual(char[] array)
    {
        return array[0] == array[1]
            && array[1] == array[2]
            && array[2] == array[3]
            && array[3] == array[4];
    }

    public void GyroChaos()
    {
        Quaternion rotation = Input.gyro.attitude;
        xRotation = rotation.x;
        yRotation = rotation.y;
        zRotation = rotation.z;
        wRotation = rotation.w;

        Vector3 rotationRate = Input.gyro.rotationRate;
        xRotationRate = rotationRate.x;
        yRotationRate = rotationRate.y;
        zRotationRate = rotationRate.z;

        Vector3 rotationRateUnbiased = Input.gyro.rotationRateUnbiased;
        xRotationRateUnbiased = rotationRateUnbiased.x;
        yRotationRateUnbiased = rotationRateUnbiased.y;
        zRotationRateUnbiased = rotationRateUnbiased.z;

        Vector3 gravity = Input.gyro.gravity;
        xGravity = gravity.x;
        yGravity = gravity.y;
        zGravity = gravity.z;

        Vector3 userAcceleration = Input.gyro.userAcceleration;
        xUserAcceleration = userAcceleration.x;
        yUserAcceleration = userAcceleration.y;
        zUserAcceleration = userAcceleration.z;
    }

    IEnumerator InitializeGyro()
    {
        Input.gyro.enabled = false;
        Input.gyro.enabled = true;
        _gyro = Input.gyro;
        yield return null;
    }

    private IEnumerator Wait(float delay)
    {
        isWaiting = true;
        yield return new WaitForSeconds(delay);
        isWaiting = false;
    }

    private void OnDestroy()
    {
        if (_gyro != null)
        {
            _gyro.enabled = false;
        }
    }
}

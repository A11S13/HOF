using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

public class ChaosGenerator : MonoBehaviour
{
    public int NRLPScale = 7;
    public int NRLPOffset = 28;
    public int lastProjected;
    public int preLastProjected;
    public bool eraseSRN;
    public float delay = 1f;
    public bool isWaiting;
    public float value;
    public char[] firstChar;
    public char[] Extracted;
    public double lastBeautyValue = 0;
    public int beautyNumbers = 0;
    public int matches = 0;
    public char[] lastBeautyNumber = new char[5];
    public List<int> reducedNumbers = new List<int>();
    public List<char[]> extractedNumbers = new List<char[]>();
    public List<double> doubleNumbers = new List<double>();

    public byte[] serializedBollArray;

    int prevMax = 0;
    int prevMin = 512;
    private Gyroscope _gyro;
    private CanvasDebug _cD;
    private RandomSounds _rS;
    private List<int[]> pairs = new List<int[]>();
    private bool[,] uSRN = new bool[512, 512];

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
            Debug.Log("Gyroscope Unavailable");
        }
    }

    void Start()
    {
        reducedNumbers = new List<int>(Enumerable.Repeat(0, 16));
        InitializePairsList();
        _rS = GetComponent<RandomSounds>();
        _cD = GameObject.Find("Canvas")?.transform.Find("DEBUG")?.GetComponent<CanvasDebug>();
        InitializeDataTables();
        serializedBollArray = new byte[32768];
    }

    void Update()
    {
        ProcessGyroData();
        SerializeReducedNumbers();

        if (eraseSRN)
        {
            uSRN = new bool[512, 512];
            eraseSRN = false;
        }
    }

    void ProcessGyroData()
    {
        if (!isWaiting)
        {
            ExtractAllNumbers();
            ReduceAllNumbers();

            StartCoroutine(Wait(delay));
        }
    }

    public char[] ExtractNumber(double value, string name)
    {
        if (value != 0)
        {
            value = Math.Abs(value);
            if (SiftOutUnbiasedRotation(value, name))
            {
                double previousValue = GetPrevValue(name);
                if (Math.Abs(previousValue - value) != 0)
                {
                    UpdateValueInList(value, name);
                    char[] result = new char[5];
                    char[] prevResult = GetPrevResult(name);

                    if (value == 1)
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            result[i] = '0';
                            _rS.PlaySound(new char[] { '0', '0', '0', '0', '0' });
                        }
                    }
                    else
                    {
                        char[] charValue = value.ToString("F17").ToCharArray();
                        firstChar = charValue; // Debug purpose
                        int lastIndex = charValue.Length - 1;

                        // Find the index of the last non-zero digit
                        while (lastIndex >= 0 && charValue[lastIndex] == '0')
                        {
                            lastIndex--;
                        }

                        if (lastIndex < 6) //Filling with zeros missing digits
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

                    Reverse5Digits(result, name);

                    if (Match5Digits(prevResult, result))
                    {
                        matches++;
                        Debug.Log("OOOPS, MATCH! " + name + " " + string.Join("", prevResult));
                        Debug.Log("previousValue: " + previousValue.ToString("F17"));
                        Debug.Log("value: " + value.ToString("F17"));
                        Debug.Log("prevResult:" + new string(prevResult));
                        Debug.Log("Result:" + new string(result));
                        _rS.PlaySound(new char[] { '0', '0', '0', '0', '0' });
                        result = Randomize5Digits(); //Resolving collision
                        _cD.ShowRandomized(result, name);
                        Debug.Log("Randomized to: " + new string(result));
                        if (CheckAllElementsEqual(result, name)) //Additional beauty number check
                        {
                            _rS.PlaySound(result);
                            _cD.ShowMatched(result, name);
                            lastBeautyNumber = result;
                            lastBeautyValue = value;
                            beautyNumbers++;
                            Debug.Log("BINGO! " + string.Join("", result));
                        }
                    }
                    Extracted = result;
                    _cD.ShowResult(result);
                    CheckForZero(result, name);
                    return result;
                }
                return GetPrevResult(name);
            }
            //return Randomize5Digits();
            return GetPrevResult(name);
        }
        return GetPrevResult(name);
    }

    private void CheckForZero(char[] result, string name)
    {
        char[] zeroArray = new char[] { '0', '0', '0', '0', '0' };

        if (Enumerable.SequenceEqual(result, zeroArray))
        {
            _rS.PlaySound(zeroArray);
            Debug.Log(name);
        }
    }

    private bool CheckAllElementsEqual(char[] result, string name)
    {
        return result[0] == result[1]
            && result[1] == result[2]
            && result[2] == result[3]
            && result[3] == result[4];
    }

    private char[] Reverse5Digits(char[] result, string name)
    {
        if (CheckAllElementsEqual(result, name))
        {
            _rS.PlaySound(result);
            _cD.ShowMatched(result, name);
            lastBeautyNumber = result;
            lastBeautyValue = value;
            beautyNumbers++;
            Debug.Log("BINGO! " + string.Join("", result));
            return result; // Early exit if all elements are the same
        }
        if (result[0] != result[4]) //Skip if first and last are equal
        {
            char temp = result[0];
            result[0] = result[4];
            result[4] = temp;
        }
        if (result[1] != result[3])
        {
            char temp = result[1];
            result[1] = result[3];
            result[3] = temp;
        }
        return result;
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

    private char[] Randomize5Digits()
    {
        char[] result = new char[5];

        using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
        {
            byte[] randomBytes = new byte[5]; // 5 bytes for 5 digits

            // Fill the byte array with random values
            rng.GetBytes(randomBytes);

            for (int i = 0; i < 5; i++)
            {
                int randomNumber = randomBytes[i] % 10; // Ensure it's between 0 and 9
                result[i] = (char)(randomNumber + '0'); // Convert to char
            }
        }

        return result;
    }

    private bool SiftOutUnbiasedRotation(double value, string name)
    {
        if (!name.StartsWith("U"))
        {
            return true;
        }
        else
        {
            switch (name)
            {
                case "UX":
                    if (value != GetPrevValue("RX"))
                    {
                        return true;
                    }
                    break;
                case "UY":
                    if (value != GetPrevValue("RY"))
                    {
                        return true;
                    }
                    break;
                case "UZ":
                    if (value != GetPrevValue("RZ"))
                    {
                        return true;
                    }
                    break;
            }
            return false;
        }
    }

    private void InitializePairsList()
    {
        for (int i = 0; i < 8; i++)
        {
            pairs.Add(new int[] { 0, 0 });
        }
    }

    private void SerializeReducedNumbers()
    {
        pairs[0] = new int[] { reducedNumbers[0], reducedNumbers[1] };
        pairs[1] = new int[] { reducedNumbers[2], reducedNumbers[3] };
        pairs[2] = new int[] { reducedNumbers[4], reducedNumbers[5] };
        pairs[3] = new int[] { reducedNumbers[6], reducedNumbers[7] };
        pairs[4] = new int[] { reducedNumbers[8], reducedNumbers[9] };
        pairs[5] = new int[] { reducedNumbers[10], reducedNumbers[11] };
        pairs[6] = new int[] { reducedNumbers[12], reducedNumbers[13] };
        pairs[7] = new int[] { reducedNumbers[14], reducedNumbers[15] };

        // Set elements in uSRN based on the pairs using a loop
        foreach (var pair in pairs)
        {
            int x = pair[0];
            int y = pair[1];

            uSRN[x - 1, y - 1] = true;
        }

        int width = uSRN.GetLength(0);
        int height = uSRN.GetLength(1);

        // Create a byte array to hold the boolean data efficiently
        int byteCount = width * height / 8; // Each byte holds 8 bits
        byte[] bytes = new byte[byteCount];

        int byteIndex = 0;
        int bitIndex = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Pack booleans into bytes
                if (uSRN[x, y])
                {
                    bytes[byteIndex] |= (byte)(1 << (7 - bitIndex)); // Set the corresponding bit
                }

                bitIndex++;
                if (bitIndex == 8)
                {
                    bitIndex = 0;
                    byteIndex++;
                }
            }
        }
        serializedBollArray = bytes;
    }

    private void NRLPCalibrator(int outputInteger)
    {
        if (outputInteger > prevMax)
            prevMax = outputInteger;
        if (outputInteger < prevMin)
            prevMin = outputInteger;
    }

    private int ApplyNRLP(char[] inputDigits)
    {
        // Structured Projection Vectors
        char[][] projectionVectors = new char[][]
        {
            new char[] { '1', '0', '1', '0', '1' },
            new char[] { '0', '1', '0', '1', '0' },
            new char[] { '1', '0', '1', '0', '1' }
        };

        // Project onto each vector and combine results
        int projectedValue = 0;
        foreach (char[] projectionVector in projectionVectors)
        {
            int dotProduct = 0;
            for (int i = 0; i < 5; i++)
            {
                dotProduct += (int)(inputDigits[i] - '0') * (int)(projectionVector[i] - '0');
            }
            projectedValue += dotProduct;
        }

        // Scale to 1-512 range
        int scaledValue = projectedValue * NRLPScale; // Manual calibration default is 7
        int outputInteger = scaledValue - NRLPOffset; // Shift to not exceed 512 default is 28
        if (outputInteger < 1)
            outputInteger = 1;
        if (outputInteger > 512)
            outputInteger = 512;
        NRLPCalibrator(outputInteger);

        preLastProjected = lastProjected;
        lastProjected = outputInteger;

        return outputInteger;
    }

    void ReduceAllNumbers()
    {
        // Iterate over each char[] in extractedNumbers and apply ApplyNRLP
        for (int i = 0; i < extractedNumbers.Count; i++)
        {
            int reducedNumber = ApplyNRLP(extractedNumbers[i]);
            // Assign to existing element in reducedNumbers
            reducedNumbers[i] = reducedNumber;
        }
    }

    void ExtractAllNumbers()
    {
        extractedNumbers[0] = ExtractNumber(Input.gyro.attitude.x, "AX");
        extractedNumbers[1] = ExtractNumber(Input.gyro.attitude.y, "AY");
        extractedNumbers[2] = ExtractNumber(Input.gyro.attitude.z, "AZ");
        extractedNumbers[3] = ExtractNumber(Input.gyro.attitude.w, "AW");
        extractedNumbers[4] = ExtractNumber(Input.gyro.rotationRate.x, "RX");
        extractedNumbers[5] = ExtractNumber(Input.gyro.rotationRate.y, "RY");
        extractedNumbers[6] = ExtractNumber(Input.gyro.rotationRate.z, "RZ");
        extractedNumbers[7] = ExtractNumber(Input.gyro.rotationRateUnbiased.x, "UX");
        extractedNumbers[8] = ExtractNumber(Input.gyro.rotationRateUnbiased.y, "UY");
        extractedNumbers[9] = ExtractNumber(Input.gyro.rotationRateUnbiased.z, "UZ");
        extractedNumbers[10] = ExtractNumber(Input.gyro.gravity.x, "GX");
        extractedNumbers[11] = ExtractNumber(Input.gyro.gravity.y, "GY");
        extractedNumbers[12] = ExtractNumber(Input.gyro.gravity.z, "GZ");
        extractedNumbers[13] = ExtractNumber(Input.gyro.userAcceleration.x, "CX");
        extractedNumbers[14] = ExtractNumber(Input.gyro.userAcceleration.y, "CY");
        extractedNumbers[15] = ExtractNumber(Input.gyro.userAcceleration.z, "CZ");
    }

    void CreateValueList()
    {
        doubleNumbers[0] = Input.gyro.attitude.x;
        doubleNumbers[1] = Input.gyro.attitude.y;
        doubleNumbers[2] = Input.gyro.attitude.z;
        doubleNumbers[3] = Input.gyro.attitude.w;
        doubleNumbers[4] = Input.gyro.rotationRate.x;
        doubleNumbers[5] = Input.gyro.rotationRate.y;
        doubleNumbers[6] = Input.gyro.rotationRate.z;
        doubleNumbers[7] = Input.gyro.rotationRateUnbiased.x;
        doubleNumbers[8] = Input.gyro.rotationRateUnbiased.y;
        doubleNumbers[9] = Input.gyro.rotationRateUnbiased.z;
        doubleNumbers[10] = Input.gyro.gravity.x;
        doubleNumbers[11] = Input.gyro.gravity.y;
        doubleNumbers[12] = Input.gyro.gravity.z;
        doubleNumbers[13] = Input.gyro.userAcceleration.x;
        doubleNumbers[14] = Input.gyro.userAcceleration.y;
        doubleNumbers[15] = Input.gyro.userAcceleration.z;
    }

    void UpdateValueInList(double value, string name)
    {
        switch (name)
        {
            case "AX":
                doubleNumbers[0] = value;
                break;
            case "AY":
                doubleNumbers[1] = value;
                break;
            case "AZ":
                doubleNumbers[2] = value;
                break;
            case "AW":
                doubleNumbers[3] = value;
                break;
            case "RX":
                doubleNumbers[4] = value;
                break;
            case "RY":
                doubleNumbers[5] = value;
                break;
            case "RZ":
                doubleNumbers[6] = value;
                break;
            case "UX":
                doubleNumbers[7] = value;
                break;
            case "UY":
                doubleNumbers[8] = value;
                break;
            case "UZ":
                doubleNumbers[9] = value;
                break;
            case "GX":
                doubleNumbers[10] = value;
                break;
            case "GY":
                doubleNumbers[11] = value;
                break;
            case "GZ":
                doubleNumbers[12] = value;
                break;
            case "CX":
                doubleNumbers[13] = value;
                break;
            case "CY":
                doubleNumbers[14] = value;
                break;
            case "CZ":
                doubleNumbers[15] = value;
                break;
            default:
                break;
        }
    }

    private char[] GetPrevResult(string name)
    {
        switch (name)
        {
            case "AX":
                return extractedNumbers[0];
            case "AY":
                return extractedNumbers[1];
            case "AZ":
                return extractedNumbers[2];
            case "AW":
                return extractedNumbers[3];
            case "RX":
                return extractedNumbers[4];
            case "RY":
                return extractedNumbers[5];
            case "RZ":
                return extractedNumbers[6];
            case "UX":
                return extractedNumbers[7];
            case "UY":
                return extractedNumbers[8];
            case "UZ":
                return extractedNumbers[9];
            case "GX":
                return extractedNumbers[10];
            case "GY":
                return extractedNumbers[11];
            case "GZ":
                return extractedNumbers[12];
            case "CX":
                return extractedNumbers[13];
            case "CY":
                return extractedNumbers[14];
            case "CZ":
                return extractedNumbers[15];
            default:
                return new char[5];
        }
    }

    double GetPrevValue(string name)
    {
        switch (name)
        {
            case "AX":
                return doubleNumbers[0];
            case "AY":
                return doubleNumbers[1];
            case "AZ":
                return doubleNumbers[2];
            case "AW":
                return doubleNumbers[3];
            case "RX":
                return doubleNumbers[4];
            case "RY":
                return doubleNumbers[5];
            case "RZ":
                return doubleNumbers[6];
            case "UX":
                return doubleNumbers[7];
            case "UY":
                return doubleNumbers[8];
            case "UZ":
                return doubleNumbers[9];
            case "GX":
                return doubleNumbers[10];
            case "GY":
                return doubleNumbers[11];
            case "GZ":
                return doubleNumbers[12];
            case "CX":
                return doubleNumbers[13];
            case "CY":
                return doubleNumbers[14];
            case "CZ":
                return doubleNumbers[15];
            default:
                return 0.0f;
        }
    }

    private void InitializeDataTables()
    {
        for (int i = 0; i < 16; i++)
        {
            doubleNumbers.Add(0.0);
        }

        for (int i = 0; i < 16; i++)
        {
            extractedNumbers.Add("00000".ToCharArray());
        }

        CreateValueList();
    }

    public void EraseSRNButton()
    {
        eraseSRN = true;
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

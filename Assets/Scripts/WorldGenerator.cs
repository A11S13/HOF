using System;
using System.Collections;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class WorldGenerator : MonoBehaviour
{
    GWS _gws;
    ComputeShaderController _csc;

    public float delay = 1f;
    public bool isWaiting;
    public bool RDD;
    public bool randomizeArrayData;
    public bool[,] defaultBoolArray;
    public RandomizeMethod randomizeMethod;

    public enum RandomizeMethod
    {
        LCG,
        Crypto,
        Simple
    }

    public int numberOfAsteroids = 1000;
    public bool spawnAsteroid;
    public Text consoleText;
    public byte[] serializedBollArray;

    private object fileLock = new object(); // Define a lock object
    private string default_data;
    private Texture2D _worldTexture;
    private const int lcgA = 1664525; // Multiplier
    private const int lcgC = 1013904223; // Increment
    private const long lcgM = 4294967296; // Modulus
    private double lcgSeed = 42; // Initial seed for LCG

    void Awake()
    {
        consoleText = GameObject.Find("Canvas/console").GetComponent<Text>();
        serializedBollArray = new byte[32768];
    }

    void Start()
    {
        RDD = true;
        randomizeMethod = RandomizeMethod.Crypto;
        _csc = GameObject.Find("RandomDataDraw").GetComponent<ComputeShaderController>();
        _gws = GetComponent<GWS>();
        _worldTexture = _gws.worldTexture;
        defaultBoolArray = new bool[_worldTexture.width, _worldTexture.height];
    }

    void Update()
    {
        if (RDD)
        {
            _csc.RandomDataDraw();
            //RDD = false;
        }
        if (spawnAsteroid)
        {
            //SpawnRandomAsteroids();
            spawnAsteroid = false;
        }
        if (randomizeArrayData)
        {
            if (!isWaiting)
            {
                RandomizeArray(defaultBoolArray, randomizeMethod, "Default_data");
                StartCoroutine(Wait(delay));
            }
        }
    }

    public void SerializeBoolArrayData(bool[,] passedBooleanArray)
    {
        int width = passedBooleanArray.GetLength(0);
        int height = passedBooleanArray.GetLength(1);

        // Create a byte array to hold the boolean data efficiently
        int byteCount = (width * height + 7) / 8; // Ensure space for all bits
        serializedBollArray = new byte[byteCount];

        int byteIndex = 0;
        int bitIndex = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Pack booleans into bytes
                if (passedBooleanArray[x, y])
                {
                    serializedBollArray[byteIndex] |= (byte)(1 << bitIndex); // Set the corresponding bit
                }

                bitIndex++;
                if (bitIndex == 8)
                {
                    bitIndex = 0;
                    byteIndex++;
                }
            }
        }
    }

    public void SaveArrayData(bool[,] passedBooleanArray, string file)
    {
        Debug.Log("SAVING BINARY DATA");

        int width = passedBooleanArray.GetLength(0);
        int height = passedBooleanArray.GetLength(1);

        // Create a byte array to hold the boolean data efficiently
        int byteCount = (width * height + 7) / 8; // Ensure space for all bits
        byte[] bytes = new byte[byteCount];

        int byteIndex = 0;
        int bitIndex = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Pack booleans into bytes
                if (passedBooleanArray[x, y])
                {
                    bytes[byteIndex] |= (byte)(1 << bitIndex); // Set the corresponding bit
                }

                bitIndex++;
                if (bitIndex == 8)
                {
                    bitIndex = 0;
                    byteIndex++;
                }
            }
        }

        // Save the byte array to a file in the "Data" folder
        string filePath = Path.Combine(GWS.fullPath, $"{file}.bin");
        using (FileStream fileStream = new FileStream(filePath, FileMode.Create))
        {
            fileStream.Write(bytes, 0, byteCount);
        }

        Debug.Log("Boolean array saved to: " + filePath);
#if UNITY_EDITOR
        // AssetDatabase.Refresh();
#endif
    }

    private void RandomizeArray(bool[,] passedBooleanArray, RandomizeMethod method, string file)
    {
        switch (method)
        {
            case RandomizeMethod.LCG:
                RandomizeArrayLCG(passedBooleanArray, file);
                break;
            case RandomizeMethod.Crypto:
                RandomizeArrayCrypto(passedBooleanArray, file);
                break;
            case RandomizeMethod.Simple:
                RandomizeArraySimple(passedBooleanArray, file);
                break;
            default:
                // Handle unexpected value or provide a default method
                Debug.LogWarning("Unexpected randomization method: " + method);
                break;
        }
        consoleText.text = method.ToString();
    }

    private void RandomizeArrayLCG(bool[,] passedBooleanArray, string file)
    {
        for (int y = 0; y < passedBooleanArray.GetLength(1); y++)
        {
            for (int x = 0; x < passedBooleanArray.GetLength(0); x++)
            {
                lcgSeed = (lcgA * lcgSeed + lcgC) % lcgM;
                passedBooleanArray[x, y] = lcgSeed / lcgM > 0.5;
            }
        }

        SaveArrayData(passedBooleanArray, file);
        //randomizeArrayData = false;
    }

    private async void RandomizeArrayCrypto(bool[,] passedBooleanArray, string file)
    {
        byte[] randomBytes = null;
        await Task.Run(() =>
        {
            int totalBits = passedBooleanArray.GetLength(0) * passedBooleanArray.GetLength(1);
            int totalBytes = totalBits / 8;

            randomBytes = new byte[totalBytes];

            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }

            int byteIndex = 0;
            for (int y = 0; y < passedBooleanArray.GetLength(1); y++)
            {
                for (int x = 0; x < passedBooleanArray.GetLength(0); x++)
                {
                    // Extract the relevant bit from the byte to get a boolean value
                    passedBooleanArray[x, y] = (randomBytes[byteIndex] & (1 << (x % 8))) != 0;

                    if (x % 8 == 7)
                    {
                        byteIndex++;
                    }
                }
            }
        });
        // Assign randomBytes to serializedBollArray
        serializedBollArray = randomBytes;

        //SerializeBoolArrayData(passedBooleanArray);

        //SaveArrayData(passedBooleanArray, file);
        //randomizeArrayData = false;
    }

    private void RandomizeArraySimple(bool[,] passedBooleanArray, string file)
    {
        for (int y = 0; y < passedBooleanArray.GetLength(1); y++)
        {
            for (int x = 0; x < passedBooleanArray.GetLength(0); x++)
            {
                passedBooleanArray[x, y] = UnityEngine.Random.value > 0.5f; // Assign random boolean value (true or false)
            }
        }

        SaveArrayData(passedBooleanArray, file); // Pass the array to SaveArrayData
        //randomizeArrayData = false; // Set randomizeArrayData to false here
    }

    public void FillArray(bool[,] passedBooleanArray, string file)
    {
        // Fill the array with ones
        for (int y = 0; y < passedBooleanArray.GetLength(1); y++)
        {
            for (int x = 0; x < passedBooleanArray.GetLength(0); x++)
            {
                passedBooleanArray[x, y] = true;
            }
        }

        // Save the modified array
        SaveArrayData(passedBooleanArray, file);
    }

    public void PurgeArray(bool[,] passedBooleanArray, string file)
    {
        // Fill the array with zeros
        for (int y = 0; y < passedBooleanArray.GetLength(1); y++)
        {
            for (int x = 0; x < passedBooleanArray.GetLength(0); x++)
            {
                passedBooleanArray[x, y] = false;
            }
        }

        SaveArrayData(passedBooleanArray, file); // Pass the array to SaveArrayData
    }

    public void DrawArray(bool[,] passedBooleanArray, string file)
    {
        _gws.PNGFromBoolArray(passedBooleanArray, file);
    }

    public void PrintArray(bool[,] passedBooleanArray)
    {
        Debug.Log("Printing Boolean Array:");

        // Create a StringBuilder to efficiently concatenate the rows
        System.Text.StringBuilder matrixStringBuilder = new System.Text.StringBuilder();

        for (int y = 0; y < passedBooleanArray.GetLength(1); y++)
        {
            string row = "";

            for (int x = 0; x < passedBooleanArray.GetLength(0); x++)
            {
                row += passedBooleanArray[x, y] ? "1" : "0";
            }

            matrixStringBuilder.AppendLine(row); // Append the row followed by a new line
        }

        // Print the entire matrix as a single message
        Debug.Log(matrixStringBuilder.ToString());
    }

    public void RandomizeSector(int x, int y) { }

    private IEnumerator Wait(float delay)
    {
        isWaiting = true;
        yield return new WaitForSeconds(delay);
        isWaiting = false;
    }

    public void RButton()
    {
        randomizeArrayData = !randomizeArrayData;
    }

    public void RDDButton()
    {
        RDD = !RDD;
        if (RDD)
        {
            consoleText.text = "DRAWING";
        }
        else
        {
            consoleText.text = "waiting";
        }
    }

    /*void SpawnRandomAsteroids()
        {
            for (int i = 0; i < numberOfAsteroids; i++)
            {
                // Generate random position within the desired world range
                float randomX =
                    UnityEngine.Random.Range(
                        -_gws.worldTexture.width / 2f,
                        _gws.worldTexture.width / 2f
                    ) * _gws.scaleFactor;
                float randomZ =
                    UnityEngine.Random.Range(
                        -_gws.worldTexture.height / 2f,
                        _gws.worldTexture.height / 2f
                    ) * _gws.scaleFactor;
    
                Vector3 asteroidPosition = new Vector3(randomX, 0f, randomZ);
    
                // Instantiate the asteroid prefab
                GameObject asteroid = Instantiate(
                    _gws.asteroidPrefab,
                    asteroidPosition,
                    Quaternion.identity
                );
    
                // Convert the asteroid's position to texture coordinates and write to the texture
                Vector2 textureCoords = _gws.WorldToTexture(asteroidPosition);
                _gws.WritePixelToTexture(textureCoords, Color.grey);
            }
    
            _gws.SaveWorldTexture();
        }*/
}

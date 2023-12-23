using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

public class WorldExplorer : MonoBehaviour
{
    GWS _gws;
    WorldGenerator _worldGenerator;

    public Texture2D explorationTexture;

    public bool randomizeArrayData;
    public bool fillArrayData;
    public bool purgeArrayData;
    public bool printArrayData;
    public bool drawArrayData;
    public bool createExplorationRadiusBoolArray = false;

    public Vector2 playerTextureCoords;
    public float initialExplorationRadius = 1.5f;
    public bool[,] explorationBoolArray;
    public bool[,] explorationRadiusBoolArray;
    public int explorationDiameter;

    private Texture2D _worldTexture;
    private GameObject _player;

    private const int lcgA = 1664525; // Multiplier
    private const int lcgC = 1013904223; // Increment
    private const long lcgM = 4294967296; // Modulus
    private int lcgSeed = 42; // Initial seed for LCG

    void Start()
    {
        _gws = GetComponent<GWS>();
        _worldGenerator = GetComponent<WorldGenerator>();
        _player = GameObject.FindGameObjectWithTag("Player");
        _worldTexture = _gws.worldTexture;
        explorationBoolArray = new bool[_worldTexture.width, _worldTexture.height];

        LoadExplorationData();
    }

    void Update()
    {
        PlayerPixelPosition();
        explorationDiameter = Mathf.RoundToInt(initialExplorationRadius * 2);

        if (randomizeArrayData)
        {
            RandomizeArray();
        }
        if (printArrayData)
        {
            PrintArray();
        }
        if (drawArrayData)
        {
            DrawArray();
        }

        if (purgeArrayData)
        {
            PurgeArray();
        }
        if (createExplorationRadiusBoolArray)
        {
            ExplorationRadiusArray();
        }
    }

    public void ExplorationRadiusArray()
    {
        explorationRadiusBoolArray = new bool[explorationDiameter, explorationDiameter];
        FillArray(explorationRadiusBoolArray, "ExplorationRadius");
        SquareToCircle(explorationRadiusBoolArray);
        _gws.PNGFromBoolArray(explorationRadiusBoolArray, "ExplorationRadius");
        createExplorationRadiusBoolArray = false;
    }

    public void SquareToCircle(bool[,] squareArray)
    {
        int width = squareArray.GetLength(0);
        int height = squareArray.GetLength(1);
        float centerX = (float)(width - 1) / 2.0f;
        float centerY = (float)(height - 1) / 2.0f;

        // Set array elements based on distance from the center
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float distanceX = Math.Abs(x - centerX);
                float distanceY = Math.Abs(y - centerY);
                float distance = (float)Math.Sqrt(distanceX * distanceX + distanceY * distanceY);

                // Set to 1 if inside the circle, 0 otherwise
                squareArray[x, y] = distance <= Math.Min(centerX, centerY);
            }
        }
    }

    private void LoadExplorationData()
    {
        Debug.Log("LOADING BINARY DATA");
        // Ensure array dimensions match world texture
        if (
            explorationBoolArray.GetLength(0) != _worldTexture.width
            || explorationBoolArray.GetLength(1) != _worldTexture.height
        )
        {
            Debug.LogError("Boolean array dimensions don't match world texture dimensions.");
            return;
        }

        using (
            FileStream fileStream = new FileStream(
                "Assets/Data/exploration_data.bin",
                FileMode.Open
            )
        )
        {
            byte[] bytes = new byte[explorationBoolArray.Length / 8];
            fileStream.Read(bytes, 0, bytes.Length);

            // Unpack bytes into the boolean array
            int byteIndex = 0;
            int bitIndex = 0;
            for (int y = 0; y < _worldTexture.height; y++)
            {
                for (int x = 0; x < _worldTexture.width; x++)
                {
                    explorationBoolArray[x, y] = (bytes[byteIndex] & (1 << bitIndex)) != 0;

                    bitIndex++;
                    if (bitIndex == 8)
                    {
                        bitIndex = 0;
                        byteIndex++;
                    }
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
        string path = Path.Combine("Assets/Data");

        using (FileStream fileStream = new FileStream(path, FileMode.Create))
        {
            fileStream.Write(bytes, 0, byteCount);
        }

        Debug.Log("Boolean array saved to: " + path);
    }

    private void FillArray(bool[,] passedBooleanArray, string file)
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

    private void RandomizeArrayLCG()
    {
        for (int y = 0; y < _worldTexture.height; y++)
        {
            for (int x = 0; x < _worldTexture.width; x++)
            {
                lcgSeed = (int)((lcgA * lcgSeed + lcgC) % lcgM);

                // Map the result to a boolean value and assign to the array
                explorationBoolArray[x, y] = lcgSeed % 2 == 0;
            }
        }

        SaveArrayData(explorationBoolArray, "Assets/Data/exploration_data.bin"); // Pass the array to SaveArrayData
        randomizeArrayData = false;
    }

    private void RandomizeArrayCrypto()
    {
        byte[] randomBytes = new byte[_worldTexture.width * _worldTexture.height / 8];

        using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }

        int byteIndex = 0;

        for (int y = 0; y < _worldTexture.height; y++)
        {
            for (int x = 0; x < _worldTexture.width; x++)
            {
                // Extract the relevant bit from the byte to get a boolean value
                explorationBoolArray[x, y] = (randomBytes[byteIndex] & (1 << (x % 8))) != 0;

                if (x % 8 == 7)
                {
                    byteIndex++;
                }
            }
        }

        SaveArrayData(explorationBoolArray, "exploration_data.bin");
        randomizeArrayData = false;
    }

    private void RandomizeArraySimple()
    {
        for (int y = 0; y < _worldTexture.height; y++)
        {
            for (int x = 0; x < _worldTexture.width; x++)
            {
                explorationBoolArray[x, y] = UnityEngine.Random.value > 0.5f; // Assign random boolean value (true or false)
            }
        }
        randomizeArrayData = false; // Set randomizeArrayData to false here
        SaveArrayData(explorationBoolArray, "Assets/Data/exploration_data.bin"); // Pass the array to SaveArrayData
    }

    private void RandomizeArray()
    {
        //RandomizeArrayLCG();
        //RandomizeArrayCrypto();
        RandomizeArraySimple();
        randomizeArrayData = false;
        Debug.Log("Randomization completed. randomizeArrayData set to false." + randomizeArrayData);
    }

    private void PurgeArray()
    {
        if (purgeArrayData)
        {
            // Fill the array with zeros
            for (int y = 0; y < _worldTexture.height; y++)
            {
                for (int x = 0; x < _worldTexture.width; x++)
                {
                    explorationBoolArray[x, y] = false;
                }
            }

            SaveArrayData(explorationBoolArray, "Assets/Data/exploration_data.bin"); // Pass the array to SaveArrayData
            purgeArrayData = false;
        }
    }

    private void PrintArray()
    {
        Debug.Log("Printing Boolean Array:");

        for (int y = 0; y < _worldTexture.height; y++)
        {
            string row = "";

            for (int x = 0; x < _worldTexture.width; x++)
            {
                row += explorationBoolArray[x, y] ? "1" : "0";
            }

            Debug.Log(row);
            printArrayData = false;
        }
    }

    private void DrawArray()
    {
        _gws.PNGFromBoolArray(explorationBoolArray, "Exploration_data");
        drawArrayData = false;
    }

    public void PlayerPixelPosition()
    {
        playerTextureCoords = _gws.WorldToTexture(_player.transform.position);
    }
}

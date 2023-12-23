using System.IO;
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
    public bool createExplorationRadiusBoolArray = false;

    public Vector2 playerTextureCoords;
    public float initialExplorationRadius = 1.5f;
    public bool[,] explorationBoolArray;
    public bool[,] explorationRadiusBoolArray;
    public int explorationDiameter;

    private Texture2D _worldTexture;
    private GameObject _player;

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
        if (fillArrayData)
        {
            FillArray();
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
        _gws.PNGFromBoolArray(explorationRadiusBoolArray);
        createExplorationRadiusBoolArray = false;
    }

    public void PlayerPixelPosition()
    {
        playerTextureCoords = _gws.WorldToTexture(_player.transform.position);
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

    public void SaveExplorationData(bool[,] passedBooleanArray, string path)
    {
        Debug.Log("SAVING BINARY DATA");
        // Ensure array dimensions match world texture
        if (
            passedBooleanArray.GetLength(0) != _worldTexture.width
            || passedBooleanArray.GetLength(1) != _worldTexture.height
        )
        {
            Debug.LogError("Boolean array dimensions don't match world texture dimensions.");
            return;
        }

        // Create a byte array to hold the boolean data efficiently
        byte[] bytes = new byte[passedBooleanArray.Length / 8]; // Each byte can hold 8 boolean values

        int byteIndex = 0;
        int bitIndex = 0;
        for (int y = 0; y < _worldTexture.height; y++)
        {
            for (int x = 0; x < _worldTexture.width; x++)
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

        // Save the byte array to a file
        using (FileStream fileStream = new FileStream(path, FileMode.Create))
        {
            fileStream.Write(bytes, 0, bytes.Length);
        }

        Debug.Log("Boolean array saved to: " + path);
    }

    private void RandomizeArray()
    {
        for (int y = 0; y < _worldTexture.height; y++)
        {
            for (int x = 0; x < _worldTexture.width; x++)
            {
                explorationBoolArray[x, y] = Random.value > 0.5f; // Assign random boolean value (true or false)
            }
        }

        SaveExplorationData(explorationBoolArray, "Assets/Data/exploration_data.bin"); // Pass the array to SaveExplorationData
        randomizeArrayData = false;
    }

    private void FillArray()
    {
        if (fillArrayData)
        {
            // Fill the array with ones
            for (int y = 0; y < _worldTexture.height; y++)
            {
                for (int x = 0; x < _worldTexture.width; x++)
                {
                    explorationBoolArray[x, y] = true;
                }
            }

            SaveExplorationData(explorationBoolArray, "Assets/Data/exploration_data.bin"); // Pass the array to SaveExplorationData
            fillArrayData = false;
        }
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

            SaveExplorationData(explorationBoolArray, "Assets/Data/exploration_data.bin"); // Pass the array to SaveExplorationData
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
}

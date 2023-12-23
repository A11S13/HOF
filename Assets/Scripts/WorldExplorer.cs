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

    public float initialExplorationRadius = 1;

    private bool[,] explorationBooleanArray;
    private Texture2D worldTexture;
    private GameObject _player;

    void Start()
    {
        _gws = GetComponent<GWS>();
        _worldGenerator = GetComponent<WorldGenerator>();
        _player = GameObject.FindGameObjectWithTag("Player");
        worldTexture = _gws.worldTexture;
        explorationBooleanArray = new bool[worldTexture.width, worldTexture.height];
        LoadExplorationData();
    }

    void Update()
    {
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
    }

    public void PlayerPixelPosition()
    {
        //float explorationRadius = initialExplorationRadius * gws.scaleFactor;
        //Debug.Log("Exploration Radius:" + explorationRadius);

        // Get player's position in world coordinates
        Vector3 playerPosition = _player.transform.position;
        Debug.Log("Player World Position:" + playerPosition);

        // Calculate player's texture coordinates
        Vector2 playerTextureCoords = _gws.WorldToTexture(playerPosition);
        Debug.Log("Player Texture Coords:" + playerTextureCoords);
    }

    private void LoadExplorationData()
    {
        Debug.Log("LOADING BINARY DATA");
        // Ensure array dimensions match world texture
        if (
            explorationBooleanArray.GetLength(0) != worldTexture.width
            || explorationBooleanArray.GetLength(1) != worldTexture.height
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
            byte[] bytes = new byte[explorationBooleanArray.Length / 8];
            fileStream.Read(bytes, 0, bytes.Length);

            // Unpack bytes into the boolean array
            int byteIndex = 0;
            int bitIndex = 0;
            for (int y = 0; y < worldTexture.height; y++)
            {
                for (int x = 0; x < worldTexture.width; x++)
                {
                    explorationBooleanArray[x, y] = (bytes[byteIndex] & (1 << bitIndex)) != 0;

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
            passedBooleanArray.GetLength(0) != worldTexture.width
            || passedBooleanArray.GetLength(1) != worldTexture.height
        )
        {
            Debug.LogError("Boolean array dimensions don't match world texture dimensions.");
            return;
        }

        // Create a byte array to hold the boolean data efficiently
        byte[] bytes = new byte[passedBooleanArray.Length / 8]; // Each byte can hold 8 boolean values

        int byteIndex = 0;
        int bitIndex = 0;
        for (int y = 0; y < worldTexture.height; y++)
        {
            for (int x = 0; x < worldTexture.width; x++)
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

    private void PrintArray()
    {
        Debug.Log("Printing Boolean Array:");

        for (int y = 0; y < worldTexture.height; y++)
        {
            string row = "";

            for (int x = 0; x < worldTexture.width; x++)
            {
                row += explorationBooleanArray[x, y] ? "1" : "0";
            }

            Debug.Log(row);
            printArrayData = false;
        }
    }

    private void RandomizeArray()
    {
        for (int y = 0; y < worldTexture.height; y++)
        {
            for (int x = 0; x < worldTexture.width; x++)
            {
                explorationBooleanArray[x, y] = Random.value > 0.5f; // Assign random boolean value (true or false)
            }
        }

        SaveExplorationData(explorationBooleanArray, "Assets/Data/exploration_data.bin"); // Pass the array to SaveExplorationData
        randomizeArrayData = false;
    }

    private void FillArray()
    {
        if (fillArrayData)
        {
            // Fill the array with ones
            for (int y = 0; y < worldTexture.height; y++)
            {
                for (int x = 0; x < worldTexture.width; x++)
                {
                    explorationBooleanArray[x, y] = true;
                }
            }

            SaveExplorationData(explorationBooleanArray, "Assets/Data/exploration_data.bin"); // Pass the array to SaveExplorationData
            fillArrayData = false;
        }
    }

    private void PurgeArray()
    {
        if (purgeArrayData)
        {
            // Fill the array with zeros
            for (int y = 0; y < worldTexture.height; y++)
            {
                for (int x = 0; x < worldTexture.width; x++)
                {
                    explorationBooleanArray[x, y] = false;
                }
            }

            SaveExplorationData(explorationBooleanArray, "Assets/Data/exploration_data.bin"); // Pass the array to SaveExplorationData
            purgeArrayData = false;
        }
    }
}

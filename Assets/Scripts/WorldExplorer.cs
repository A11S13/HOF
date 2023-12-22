using System.IO;
using UnityEditor;
using UnityEngine;

public class WorldExplorer : MonoBehaviour
{
    GWS gws;
    WorldGenerator worldGenerator;

    public Texture2D explorationTexture;
    public bool isExploring;
    public bool purgeArrayData;
    public bool fillArrayData;
    public bool randomizeArrayData;
    public bool printArrayData;
    public bool saveExplorationTexture;
    public float initialExplorationRadius = 1;

    private bool[,] worldBooleanArray;
    private Texture2D worldTexture;

    // Start is called before the first frame update
    void Start()
    {
        gws = GetComponent<GWS>();
        worldGenerator = GetComponent<WorldGenerator>();

        worldTexture = gws.worldTexture;

        // Initialize the boolean array
        worldBooleanArray = new bool[worldTexture.width, worldTexture.height];

        // Load exploration data
        LoadExplorationData();
    }

    // Update is called once per frame
    void Update()
    {
        if (isExploring)
        {
            Exploring();
        }

        if (saveExplorationTexture)
        {
            SaveExplorationDataAsTexture("Assets/Data/explorationTexture.png");
        }

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

    public void SaveExplorationDataAsTexture(string path)
    {
        Debug.Log("SAVE TEXTURE OPERATION BEGINS");

                {
            // Create a new texture with the same dimensions as the boolean array
            explorationTexture = new Texture2D(
                worldBooleanArray.GetLength(0),
                worldBooleanArray.GetLength(1),
                TextureFormat.Alpha8,
                false
            );
        }

        // Set each pixel's color based on the boolean array
        for (int y = 0; y < explorationTexture.height; y++)
        {
            for (int x = 0; x < explorationTexture.width; x++)
            {
                // Set pixel color based on boolean value (0 = black, 1 = white)
                Color pixelColor = worldBooleanArray[x, y] ? Color.white : Color.black;
                //Vector2 textureCoords = new Vector2(x, y);
                //gws.WritePixelToTexture(textureCoords, Color.grey);
            }
        }
        /*explorationTexture.Apply();
        byte[] bytes = ImageConversion.EncodeToPNG(explorationTexture);
        File.WriteAllBytes(path, bytes);
        AssetDatabase.Refresh();*/

        Debug.Log("Exploration data saved as texture to: " + path);
        saveExplorationTexture = false;
    }

    private void Exploring()
    {
        float explorationRadius = initialExplorationRadius * gws.scaleFactor;

        // Get player's position in world coordinates
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Vector3 playerPosition = player.transform.position;

        // Calculate texture coordinates without rounding
        Vector2 playerTextureCoords = gws.WorldToTexture(playerPosition);
        //Debug.Log("PLAYER TEXTURE COORDS:" + playerTextureCoords);

        // Calculate texture coordinates within the exploration radius
        Vector2 minTextureCoords = gws.WorldToTexture(
            playerPosition - new Vector3(explorationRadius, 0, explorationRadius)
        );
        Vector2 maxTextureCoords = gws.WorldToTexture(
            playerPosition + new Vector3(explorationRadius, 0, explorationRadius)
        );
        //Debug.Log("TEXTURE EXPLORATION RADIUS:" + minTextureCoords + maxTextureCoords);

        // Round for loop variables as needed
        for (
            int y = Mathf.RoundToInt(minTextureCoords.y);
            y <= Mathf.RoundToInt(maxTextureCoords.y);
            y++
        )
        {
            //Debug.Log("ENTERING MAIN LOOP");

            for (
                int x = Mathf.RoundToInt(minTextureCoords.x);
                x <= Mathf.RoundToInt(maxTextureCoords.x);
                x++
            )
            {
                //Debug.Log("SECOND LOOP");
                // Check if pixel is within texture bounds
                if (x >= 0 && x < worldTexture.width && y >= 0 && y < worldTexture.height)
                {
                    Debug.Log("ENTERING NESTED LOOP");
                    // Check if already explored
                    if (worldBooleanArray[x, y])
                    {
                        Debug.Log("!!!!!!!THE PIXEL IS EXPLORED!!!!!!!!");
                        continue; // Skip to the next pixel
                    }

                    Debug.Log("PROCEEDING ROUNDING OPERATIONS");

                    // Round again for pixel access if needed
                    int roundedX = Mathf.RoundToInt(x);
                    int roundedY = Mathf.RoundToInt(y);

                    // Check pixel alpha and call WorldGenerator method if needed
                    Color pixelColor = worldTexture.GetPixel(roundedX, roundedY); // Use rounded coordinates for pixel access
                    Debug.Log("CHECKING PIXEL FOR TRANSPARENCY");
                    if (!pixelColor.a.Equals(0))
                    {
                        // Call the specific WorldGenerator method here
                        worldGenerator.RandomizeSector(x, y); // Replace "RandomizeSector" with the actual method name

                        // Mark as explored
                        worldBooleanArray[x, y] = true;
                        Debug.Log("TRYING TO PERFORM SAVE TO EXPLORATION DATA");
                        SaveBooleanArray(worldBooleanArray, "Assets/Data/exploration_data.bin");
                    }
                }
            }
        }
    }

    private void PrintArray()
    {
        Debug.Log("Printing Boolean Array:");

        for (int y = 0; y < worldTexture.height; y++)
        {
            string row = "";

            for (int x = 0; x < worldTexture.width; x++)
            {
                row += worldBooleanArray[x, y] ? "1" : "0";
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
                worldBooleanArray[x, y] = Random.value > 0.5f; // Assign random boolean value (true or false)
            }
        }

        SaveBooleanArray(worldBooleanArray, "Assets/Data/exploration_data.bin"); // Pass the array to SaveBooleanArray
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
                    worldBooleanArray[x, y] = true;
                }
            }

            SaveBooleanArray(worldBooleanArray, "Assets/Data/exploration_data.bin"); // Pass the array to SaveBooleanArray
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
                    worldBooleanArray[x, y] = false;
                }
            }

            SaveBooleanArray(worldBooleanArray, "Assets/Data/exploration_data.bin"); // Pass the array to SaveBooleanArray
            purgeArrayData = false;
        }
    }

    private void LoadExplorationData()
    {
        // Load exploration data from file
        using (
            FileStream fileStream = new FileStream(
                "Assets/Data/exploration_data.bin",
                FileMode.Open
            )
        )
        {
            byte[] bytes = new byte[worldBooleanArray.Length / 8];
            fileStream.Read(bytes, 0, bytes.Length);

            // Unpack bytes into the boolean array
            int byteIndex = 0;
            int bitIndex = 0;
            for (int y = 0; y < worldTexture.height; y++)
            {
                for (int x = 0; x < worldTexture.width; x++)
                {
                    worldBooleanArray[x, y] = (bytes[byteIndex] & (1 << bitIndex)) != 0;

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

    public void SaveBooleanArray(bool[,] booleanArray, string path)
    {
        Debug.Log("SAVE OPERATION BEGINS");
        // Ensure array dimensions match world texture
        if (
            booleanArray.GetLength(0) != worldTexture.width
            || booleanArray.GetLength(1) != worldTexture.height
        )
        {
            Debug.LogError("Boolean array dimensions don't match world texture dimensions.");
            return;
        }

        // Create a byte array to hold the boolean data efficiently
        byte[] bytes = new byte[booleanArray.Length / 8]; // Each byte can hold 8 boolean values

        int byteIndex = 0;
        int bitIndex = 0;
        for (int y = 0; y < worldTexture.height; y++)
        {
            for (int x = 0; x < worldTexture.width; x++)
            {
                // Pack booleans into bytes
                if (booleanArray[x, y])
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
}

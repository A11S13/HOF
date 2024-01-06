using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Jobs;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class WorldExplorer : MonoBehaviour
{
    GWS _gws;
    WorldGenerator _worldGenerator;

    public bool createExplorationRadius = false;
    public bool eraseExplorationData = false;
    public bool DrawExploration = true;

    public Vector2 playerTextureCoords;
    public float initialExplorationRadius = 1.5f;
    public bool[,] explorationBoolArray;
    public bool[,] explorationRadiusBoolArray;
    public int explorationDiameter;
    public int RAPPX;
    public int RAPPY;

    private Vector2 lastPlayerTextureCoords;
    private float lastInitialExplorationRadius = 1;
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
        PlayerPixelPosition();
        ExplorationDiameterRounding();
        ExplorationRadiusInitialize();
        lastPlayerTextureCoords = new Vector2(
            Mathf.RoundToInt(playerTextureCoords.x),
            Mathf.RoundToInt(playerTextureCoords.y)
        );
        ArraysCompare();
        UpdateExplorationData();
    }

    void Update()
    {
        PlayerPixelPosition();
        ExplorationDiameterRounding();

        if (eraseExplorationData)
        {
            EraseExplorationData();
        }
        if (createExplorationRadius)
        {
            ExplorationRadiusInitialize();
        }
        if (PPPChanged())
        {
            ArraysCompare();
            UpdateExplorationData();
        }
    }

    public void UpdateExplorationData()
    {
        _worldGenerator.SaveArrayData(explorationBoolArray, "Exploration_data");
        if (DrawExploration)
        {
            _gws.PNGFromBoolArray(explorationBoolArray, "Exploration_data");
            _gws._mapLoader.LoadAndAssignTexture();
        }
    }

    private void LoadExplorationData()
    {
        Debug.Log("LOADING BINARY DATA");
        string filePath = Path.Combine(GWS.fullPath, "Exploration_data");
        //string filePath = Path.Combine(Application.persistentDataPath, "Exploration_data.bin");
        if (File.Exists(filePath))
        {
            using (FileStream fileStream = new FileStream(filePath, FileMode.Open))
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
        else
        {
            Debug.Log("Exploration data not found. Creating empty Exploration data");
            _worldGenerator.PurgeArray(explorationBoolArray, "Exploration_data");
#if UNITY_EDITOR
            AssetDatabase.Refresh();
#endif
        }
    }

    public void EraseExplorationData()
    {
        _worldGenerator.PurgeArray(explorationBoolArray, "Exploration_data");
        _gws.PNGFromBoolArray(explorationBoolArray, "Exploration_data");
        eraseExplorationData = false;
    }

    public void RadiusArrayPixelPosition(int x, int y) //Updates Exploration Array by Exploration Radius
    {
        int ExplorationRadiusCenter = (explorationDiameter - 1) / 2 + 1;
        int shiftX = ExplorationRadiusCenter - x;
        int shiftY = ExplorationRadiusCenter - y;
        int PPPX = Mathf.RoundToInt(playerTextureCoords.x);
        int PPPY = Mathf.RoundToInt(playerTextureCoords.y);
        RAPPX = PPPX - shiftX;
        RAPPY = PPPY - shiftY;

        // Check if RAPPX and RAPPY are within the bounds of _worldTexture
        if (RAPPX >= 0 && RAPPX < _worldTexture.width && RAPPY >= 0 && RAPPY < _worldTexture.height)
        {
            if (!explorationBoolArray[RAPPX, RAPPY]) // is Unexplored
            {
                explorationBoolArray[RAPPX, RAPPY] = true;
            }
        }
    }

    public void ArraysCompare()
    {
        for (int x = 0; x < explorationRadiusBoolArray.GetLength(0); x++)
        {
            for (int y = 0; y < explorationRadiusBoolArray.GetLength(1); y++)
            {
                if (explorationRadiusBoolArray[x, y] && !explorationBoolArray[x, y])
                {
                    RadiusArrayPixelPosition(x, y);
                }
            }
        }
    }

    public void ExplorationRadiusInitialize()
    {
        explorationRadiusBoolArray = new bool[explorationDiameter, explorationDiameter];
        _worldGenerator.FillArray(explorationRadiusBoolArray, "ExplorationRadius");
        SquareToCircle(explorationRadiusBoolArray);
        _worldGenerator.PrintArray(explorationRadiusBoolArray);
        _worldGenerator.DrawArray(explorationRadiusBoolArray, "ExplorationRadius");
        createExplorationRadius = false;
    }

    private void ExplorationDiameterRounding()
    {
        // Check if the initial exploration radius has changed
        if (Mathf.Approximately(initialExplorationRadius, lastInitialExplorationRadius))
        {
            return; // No need to recalculate if the radius hasn't changed
        }

        // Round to the nearest odd integer
        explorationDiameter = Mathf.FloorToInt(initialExplorationRadius * 2);
        if (explorationDiameter % 2 == 0)
        {
            explorationDiameter--; // Make it odd
        }

        // Update the last initial exploration radius
        lastInitialExplorationRadius = initialExplorationRadius;
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

    public bool PPPChanged()
    {
        Vector2 currentPlayerTextureCoords = new Vector2(
            Mathf.RoundToInt(playerTextureCoords.x),
            Mathf.RoundToInt(playerTextureCoords.y)
        );

        if (currentPlayerTextureCoords != lastPlayerTextureCoords)
        {
            lastPlayerTextureCoords = currentPlayerTextureCoords; // Update the last position
            return true;
        }

        return false;
    }

    public void PlayerPixelPosition()
    {
        playerTextureCoords = _gws.WorldToTexture(_player.transform.position);
    }
}

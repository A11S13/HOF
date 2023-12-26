using System.IO;
using UnityEditor;
using UnityEngine;

//created local git repo
public class GWS : MonoBehaviour
{
    public GameObject centralMindPrefab;
    public GameObject asteroidPrefab;

    public Texture2D worldTexture;
    public float scaleFactor;

    public enum GameObjectType
    {
        Default,
        Asteroid,
        CentralMind,
    }

    void Awake()
    {
        // Calculate the scale factor based on the world texture dimensions
        scaleFactor = CalculateScaleFactor();
    }

    void Start()
    {
        ReadAndPlaceGOs();
    }

    float CalculateScaleFactor()
    {
        // Choose a maximum distance from the origin
        float maxDistance = 100000f;

        // Calculate the maximum possible extent of the texture
        float maxTextureExtent = Mathf.Max(worldTexture.width, worldTexture.height);

        // Calculate the scale factor based on the maximum distance and texture extent
        return 2 * maxDistance / maxTextureExtent; // Multiply by 2 to cover both positive and negative directions
    }

    void ReadAndPlaceGOs()
    {
        for (int y = 0; y < worldTexture.height; y++)
        {
            for (int x = 0; x < worldTexture.width; x++)
            {
                Color pixelColor = worldTexture.GetPixel(x, y);

                if (!pixelColor.a.Equals(0))
                {
                    GameObjectType type = DecodeObjectType(pixelColor);
                    GameObject go = Instantiate(
                        GetPrefabForObjectType(type),
                        TextureToWorld(x, y),
                        Quaternion.identity
                    );
                }
            }
        }
    }

    public void PNGFromBoolArray(bool[,] passedBooleanArray, string file)
    {
        // Ensure the boolean array is not null
        if (passedBooleanArray == null)
        {
            Debug.LogError("Boolean array is null.");
            return;
        }

        // Create a new texture with the same dimensions as the boolean array
        Texture2D texture = new Texture2D(
            passedBooleanArray.GetLength(0),
            passedBooleanArray.GetLength(1),
            TextureFormat.RGBA32,
            false
        );

        // Set each pixel's color based on the boolean array
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                Color pixelColor = passedBooleanArray[x, y] ? Color.white : new Color(0, 0, 0, 0); // White for true, transparent for false
                texture.SetPixel(x, y, pixelColor);
            }
        }

        texture.filterMode = FilterMode.Point;

        // Apply changes to the texture
        texture.Apply();

        // Encode the texture to PNG format
        byte[] pngBytes = ImageConversion.EncodeToPNG(texture);

        // Save the PNG file with the specified file name
        string path = $"Assets/Data/{file}.png";
        File.WriteAllBytes(path, pngBytes);

        // Refresh the asset database to ensure changes are reflected
        AssetDatabase.Refresh();

        Debug.Log("Image saved as PNG to: " + path);
    }

    public Vector3 TextureToWorld(int texX, int texY)
    {
        return new Vector3(
            (texX - worldTexture.width / 2) * scaleFactor,
            0,
            (texY - worldTexture.height / 2) * scaleFactor
        );
    }

    public Vector2 WorldToTexture(Vector3 worldPos)
    {
        return new Vector2(
            (worldPos.x / scaleFactor + worldTexture.width / 2f), // Adjust for center and scale
            (worldPos.z / scaleFactor + worldTexture.height / 2f) // Adjust for center, scale, and flip Y
        );
    }

    public void WritePixelToTexture(Vector2 textureCoords, Color pixelColor)
    {
        int texX = Mathf.RoundToInt(textureCoords.x);
        int texY = Mathf.RoundToInt(worldTexture.height - textureCoords.y) - 1; // Adjust Y-coordinate

        if (texX >= 0 && texX < worldTexture.width && texY >= 0 && texY < worldTexture.height)
        {
            worldTexture.SetPixel(texX, texY, pixelColor);
            worldTexture.Apply();
        }
    }

    public void SaveWorldTexture()
    {
        worldTexture.Apply();
        byte[] pngBytes = ImageConversion.EncodeToPNG(worldTexture);
        File.WriteAllBytes(AssetDatabase.GetAssetPath(worldTexture), pngBytes);
        AssetDatabase.Refresh();

        Debug.Log("WorldTexture saved");
    }

    GameObjectType DecodeObjectType(Color pixelColor)
    {
        float tolerance = 0.01f; // Adjust the tolerance based on your needs

        if (
            Mathf.Abs(pixelColor.r - 0.502f) < tolerance
            && Mathf.Abs(pixelColor.g - 0.502f) < tolerance
            && Mathf.Abs(pixelColor.b - 0.502f) < tolerance
        )
        {
            return GameObjectType.Asteroid;
        }
        else if (pixelColor.Equals(Color.black))
        {
            return GameObjectType.CentralMind;
        }
        else
        {
            // Implement decoding logic for other object types
            return GameObjectType.Default;
        }
    }

    GameObject GetPrefabForObjectType(GameObjectType type)
    {
        if (type == GameObjectType.CentralMind)
        {
            return centralMindPrefab;
        }
        if (type == GameObjectType.Asteroid)
        {
            return asteroidPrefab;
        }
        else
        {
            return null;
        }
    }
}

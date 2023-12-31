using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class MapLoader : MonoBehaviour
{
    public RawImage rawImage;
    void Start()
{
    rawImage = GetComponent<RawImage>();
    if (GWS.fullPath != null)
    {
        LoadAndAssignTexture();
    }
    else
    {
        Debug.LogError("GWS reference not assigned. Please assign it in the Inspector.");
    }
}

public void LoadAndAssignTexture()
{
    if (GWS.fullPath == null)
    {
        Debug.LogError("GWS fullPath is null. Ensure it is properly initialized.");
        return;
    }

    string filePath = Path.Combine(GWS.fullPath, "Exploration_data.png");
    Debug.Log(filePath);

    try
    {
        byte[] fileData = File.ReadAllBytes(filePath);

        // Assuming your raw texture data is in the correct format (RGBA32)
        Texture2D texture = new Texture2D(512, 512, TextureFormat.RGBA32, false);
        texture.LoadImage(fileData);
        texture.Apply();

        rawImage.texture = texture;
    }
    catch (Exception e)
    {
        Debug.LogError($"Failed to load texture from {filePath}: {e.Message}");
    }
}

}

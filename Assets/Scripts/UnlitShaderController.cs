using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class UnlitShaderController : MonoBehaviour
{
    public Shader unlitShader;
    public RenderTexture booleanArrayRenderTexture;
    public RawImage rawImage;
    public Text consoleText;

    void Awake()
    {
        consoleText = GameObject.Find("Canvas/console").GetComponent<Text>();
        rawImage = GameObject.Find("Canvas/RDD").GetComponent<RawImage>();
        unlitShader = Shader.Find("Custom/BooleanArrayUnlitShader");
    }

    void Start()
    {
        //booleanArrayRenderTexture = new RenderTexture(512, 512, 0, RenderTextureFormat.ARGBHalf);
        booleanArrayRenderTexture.enableRandomWrite = true;
        //booleanArrayRenderTexture.filterMode = FilterMode.Point;
        //booleanArrayRenderTexture.Create();

        if (booleanArrayRenderTexture != null)
        {
            consoleText.text = consoleText.text + "\nRT Created";
        }
        else
        {
            consoleText.text = consoleText.text + "\nfuck off";
        }

        rawImage.texture = booleanArrayRenderTexture;
        Shader.SetGlobalTexture("_MainTex", booleanArrayRenderTexture);

        // Call RandomDataDraw initially or whenever you want to update the texture
    }

    public void RandomDataDraw()
    {
        UpdateBinaryData();
    }

    void UpdateBinaryData()
    {
        consoleText.text = consoleText.text + "\ni am here";
        string filePath = Path.Combine(GWS.fullPath, "Default_data.bin");
        consoleText.text = consoleText.text + "\nWORKING";
        if (!File.Exists(filePath))
        {
            consoleText.text = consoleText.text + "\nFile NOT Exist";
        }
        else
        {
            consoleText.text = consoleText.text + "\nFile EXIST";
        }
        byte[] binaryBytes = System.IO.File.ReadAllBytes(filePath);

        int textureWidth = 512;
        int textureHeight = (int)Mathf.Ceil(binaryBytes.Length * 8.0f / textureWidth);

        // Create a new texture with the required dimensions
        Texture2D binaryDataTexture = new Texture2D(
            textureWidth,
            textureHeight,
            TextureFormat.RGBA32,
            false
        );

        // Initialize boolean array based on the binary data
        bool[,] booleanArray = new bool[textureWidth, textureHeight];

        for (int i = 0; i < binaryBytes.Length; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                int index = i * 8 + j;
                if (index < textureWidth * textureHeight)
                {
                    booleanArray[index % textureWidth, index / textureWidth] =
                        (binaryBytes[i] & (1 << (7 - j))) != 0;
                }
            }
        }

        // Set each pixel's color based on the boolean array
        Color[] colors = new Color[textureWidth * textureHeight];

        for (int y = 0; y < textureHeight; y++)
        {
            for (int x = 0; x < textureWidth; x++)
            {
                int index = y * textureWidth + x;
                Color pixelColor = booleanArray[x, y] ? Color.white : new Color(0, 0, 0, 0); // White for true, transparent for false
                colors[index] = pixelColor;
            }
        }

        binaryDataTexture.SetPixels(colors);
        binaryDataTexture.filterMode = FilterMode.Point;
        binaryDataTexture.Apply();

        // Use Graphics.Blit to update the render texture
        Graphics.Blit(binaryDataTexture, booleanArrayRenderTexture);
    }
}

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class ComputeShaderController : MonoBehaviour
{
    WorldGenerator _wG;
    public ComputeShader computeShader;
    public RenderTexture booleanArrayRenderTexture;
    public RawImage rawImage;
    public Text consoleText;

    private object fileLock = new object();

    ComputeBuffer binaryBuffer = null;

    void Start()
    {
        _wG = GameObject.Find("GWS").GetComponent<WorldGenerator>();
        consoleText = GameObject.Find("Canvas/console").GetComponent<Text>();
        rawImage = GameObject.Find("Canvas/RDD").GetComponent<RawImage>();
        computeShader = Resources.Load<ComputeShader>("BooleanArrayComputeShader");
        // Find the kernel index by name
        int kernelIndex = computeShader.FindKernel("CSMain");

        // Release existing Render Texture if it exists
        if (booleanArrayRenderTexture != null)
        {
            booleanArrayRenderTexture.Release();
            Destroy(booleanArrayRenderTexture);
        }

        // Create a new Render Texture with resolution 512x512
        booleanArrayRenderTexture = new RenderTexture(512, 512, 0, RenderTextureFormat.ARGB32);
        booleanArrayRenderTexture.enableRandomWrite = true;
        booleanArrayRenderTexture.filterMode = FilterMode.Point; // Ensure point filtering
        booleanArrayRenderTexture.Create();

        // Set the Render Texture to the Compute Shader using the kernel index
        computeShader.SetTexture(kernelIndex, "Result", booleanArrayRenderTexture);

        // Set the initial data
        UpdateBufferData();

        // Dispatch the Compute Shader initially
        DispatchComputeShader(kernelIndex);

        // Assign Render Texture to RawImage
        rawImage.texture = booleanArrayRenderTexture;
    }

    public void RandomDataDraw()
    {
        UpdateBufferData();
        DispatchComputeShader(computeShader.FindKernel("CSMain"));
    }

    void OnDestroy()
    {
        // Release the compute buffer when the script is destroyed
        if (binaryBuffer != null)
            binaryBuffer.Release();
    }

    void UpdateBufferData()
    {
        // Load binary data from file or generate it dynamically
        //string filePath = Path.Combine(GWS.fullPath, "Default_data.bin");
        //byte[] binaryData = System.IO.File.ReadAllBytes(filePath);
        byte[] binaryData = _wG.serializedBollArray;

        // If the buffer is already created, release it before creating a new one
        if (binaryBuffer != null)
        {
            binaryBuffer.Release();
        }
        // Pass binary data to the compute shader
        binaryBuffer = new ComputeBuffer(binaryData.Length / sizeof(uint), sizeof(uint));
        binaryBuffer.SetData(binaryData);
        computeShader.SetBuffer(
            computeShader.FindKernel("CSMain"),
            "BinaryDataBuffer",
            binaryBuffer
        );
    }

    void DispatchComputeShader(int kernelIndex)
    {
        // Dispatch the Compute Shader
        int threadGroupsX = Mathf.CeilToInt(booleanArrayRenderTexture.width / 1.0f);
        int threadGroupsY = Mathf.CeilToInt(booleanArrayRenderTexture.height / 1.0f);
        computeShader.Dispatch(kernelIndex, threadGroupsX, threadGroupsY, 1);
    }
}

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Optimized Morpher handles mesh morphing with improved performance for multiple instances
/// </summary>
public class Morpher : MonoBehaviour
{
    public bool IsDeforming = true;

    [Tooltip("Mesh should be read/write enabled from the model import settings")]
    [SerializeField] private Mesh _oldMesh;
    [Tooltip("Mesh should be read/write enabled from the model import settings")]
    [SerializeField] private Mesh _newMesh;

    [SerializeField] private Material _oldMat;
    [SerializeField] private Material _newMat;

    [SerializeField] private MeshFilter _meshFilter;
    [SerializeField] private Renderer _renderer;

    [Range(0f, 1f)]
    [SerializeField] private float _slider;

    // Cached vertex mapping data - static to share across instances with same meshes
    private static Dictionary<int, int[]> _meshPairToVertexMapping = new Dictionary<int, int[]>();

    // Instance-specific data
    private Vector3[] _oldVertices;
    private Vector3[] _newVertices;
    private int[] _oldTriangles;
    private int[] _newTriangles;
    private Vector3[] _finalVertices;
    private Mesh _interpolatedMesh;
    private int _vertexMapKey;
    private int[] _vertexMapping;
    private Material _finalMaterial;

    // Optimization - caching and reusability
    private bool _isDirty = true;
    private float _lastSliderValue = -1;
    private static readonly int MainTexProperty = Shader.PropertyToID("_MainTex");

    void Start()
    {
        InitializeMeshData();
    }

    private void InitializeMeshData()
    {
        _interpolatedMesh = new Mesh();
        _interpolatedMesh.MarkDynamic();

        if (_meshFilter != null)
        {
            _meshFilter.mesh = _interpolatedMesh;
        }

        _oldVertices = _oldMesh.vertices;
        _newVertices = _newMesh.vertices;
        _oldTriangles = _oldMesh.triangles;
        _newTriangles = _newMesh.triangles;

        // Create a unique key for this mesh pair
        _vertexMapKey = GenerateMeshPairHash(_oldMesh.GetInstanceID(), _newMesh.GetInstanceID());

        // Only compute vertex mappings once per unique mesh pair
        if (!_meshPairToVertexMapping.TryGetValue(_vertexMapKey, out _vertexMapping))
        {
            ComputeVertexMapping();
            _meshPairToVertexMapping[_vertexMapKey] = _vertexMapping;
        }

        _finalVertices = new Vector3[_oldVertices.Length];
        _finalMaterial = new Material(_oldMat);

        // Force first update
        _isDirty = true;
    }

    private int GenerateMeshPairHash(int oldMeshID, int newMeshID)
    {
        return oldMeshID * 100000 + newMeshID;
    }

    /// <summary>
    /// Precompute and cache vertex mappings instead of searching each frame
    /// </summary>
    private void ComputeVertexMapping()
    {
        _vertexMapping = new int[_oldVertices.Length];

        for (int i = 0; i < _oldVertices.Length; i++)
        {
            float minDistance = float.MaxValue;
            int nearestIndex = 0;

            // Find nearest vertex in new mesh
            for (int j = 0; j < _newVertices.Length; j++)
            {
                float distance = Vector3.SqrMagnitude(_oldVertices[i] - _newVertices[j]);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestIndex = j;
                }
            }

            _vertexMapping[i] = nearestIndex;
        }
    }

    void Update()
    {
        if (IsDeforming && (_isDirty || _lastSliderValue != _slider))
        {
            Deform();
            _lastSliderValue = _slider;
            _isDirty = false;
        }
    }

    public void SetSlider(float slider)
    {
        if (_slider != slider)
        {
            _slider = slider;
            _isDirty = true;
        }
    }

    /// <summary>
    /// Optimized deform method using precalculated vertex mappings
    /// </summary>
    private void Deform()
    {
        // Interpolate vertices using the mapping
        for (int i = 0; i < _oldVertices.Length; i++)
        {
            int targetIndex = _vertexMapping[i];
            _finalVertices[i] = Vector3.Lerp(_oldVertices[i], _newVertices[targetIndex], _slider);
        }

        _interpolatedMesh.Clear();
        _interpolatedMesh.SetVertices(_finalVertices);

        // Use appropriate triangles based on morph progress
        _interpolatedMesh.triangles = _slider < 0.5f ? _oldTriangles : _newTriangles;

        // Set appropriate UVs
        if (_slider < 0.5f)
        {
            _interpolatedMesh.uv = _oldMesh.uv;
            _interpolatedMesh.bounds = _oldMesh.bounds;
        }
        else
        {
            _interpolatedMesh.uv = _newMesh.uv;
            _interpolatedMesh.bounds = _newMesh.bounds;
        }

        _interpolatedMesh.RecalculateNormals();

        // Material handling
        _finalMaterial.Lerp(_oldMat, _newMat, _slider);
        _finalMaterial.SetTexture(MainTexProperty,
            _slider < 0.5f ? _oldMat.GetTexture(MainTexProperty) : _newMat.GetTexture(MainTexProperty));

        _renderer.material = _finalMaterial;
    }

    private void OnDestroy()
    {
        // Cleanup 
        if (_interpolatedMesh != null)
        {
            Destroy(_interpolatedMesh);
        }

        if (_finalMaterial != null)
        {
            Destroy(_finalMaterial);
        }
    }
}
using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class Damageable : MonoBehaviour
{
    Health health;
    Shield shield;

    [Header("Dynamic Destruction")]
    public bool canDetachDebris = true;
    public float damageThresholdForDebris = 20f;
    private float _lastDebrisHealth;

    // DATA STORAGE (Lightweight)
    // We store only the triangle lists, not the GameObjects
    private List<int[]> _islandData = new List<int[]>();
    private bool _isAnalysisComplete = false;

    private MeshFilter _mainMeshFilter;
    private MeshRenderer _mainRenderer;
    private Mesh _workingMesh;

    void Awake()
    {
        health = GetComponent<Health>();
        shield = GetComponent<Shield>();
        _mainMeshFilter = GetComponentInChildren<MeshFilter>();
        _mainRenderer = GetComponentInChildren<MeshRenderer>();
    }

    void Start()
    {
        // 1. FILTER: Ignore non-ships or optimized meshes
        if (GetComponent<Artifica>() == null || _mainMeshFilter == null || !_mainMeshFilter.sharedMesh.isReadable)
        {
            canDetachDebris = false;
            return;
        }

        // Clone mesh safely
        _workingMesh = Instantiate(_mainMeshFilter.sharedMesh);
        _mainMeshFilter.mesh = _workingMesh;

        if (canDetachDebris)
        {
            // Run analysis in background, spread over time
            StartCoroutine(AnalyzeGeometrySliced());
        }
    }

    void OnEnable()
    {
        if (health != null) _lastDebrisHealth = health.max;
    }

    public void Damage(float amount)
    {
        if (shield && shield.Absorb(amount)) return;
        health?.Damage(amount);

        // Only detach if analysis is done
        if (canDetachDebris && _isAnalysisComplete && health != null && health.current > 0)
        {
            if (_lastDebrisHealth - health.current >= damageThresholdForDebris)
            {
                DetachRandomPiece();
                _lastDebrisHealth = health.current;
            }
        }
    }

    public void ShatterAllRemaining()
    {
        if (!_isAnalysisComplete) return;

        // Limit loop to prevent freezing if too many pieces remain
        int limit = 50;
        while (_islandData.Count > 0 && limit > 0)
        {
            DetachRandomPiece();
            limit--;
        }
    }

    private void DetachRandomPiece()
    {
        if (_islandData.Count == 0) return;

        int index = Random.Range(0, _islandData.Count);
        int[] triangles = _islandData[index];
        _islandData.RemoveAt(index);

        // 1. Create GameObject (On Demand)
        GameObject debris = new GameObject($"{gameObject.name}_Debris");
        debris.transform.SetPositionAndRotation(_mainMeshFilter.transform.position, _mainMeshFilter.transform.rotation);
        debris.transform.localScale = _mainMeshFilter.transform.lossyScale;

        // 2. Build Mesh
        Mesh debrisMesh = new Mesh();
        Mesh source = _workingMesh;
        debrisMesh.vertices = source.vertices;
        debrisMesh.normals = source.normals;
        debrisMesh.uv = source.uv;
        debrisMesh.triangles = triangles;
        debrisMesh.RecalculateBounds();

        debris.AddComponent<MeshFilter>().mesh = debrisMesh;
        MeshRenderer mr = debris.AddComponent<MeshRenderer>();
        mr.sharedMaterial = _mainRenderer.sharedMaterial;

        // 3. FAST PHYSICS (The Key Optimization)
        Rigidbody rb = debris.AddComponent<Rigidbody>();
        rb.useGravity = false;

        // instead of MeshCollider (Slow), use BoxCollider (Instant)
        BoxCollider box = debris.AddComponent<BoxCollider>();
        // Auto-fit the box to the mesh bounds
        box.center = debrisMesh.bounds.center;
        box.size = debrisMesh.bounds.size;

        // 4. Force
        Vector3 forceDir = (box.bounds.center - transform.position).normalized;
        rb.AddForce(forceDir * Random.Range(2f, 8f), ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * 10f, ForceMode.Impulse);

        // 5. Hide on Main Ship
        HideVertices(triangles);

        Destroy(debris, 8f);
    }

    private void HideVertices(int[] trianglesToHide)
    {
        Vector3[] verts = _workingMesh.vertices;
        bool hasChanges = false;

        // Optimization: Checking every int is faster than reassigning array every time
        for (int i = 0; i < trianglesToHide.Length; i++)
        {
            int vIndex = trianglesToHide[i];
            if (verts[vIndex] != Vector3.zero)
            {
                verts[vIndex] = Vector3.zero;
                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            _workingMesh.vertices = verts;
            // No need to RecalculateBounds on main ship every frame, saves perf
        }
    }

    // --- SLICED ANALYSIS (Fixes 3 FPS on Start) ---
    IEnumerator AnalyzeGeometrySliced()
    {
        Mesh m = _workingMesh;
        int[] tris = m.triangles;
        Vector3[] verts = m.vertices;
        int triangleCount = tris.Length / 3;

        // Dictionary to map position to triangle indices
        Dictionary<Vector3, List<int>> vertPosToTriangles = new Dictionary<Vector3, List<int>>();

        // STEP 1: Build Adjacency Graph (Time Sliced)
        // We process 500 triangles per frame.
        int trianglesPerFrame = 500;
        int processed = 0;

        for (int t = 0; t < triangleCount; t++)
        {
            for (int i = 0; i < 3; i++)
            {
                int vIdx = tris[t * 3 + i];
                Vector3 pos = verts[vIdx];
                if (!vertPosToTriangles.ContainsKey(pos)) vertPosToTriangles[pos] = new List<int>();
                vertPosToTriangles[pos].Add(t);
            }

            processed++;
            if (processed >= trianglesPerFrame)
            {
                processed = 0;
                yield return null; // Wait for next frame
            }
        }

        // STEP 2: Find Islands (Time Sliced)
        bool[] visitedTriangles = new bool[triangleCount];

        for (int t = 0; t < triangleCount; t++)
        {
            if (visitedTriangles[t]) continue;

            List<int> islandTrisIndices = new List<int>();
            Queue<int> queue = new Queue<int>();
            queue.Enqueue(t);
            visitedTriangles[t] = true;

            // Process one whole island at once (usually fast), but yield between islands
            while (queue.Count > 0)
            {
                int currentTri = queue.Dequeue();
                islandTrisIndices.Add(tris[currentTri * 3 + 0]);
                islandTrisIndices.Add(tris[currentTri * 3 + 1]);
                islandTrisIndices.Add(tris[currentTri * 3 + 2]);

                for (int i = 0; i < 3; i++)
                {
                    int vIdx = tris[currentTri * 3 + i];
                    Vector3 pos = verts[vIdx];
                    foreach (int neighborTri in vertPosToTriangles[pos])
                    {
                        if (!visitedTriangles[neighborTri])
                        {
                            visitedTriangles[neighborTri] = true;
                            queue.Enqueue(neighborTri);
                        }
                    }
                }
            }

            if (islandTrisIndices.Count > 12) // Minimum size check
            {
                _islandData.Add(islandTrisIndices.ToArray());
            }

            // Yield between islands to keep FPS smooth
            yield return null;
        }

        _isAnalysisComplete = true;
        // Debug.Log($"[Damageable] Analysis complete. Found {_islandData.Count} debris parts.");
    }
}
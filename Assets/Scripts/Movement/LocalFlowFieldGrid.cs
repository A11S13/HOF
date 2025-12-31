using UnityEngine;
using System.Collections.Generic;

public class LocalFlowFieldGrid
{
    // Grid configuration
    public int gridWidth { get; private set; }
    public int gridHeight { get; private set; }
    public float cellSize { get; private set; }
    public float gridRadius { get; private set; }

    // Grid data
    private FlowCell[,] cells;
    private Vector3 gridCenter;

    // Standard neighbors (8 directions)
    private static readonly Vector2Int[] neighborOffsets = new Vector2Int[]
    {
        new Vector2Int(0, 1),   new Vector2Int(1, 1),   new Vector2Int(1, 0),   new Vector2Int(1, -1),
        new Vector2Int(0, -1),  new Vector2Int(-1, -1), new Vector2Int(-1, 0),  new Vector2Int(-1, 1)
    };

    private static readonly ushort[] neighborCosts = new ushort[] { 10, 14, 10, 14, 10, 14, 10, 14 };

    public LocalFlowFieldGrid(int width, int height, float cellSize)
    {
        this.gridWidth = width;
        this.gridHeight = height;
        this.cellSize = cellSize;
        this.gridRadius = (width * cellSize) * 0.5f;

        cells = new FlowCell[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
                cells[x, y] = new FlowCell(x, y);
        }
    }

    public void Generate(Vector3 shipPosition, Vector3 goalPosition, LayerMask obstacleLayer,
                        float obstacleSampleRadius, float predictionTime,
                        float gradientMultiplier, byte maxGradientCost)
    {
        gridCenter = shipPosition;
        ResetCells();

        // 1. Sample Obstacles (Now with Movement Smearing)
        SampleObstacles(obstacleLayer, obstacleSampleRadius, predictionTime, gradientMultiplier, maxGradientCost);

        // 2. Integration Field
        Vector2Int goalCell = WorldToGrid(goalPosition);
        CreateIntegrationField(goalCell);

        // 3. Flow Field
        CreateFlowField();
    }

    void ResetCells()
    {
        for (int x = 0; x < gridWidth; x++)
            for (int y = 0; y < gridHeight; y++)
                cells[x, y].Reset();
    }

    void SampleObstacles(LayerMask obstacleLayer, float sampleRadius, float predictionTime,
                     float gradientMultiplier, byte maxGradientCost)
    {
        Collider[] obstacles = new Collider[60];
        // Scan wider to catch fast incoming rocks
        float searchRadius = gridRadius + (40f * predictionTime);

        int obstacleCount = Physics.OverlapSphereNonAlloc(gridCenter, searchRadius, obstacles, obstacleLayer);

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                Vector3 cellWorldPos = GridToWorld(x, y);
                float minDistance = float.MaxValue;

                for (int i = 0; i < obstacleCount; i++)
                {
                    if (obstacles[i] == null) continue;

                    Vector3 currentPos = obstacles[i].bounds.center;

                    // Approximate radius using the X extent
                    float obsRadius = obstacles[i].bounds.extents.x;

                    float distToObj = 0f;

                    Rigidbody rb = obstacles[i].attachedRigidbody;
                    if (rb != null && rb.linearVelocity.sqrMagnitude > 1f)
                    {
                        // --- THE FIX ---
                        // Instead of checking just the future point, check the whole segment
                        Vector3 futurePos = currentPos + (rb.linearVelocity * predictionTime);

                        // Calculate distance to the "Time Tube"
                        float distToPath = DistancePointToSegment(cellWorldPos, currentPos, futurePos);
                        distToObj = distToPath - obsRadius;
                    }
                    else
                    {
                        // Static Object
                        distToObj = Vector3.Distance(cellWorldPos, currentPos) - obsRadius;
                    }

                    if (distToObj < minDistance) minDistance = distToObj;
                }

                // Apply Costs
                if (minDistance <= 0)
                {
                    cells[x, y].cost = 255; // Wall
                }
                else
                {
                    float gradientRange = sampleRadius * gradientMultiplier;
                    if (minDistance < gradientRange)
                    {
                        float factor = 1f - (minDistance / gradientRange);
                        factor = factor * factor; // Nonlinear cost (scarier when close)
                        cells[x, y].cost = (byte)(1 + (factor * maxGradientCost));
                    }
                    else
                    {
                        cells[x, y].cost = 1;
                    }
                }
            }
        }
    }

    // --- MATH HELPER ---
    // Calculates the shortest distance from point P to the line segment AB
    float DistancePointToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float magSq = ab.sqrMagnitude;
        if (magSq < 0.001f) return Vector3.Distance(p, a);

        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / magSq);
        Vector3 closestPoint = a + t * ab;
        return Vector3.Distance(p, closestPoint);
    }

    void CreateIntegrationField(Vector2Int goalCell)
    {
        // Clamp goal to grid bounds so we always have a target
        goalCell.x = Mathf.Clamp(goalCell.x, 0, gridWidth - 1);
        goalCell.y = Mathf.Clamp(goalCell.y, 0, gridHeight - 1);

        FlowCell goal = cells[goalCell.x, goalCell.y];
        // Even if goal is inside a wall, we must make it reachable 
        // or the pathfinding stops. We force it to 0.
        goal.bestCost = 0;

        Queue<Vector2Int> openSet = new Queue<Vector2Int>();
        openSet.Enqueue(goalCell);

        while (openSet.Count > 0)
        {
            Vector2Int current = openSet.Dequeue();
            FlowCell currentCell = cells[current.x, current.y];

            for (int i = 0; i < neighborOffsets.Length; i++)
            {
                Vector2Int neighborPos = current + neighborOffsets[i];
                if (!IsValidCell(neighborPos)) continue;

                FlowCell neighbor = cells[neighborPos.x, neighborPos.y];

                // If neighbor is 255 (Wall), we usually skip it.
                // But for "Ejection" logic, walls can have a cost, just very high.
                // Here we stick to 255 = unwalkable for the integration field.
                if (neighbor.cost == 255) continue;

                ushort newCost = (ushort)(currentCell.bestCost + (neighborCosts[i] * neighbor.cost));

                if (newCost < neighbor.bestCost)
                {
                    neighbor.bestCost = newCost;
                    openSet.Enqueue(neighborPos);
                }
            }
        }
    }

    void CreateFlowField()
    {
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                FlowCell cell = cells[x, y];

                // --- FIX 3: EJECTION LOGIC ---
                // If inside a wall (255), we shouldn't return Zero.
                // We should find the nearest non-255 neighbor.
                if (cell.cost == 255)
                {
                    Vector2Int bestExit = new Vector2Int(-1, -1);
                    ushort minExitCost = ushort.MaxValue;

                    for (int i = 0; i < neighborOffsets.Length; i++)
                    {
                        Vector2Int nPos = new Vector2Int(x, y) + neighborOffsets[i];
                        if (IsValidCell(nPos) && cells[nPos.x, nPos.y].cost < 255)
                        {
                            if (cells[nPos.x, nPos.y].bestCost < minExitCost)
                            {
                                minExitCost = cells[nPos.x, nPos.y].bestCost;
                                bestExit = nPos;
                            }
                        }
                    }

                    if (bestExit.x != -1)
                    {
                        // Point towards the exit
                        Vector3 dir = GridToWorld(bestExit.x, bestExit.y) - GridToWorld(x, y);
                        cell.flowDirection = new Vector2(dir.x, dir.z).normalized;
                    }
                    else
                    {
                        // Deep inside wall, no exit found nearby.
                        cell.flowDirection = Vector2.zero;
                    }
                    continue;
                }

                // Normal Flow Logic
                ushort lowestCost = cell.bestCost;
                Vector2Int bestNeighbor = new Vector2Int(x, y);

                for (int i = 0; i < neighborOffsets.Length; i++)
                {
                    Vector2Int neighborPos = new Vector2Int(x, y) + neighborOffsets[i];
                    if (!IsValidCell(neighborPos)) continue;

                    FlowCell neighbor = cells[neighborPos.x, neighborPos.y];
                    if (neighbor.bestCost < lowestCost)
                    {
                        lowestCost = neighbor.bestCost;
                        bestNeighbor = neighborPos;
                    }
                }

                if (bestNeighbor != new Vector2Int(x, y))
                {
                    Vector3 dirWorld = GridToWorld(bestNeighbor.x, bestNeighbor.y) - GridToWorld(x, y);
                    cell.flowDirection = new Vector2(dirWorld.x, dirWorld.z).normalized;
                }
                else
                {
                    cell.flowDirection = Vector2.zero;
                }
            }
        }
    }

    // --- HELPERS ---
    public Vector3 GetFlowDirection(Vector3 worldPosition)
    {
        Vector2Int gridPos = WorldToGrid(worldPosition);
        if (!IsValidCell(gridPos)) return Vector3.zero;

        Vector2 flow = cells[gridPos.x, gridPos.y].flowDirection;
        return new Vector3(flow.x, 0, flow.y);
    }

    public byte GetCost(Vector3 worldPosition)
    {
        Vector2Int gridPos = WorldToGrid(worldPosition);
        if (!IsValidCell(gridPos)) return 255;
        return cells[gridPos.x, gridPos.y].cost;
    }

    Vector3 GridToWorld(int x, int y)
    {
        float worldX = gridCenter.x + (x - gridWidth * 0.5f) * cellSize + cellSize * 0.5f;
        float worldZ = gridCenter.z + (y - gridHeight * 0.5f) * cellSize + cellSize * 0.5f;
        return new Vector3(worldX, gridCenter.y, worldZ);
    }

    Vector2Int WorldToGrid(Vector3 worldPos)
    {
        float localX = worldPos.x - gridCenter.x;
        float localZ = worldPos.z - gridCenter.z;
        int x = Mathf.FloorToInt((localX / cellSize) + gridWidth * 0.5f);
        int y = Mathf.FloorToInt((localZ / cellSize) + gridHeight * 0.5f);
        return new Vector2Int(x, y);
    }

    bool IsValidCell(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < gridWidth && pos.y >= 0 && pos.y < gridHeight;
    }

    public void DrawDebug(bool showGrid, bool showVectors)
    {
        if (cells == null) return;

        // Visual tweaks for debug
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                FlowCell cell = cells[x, y];
                Vector3 worldPos = GridToWorld(x, y);

                if (showGrid)
                {
                    if (cell.cost == 255)
                    {
                        Gizmos.color = new Color(1, 0, 0, 0.3f);
                        Gizmos.DrawCube(worldPos, Vector3.one * cellSize * 0.9f);
                    }
                    else if (cell.cost > 1)
                    {
                        Gizmos.color = Color.Lerp(Color.yellow, Color.red, (cell.cost) / 100f);
                        Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, 0, 0.2f);
                        Gizmos.DrawCube(worldPos, Vector3.one * cellSize * 0.8f);
                    }
                }

                if (showVectors && (x % 2 == 0 && y % 2 == 0)) // Draw fewer vectors
                {
                    if (cell.flowDirection != Vector2.zero)
                    {
                        Vector3 dir = new Vector3(cell.flowDirection.x, 0, cell.flowDirection.y);
                        Gizmos.color = cell.cost == 255 ? Color.magenta : Color.cyan;
                        Gizmos.DrawRay(worldPos, dir * cellSize * 0.6f);
                    }
                }
            }
        }
    }

    private class FlowCell
    {
        public int x, y;
        public byte cost;
        public ushort bestCost;
        public Vector2 flowDirection;
        public FlowCell(int x, int y) { this.x = x; this.y = y; Reset(); }
        public void Reset() { cost = 1; bestCost = ushort.MaxValue; flowDirection = Vector2.zero; }
    }
}

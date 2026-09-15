using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public class LevelGenerator : MonoBehaviour
{
    [Header("Level tile prefabs (map values 1 to 8)")]
    public GameObject outsideCornerPrefab;
    public GameObject outsideWallPrefab;
    public GameObject insideCornerPrefab;
    public GameObject insideWallPrefab;
    public GameObject pelletPrefab;
    public GameObject powerPelletPrefab;
    public GameObject tJunctionPrefab;
    public GameObject ghostExitGatePrefab;

    [Header("Camera")]
    [SerializeField, Min(0f)] private float cameraPadding = 1f;

    private static readonly int[,] LevelMap =
    {
        {1,2,2,2,2,2,2,2,2,2,2,2,2,7},
        {2,5,5,5,5,5,5,5,5,5,5,5,5,4},
        {2,5,3,4,4,3,5,3,4,4,4,3,5,4},
        {2,6,4,0,0,4,5,4,0,0,0,4,5,4},
        {2,5,3,4,4,3,5,3,4,4,4,3,5,3},
        {2,5,5,5,5,5,5,5,5,5,5,5,5,5},
        {2,5,3,4,4,3,5,3,3,5,3,4,4,4},
        {2,5,3,4,4,3,5,4,4,5,3,4,4,3},
        {2,5,5,5,5,5,5,4,4,5,5,5,5,4},
        {1,2,2,2,2,1,5,4,3,4,4,3,0,4},
        {0,0,0,0,0,2,5,4,3,4,4,3,0,3},
        {0,0,0,0,0,2,5,4,4,0,0,0,0,0},
        {0,0,0,0,0,2,5,4,4,0,3,4,4,8},
        {2,2,2,2,2,1,5,3,3,0,4,0,0,0},
        {0,0,0,0,0,0,5,0,0,0,4,0,0,0}
    };

    private readonly Dictionary<int, GameObject> prefabByType = new Dictionary<int, GameObject>();
    private readonly Dictionary<Vector2Int, GameObject> pelletObjects = new Dictionary<Vector2Int, GameObject>();
    private int[,] fullMap;
    private Vector2 tileSize = Vector2.one;

    public static LevelGenerator Instance { get; private set; }
    public int Width => fullMap == null ? 0 : fullMap.GetLength(1);
    public int Height => fullMap == null ? 0 : fullMap.GetLength(0);
    public int RemainingPellets => pelletObjects.Count;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        GenerateLevel();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void GenerateLevel()
    {
        GameObject manualLevel = GameObject.Find("[Level01_Manual]");
        if (manualLevel != null)
        {
            Destroy(manualLevel);
        }

        GameObject previousGeneratedLevel = GameObject.Find("[Level01_Procedural]");
        if (previousGeneratedLevel != null)
        {
            Destroy(previousGeneratedLevel);
        }

        BuildPrefabLookup();
        tileSize = GetTileSize();
        fullMap = CreateFullMap();
        pelletObjects.Clear();

        var levelRoot = new GameObject("[Level01_Procedural]");
        Transform topLeft = CreateContainer("[TopLeft]", levelRoot.transform);
        Transform topRight = CreateContainer("[TopRight]", levelRoot.transform);
        Transform bottomLeft = CreateContainer("[BottomLeft]", levelRoot.transform);
        Transform bottomRight = CreateContainer("[BottomRight]", levelRoot.transform);

        int sourceRows = LevelMap.GetLength(0);
        int sourceColumns = LevelMap.GetLength(1);
        int fullRows = fullMap.GetLength(0);
        int fullColumns = fullMap.GetLength(1);

        for (int row = 0; row < sourceRows; row++)
        {
            for (int column = 0; column < sourceColumns; column++)
            {
                CreateCell(topLeft, row, column, row, column);
                CreateCell(topRight, row, column, row, fullColumns - 1 - column);

                if (row < sourceRows - 1)
                {
                    CreateCell(bottomLeft, row, column, fullRows - 1 - row, column);
                    CreateCell(bottomRight, row, column, fullRows - 1 - row, fullColumns - 1 - column);
                }
            }
        }

        AdjustCamera();
    }

    public bool IsPlayerWalkable(Vector2Int cell)
    {
        if (!IsInside(cell))
        {
            return false;
        }

        int tileType = fullMap[cell.y, cell.x];
        return tileType == 5 || tileType == 6;
    }

    public bool IsGhostWalkable(Vector2Int cell)
    {
        return IsPlayerWalkable(cell);
    }

    public Vector3 CellToWorld(Vector2Int cell, float z = -0.1f)
    {
        return new Vector3(cell.x * tileSize.x, -cell.y * tileSize.y, z);
    }

    public bool TryCollectPellet(Vector2Int cell, out bool isPowerPellet)
    {
        isPowerPellet = false;
        if (!pelletObjects.TryGetValue(cell, out GameObject pellet))
        {
            return false;
        }

        isPowerPellet = fullMap[cell.y, cell.x] == 6;
        pelletObjects.Remove(cell);
        if (pellet != null)
        {
            Destroy(pellet);
        }

        return true;
    }

    public Vector2Int FindNearestWalkable(Vector2Int requestedCell)
    {
        if (IsPlayerWalkable(requestedCell))
        {
            return requestedCell;
        }

        int bestDistance = int.MaxValue;
        Vector2Int bestCell = new Vector2Int(1, 1);
        for (int row = 0; row < Height; row++)
        {
            for (int column = 0; column < Width; column++)
            {
                var candidate = new Vector2Int(column, row);
                if (!IsPlayerWalkable(candidate))
                {
                    continue;
                }

                int distance = Mathf.Abs(candidate.x - requestedCell.x) + Mathf.Abs(candidate.y - requestedCell.y);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestCell = candidate;
                }
            }
        }

        return bestCell;
    }

    public int ShortestPathDistance(Vector2Int start, Vector2Int target)
    {
        if (!IsGhostWalkable(start) || !IsGhostWalkable(target))
        {
            return 100000;
        }

        if (start == target)
        {
            return 0;
        }

        var queue = new Queue<Vector2Int>();
        var distances = new Dictionary<Vector2Int, int>();
        queue.Enqueue(start);
        distances.Add(start, 0);

        Vector2Int[] steps =
        {
            new Vector2Int(0, -1),
            Vector2Int.right,
            new Vector2Int(0, 1),
            Vector2Int.left
        };

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            int nextDistance = distances[current] + 1;
            foreach (Vector2Int step in steps)
            {
                Vector2Int next = current + step;
                if (!IsGhostWalkable(next) || distances.ContainsKey(next))
                {
                    continue;
                }

                if (next == target)
                {
                    return nextDistance;
                }

                distances.Add(next, nextDistance);
                queue.Enqueue(next);
            }
        }

        return 100000;
    }

    private bool IsInside(Vector2Int cell)
    {
        return fullMap != null && cell.x >= 0 && cell.y >= 0 && cell.x < Width && cell.y < Height;
    }

    private void BuildPrefabLookup()
    {
        prefabByType.Clear();
        prefabByType.Add(1, outsideCornerPrefab);
        prefabByType.Add(2, outsideWallPrefab);
        prefabByType.Add(3, insideCornerPrefab);
        prefabByType.Add(4, insideWallPrefab);
        prefabByType.Add(5, pelletPrefab);
        prefabByType.Add(6, powerPelletPrefab);
        prefabByType.Add(7, tJunctionPrefab);
        prefabByType.Add(8, ghostExitGatePrefab);

        foreach (KeyValuePair<int, GameObject> pair in prefabByType)
        {
            if (pair.Value == null)
            {
                throw new InvalidOperationException("Assign the prefab for level map value " + pair.Key + ".");
            }
        }
    }

    private Vector2 GetTileSize()
    {
        SpriteRenderer renderer = outsideWallPrefab.GetComponent<SpriteRenderer>();
        if (renderer == null || renderer.sprite == null)
        {
            throw new InvalidOperationException("OutsideWall prefab needs a SpriteRenderer with a sprite.");
        }

        return renderer.sprite.bounds.size;
    }

    private static Transform CreateContainer(string name, Transform parent)
    {
        var container = new GameObject(name);
        container.transform.SetParent(parent, false);
        return container.transform;
    }

    private void CreateCell(Transform parent, int sourceRow, int sourceColumn, int fullRow, int fullColumn)
    {
        int tileType = LevelMap[sourceRow, sourceColumn];
        if (tileType == 0)
        {
            return;
        }

        float angle = GetRotation(tileType, fullMap, fullRow, fullColumn);
        var cell = new Vector2Int(fullColumn, fullRow);
        Vector3 position = CellToWorld(cell, 0f);
        Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
        GameObject instance = Instantiate(prefabByType[tileType], position, rotation, parent);
        instance.name = string.Format("r{0:00}_c{1:00}_{2}", fullRow, fullColumn, prefabByType[tileType].name);

        if (tileType == 5 || tileType == 6)
        {
            pelletObjects[cell] = instance;
        }
    }

    private static int[,] CreateFullMap()
    {
        int sourceRows = LevelMap.GetLength(0);
        int sourceColumns = LevelMap.GetLength(1);
        int fullRows = sourceRows * 2 - 1;
        int fullColumns = sourceColumns * 2;
        var full = new int[fullRows, fullColumns];

        for (int row = 0; row < sourceRows; row++)
        {
            for (int column = 0; column < sourceColumns; column++)
            {
                int value = LevelMap[row, column];
                full[row, column] = value;
                full[row, fullColumns - 1 - column] = value;

                if (row < sourceRows - 1)
                {
                    full[fullRows - 1 - row, column] = value;
                    full[fullRows - 1 - row, fullColumns - 1 - column] = value;
                }
            }
        }

        return full;
    }

    private static float GetRotation(int tileType, int[,] map, int row, int column)
    {
        if (tileType == 5 || tileType == 6)
        {
            return 0f;
        }

        Direction baseConnections = tileType switch
        {
            1 => Direction.Right | Direction.Down,
            2 => Direction.Left | Direction.Right,
            3 => Direction.Right | Direction.Down,
            4 => Direction.Left | Direction.Right,
            7 => Direction.Left | Direction.Right | Direction.Down,
            8 => Direction.Left | Direction.Right,
            _ => Direction.None
        };

        Direction neighbors = GetStructuralNeighbors(map, row, column);
        int bestQuarterTurns = 0;
        int bestScore = int.MinValue;

        for (int quarterTurns = 0; quarterTurns < 4; quarterTurns++)
        {
            Direction rotated = RotateCounterClockwise(baseConnections, quarterTurns);
            int connected = CountBits(rotated & neighbors);
            int missing = CountBits(rotated & ~neighbors);
            int score = connected * 10 - missing * 30;

            if (score > bestScore)
            {
                bestScore = score;
                bestQuarterTurns = quarterTurns;
            }
        }

        return bestQuarterTurns * 90f;
    }

    private static Direction GetStructuralNeighbors(int[,] map, int row, int column)
    {
        Direction result = Direction.None;
        int rows = map.GetLength(0);
        int columns = map.GetLength(1);

        if (row > 0 && IsStructural(map[row - 1, column])) result |= Direction.Up;
        if (column < columns - 1 && IsStructural(map[row, column + 1])) result |= Direction.Right;
        if (row < rows - 1 && IsStructural(map[row + 1, column])) result |= Direction.Down;
        if (column > 0 && IsStructural(map[row, column - 1])) result |= Direction.Left;

        return result;
    }

    private static bool IsStructural(int tileType)
    {
        return tileType == 1 || tileType == 2 || tileType == 3 || tileType == 4 || tileType == 7 || tileType == 8;
    }

    private static Direction RotateCounterClockwise(Direction directions, int quarterTurns)
    {
        Direction result = directions;
        for (int turn = 0; turn < quarterTurns; turn++)
        {
            Direction rotated = Direction.None;
            if ((result & Direction.Up) != 0) rotated |= Direction.Left;
            if ((result & Direction.Left) != 0) rotated |= Direction.Down;
            if ((result & Direction.Down) != 0) rotated |= Direction.Right;
            if ((result & Direction.Right) != 0) rotated |= Direction.Up;
            result = rotated;
        }

        return result;
    }

    private static int CountBits(Direction directions)
    {
        int value = (int)directions;
        int count = 0;
        while (value != 0)
        {
            count += value & 1;
            value >>= 1;
        }

        return count;
    }

    private void AdjustCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            Debug.LogWarning("Main Camera was not found; dynamic camera sizing was skipped.", this);
            return;
        }

        float totalWidth = Width * tileSize.x;
        float totalHeight = Height * tileSize.y;
        float safeAspect = Mathf.Max(0.01f, camera.aspect);

        camera.orthographic = true;
        camera.orthographicSize = Mathf.Max(totalHeight / 2f, totalWidth / (2f * safeAspect)) + cameraPadding;
        camera.transform.position = new Vector3(
            (totalWidth - tileSize.x) / 2f,
            -(totalHeight - tileSize.y) / 2f,
            camera.transform.position.z);
    }

    [Flags]
    private enum Direction
    {
        None = 0,
        Up = 1,
        Right = 2,
        Down = 4,
        Left = 8
    }
}

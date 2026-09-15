using System;
using System.Collections.Generic;
using UnityEngine;

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

    private const int Rows = 15;
    private const int Columns = 14;
    private const int FullRows = Rows * 2 - 1;
    private const int FullColumns = Columns * 2;

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

    private void Start()
    {
        GenerateLevel();
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
        Vector2 tileSize = GetTileSize();
        int[,] fullMap = CreateFullMap();

        var levelRoot = new GameObject("[Level01_Procedural]");
        Transform topLeft = CreateContainer("[TopLeft]", levelRoot.transform);
        Transform topRight = CreateContainer("[TopRight]", levelRoot.transform);
        Transform bottomLeft = CreateContainer("[BottomLeft]", levelRoot.transform);
        Transform bottomRight = CreateContainer("[BottomRight]", levelRoot.transform);

        for (int row = 0; row < Rows; row++)
        {
            for (int column = 0; column < Columns; column++)
            {
                CreateCell(topLeft, fullMap, row, column, row, column, tileSize);
                CreateCell(topRight, fullMap, row, column, row, FullColumns - 1 - column, tileSize);

                if (row < Rows - 1)
                {
                    CreateCell(bottomLeft, fullMap, row, column, FullRows - 1 - row, column, tileSize);
                    CreateCell(bottomRight, fullMap, row, column, FullRows - 1 - row, FullColumns - 1 - column, tileSize);
                }
            }
        }

        AdjustCamera(tileSize);
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

    private void CreateCell(
        Transform parent,
        int[,] fullMap,
        int sourceRow,
        int sourceColumn,
        int fullRow,
        int fullColumn,
        Vector2 tileSize)
    {
        int tileType = LevelMap[sourceRow, sourceColumn];
        if (tileType == 0)
        {
            return;
        }

        float angle = GetRotation(tileType, fullMap, fullRow, fullColumn);
        Vector3 position = new Vector3(fullColumn * tileSize.x, -fullRow * tileSize.y, 0f);
        Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
        GameObject instance = Instantiate(prefabByType[tileType], position, rotation, parent);
        instance.name = string.Format("r{0:00}_c{1:00}_{2}", fullRow, fullColumn, prefabByType[tileType].name);
    }

    private static int[,] CreateFullMap()
    {
        var full = new int[FullRows, FullColumns];

        for (int row = 0; row < Rows; row++)
        {
            for (int column = 0; column < Columns; column++)
            {
                int value = LevelMap[row, column];
                full[row, column] = value;
                full[row, FullColumns - 1 - column] = value;

                if (row < Rows - 1)
                {
                    full[FullRows - 1 - row, column] = value;
                    full[FullRows - 1 - row, FullColumns - 1 - column] = value;
                }
            }
        }

        return full;
    }

    private static float GetRotation(int tileType, int[,] fullMap, int row, int column)
    {
        if (tileType == 5 || tileType == 6)
        {
            return 0f;
        }

        Direction baseConnections = tileType switch
        {
            1 => Direction.Left | Direction.Down,
            2 => Direction.Left | Direction.Right,
            3 => Direction.Left | Direction.Down,
            4 => Direction.Left | Direction.Right,
            7 => Direction.Left | Direction.Right | Direction.Down,
            8 => Direction.Left | Direction.Right,
            _ => Direction.None
        };

        Direction neighbors = GetStructuralNeighbors(fullMap, row, column);
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

        if (row > 0 && IsStructural(map[row - 1, column])) result |= Direction.Up;
        if (column < FullColumns - 1 && IsStructural(map[row, column + 1])) result |= Direction.Right;
        if (row < FullRows - 1 && IsStructural(map[row + 1, column])) result |= Direction.Down;
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

    private void AdjustCamera(Vector2 tileSize)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            Debug.LogWarning("Main Camera was not found; dynamic camera sizing was skipped.", this);
            return;
        }

        float totalWidth = FullColumns * tileSize.x;
        float totalHeight = FullRows * tileSize.y;
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

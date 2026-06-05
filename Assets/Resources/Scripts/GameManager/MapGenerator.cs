using System;
using System.Collections.Generic;
using Resources.Scripts;
using Resources.Scripts.Drops;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    public static MapGenerator instance;
    
    public bool useProceduralTerrain = true;
    public int minMapSize = 25;
    public int maxMapSize = 40;
    public int minRooms = 8;
    public int maxRooms = 12;
    public int seed = 0;

    public int sizeGridX = 10;
    public int sizeGridY = 10;

    public TileManager[,] grid;
    
    public List<TileData> tileDatas = new List<TileData>();
    public Vector2Int playerSpawnPosition;
    public Vector2Int exitPosition;
    public List<Vector2Int> enemySpawnPositions = new List<Vector2Int>();
    public List<Vector2Int> goldSpawnPositions = new List<Vector2Int>();
    public List<Vector2Int> chestSpawnPositions = new List<Vector2Int>();
    public List<DoorSpawnData> doorSpawnPositions = new List<DoorSpawnData>();
    public List<SecretRoomData> secretRooms = new List<SecretRoomData>();
    public List<HiddenGoldPocketData> hiddenGoldPockets = new List<HiddenGoldPocketData>();
    public List<RectInt> generatedRooms = new List<RectInt>();

    public List<GameObject> enemyPrefabs = new List<GameObject>();
    public GameObject normalChestPrefab;
    public GameObject doorPrefab;
    public GameObject stairsPrefab;
    
    public int maxNormalChestsInMap = 2;
    
    public int maxEnemiesPerRoom = 3;
    public float enemyBlockerChance = 0.35f;
    public int maxBreakableBlockersPerEncounter = 2;
    
    public bool spawnGeneratedContent = true;
    public bool removeScenePlacedEnemiesAndCoins = true;

    private int _nextEnemySpawnIndex;
    private int _nextGoldSpawnIndex;
    private Sprite _hiddenRoomSprite;
    private readonly Dictionary<Vector2Int, List<GameObject>> _hiddenRoomCovers = new Dictionary<Vector2Int, List<GameObject>>();

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        if (useProceduralTerrain)
            CreateProceduralGrid();
        else
            CreateTestGrid();
    }

    private void CreateProceduralGrid()
    {
        NecrodancerTerrainGenerator terrainGenerator = new NecrodancerTerrainGenerator();
        NecrodancerTerrainData terrain = terrainGenerator.Generate(
            minMapSize,
            maxMapSize,
            minRooms,
            maxRooms,
            seed,
            maxEnemiesPerRoom);

        sizeGridX = terrain.Width;
        sizeGridY = terrain.Height;
        playerSpawnPosition = terrain.PlayerSpawnPosition;
        exitPosition = terrain.ExitPosition;
        enemySpawnPositions = new List<Vector2Int>(terrain.EnemyPositions);
        goldSpawnPositions = new List<Vector2Int>(terrain.GoldPositions);
        chestSpawnPositions = new List<Vector2Int>(terrain.ChestPositions);
        doorSpawnPositions = new List<DoorSpawnData>(terrain.DoorPositions);
        secretRooms = new List<SecretRoomData>(terrain.SecretRooms);
        hiddenGoldPockets = new List<HiddenGoldPocketData>(terrain.HiddenGoldPockets);
        generatedRooms = new List<RectInt>(terrain.Rooms);

        CreateGridFromTiles(terrain.Tiles);
        CreateHiddenRoomCovers();
        SpawnGeneratedContent();
        
        EventBus<TerrainGenerated>.Raise(new TerrainGenerated());
    }

    private void CreateTestGrid()
    {
        sizeGridX = Mathf.Max(sizeGridX, 24);
        sizeGridY = Mathf.Max(sizeGridY, 16);

        TileManager.TileType[,] tiles = new TileManager.TileType[sizeGridX, sizeGridY];

        for (int x = 0; x < sizeGridX; x++)
        {
            for (int y = 0; y < sizeGridY; y++)
            {
                TileManager.TileType typeTile = TileManager.TileType.WALKABLE;

                if (x == 0 || x == sizeGridX-1 || y == 0 || y == sizeGridY-1) 
                    typeTile = TileManager.TileType.WALL;

                tiles[x, y] = typeTile;
            }
        }

        AddTestBreakableWalls(tiles);

        playerSpawnPosition = new Vector2Int(3, sizeGridY / 2);
        exitPosition = new Vector2Int(sizeGridX - 3, sizeGridY / 2);
        enemySpawnPositions = GetTestEnemySpawnPositions();
        goldSpawnPositions = new List<Vector2Int>
        {
            new Vector2Int(4, 3),
            new Vector2Int(5, 3),
            new Vector2Int(6, 3),
            new Vector2Int(sizeGridX - 6, sizeGridY - 4),
            new Vector2Int(sizeGridX - 5, sizeGridY - 4)
        };
        chestSpawnPositions = new List<Vector2Int>();
        secretRooms = new List<SecretRoomData>();
        doorSpawnPositions = new List<DoorSpawnData>
        {
            new DoorSpawnData(new Vector2Int(sizeGridX / 2, sizeGridY / 2), true)
        };
        hiddenGoldPockets = new List<HiddenGoldPocketData>();
        generatedRooms = new List<RectInt>
        {
            new RectInt(1, 1, sizeGridX - 2, sizeGridY - 2)
        };
        CreateGridFromTiles(tiles);
        SpawnTestContent();

        EventBus<TerrainGenerated>.Raise(new TerrainGenerated());
    }

    private void AddTestBreakableWalls(TileManager.TileType[,] tiles)
    {
        int centerY = sizeGridY / 2;
        int centerX = sizeGridX / 2;

        for (int y = 3; y < sizeGridY - 3; y++)
        {
            if (y == centerY)
                continue;

            tiles[centerX, y] = TileManager.TileType.BREAKABLEWALL;
        }

        for (int x = centerX + 3; x < sizeGridX - 4; x++)
        {
            if (x % 2 == 0)
                tiles[x, 4] = TileManager.TileType.BREAKABLEWALL;
        }
    }

    private List<Vector2Int> GetTestEnemySpawnPositions()
    {
        List<Vector2Int> positions = new List<Vector2Int>();
        int startX = sizeGridX - 8;
        int startY = 3;

        for (int i = 0; i < enemyPrefabs.Count; i++)
        {
            int row = i / 3;
            int column = i % 3;
            positions.Add(new Vector2Int(startX + column * 2, startY + row * 3));
        }

        return positions;
    }

    private void CreateGridFromTiles(TileManager.TileType[,] tiles)
    {
        grid = new TileManager[sizeGridX, sizeGridY];

        for (int x = 0; x < sizeGridX; x++)
        {
            for (int y = 0; y < sizeGridY; y++)
            {
                TileManager.TileType typeTile = tiles[x, y];

                if (!ShouldCreateTile(tiles, x, y))
                    continue;

                TileData tileData = tileDatas.Find(it => it.tileType == typeTile);

                if (tileData == null || tileData.prefabTile == null)
                {
                    Debug.LogError($"Missing tile prefab for {typeTile}.");
                    continue;
                }

                GameObject tile = Instantiate(tileData.prefabTile, transform);
                tile.transform.position = GetWorldPosition(new Vector2Int(x, y));

                TileManager tileManager = tile.GetComponent<TileManager>();
                tileManager.indexPosition = new Vector2Int(x, y);
                tileManager.SetTileType(typeTile);
                grid[x, y] = tileManager;
            }
        }
    }

    private bool ShouldCreateTile(TileManager.TileType[,] tiles, int x, int y)
    {
        if (tiles[x, y] != TileManager.TileType.WALL)
            return true;

        for (int checkX = x - 1; checkX <= x + 1; checkX++)
        {
            for (int checkY = y - 1; checkY <= y + 1; checkY++)
            {
                if (checkX == x && checkY == y)
                    continue;
                if (checkX < 0 || checkY < 0 || checkX >= sizeGridX || checkY >= sizeGridY)
                    continue;
                if (tiles[checkX, checkY] != TileManager.TileType.WALL)
                    return true;
            }
        }

        return false;
    }
    
    public TileManager GetNextTile(Vector2Int index)
    {
        if (index.x >= sizeGridX ||
            index.y >= sizeGridY ||
            index.x < 0 || index.y < 0 ||
            grid[index.x, index.y] == null ||
            grid[index.x, index.y].tileType ==
            TileManager.TileType.WALL) return null;
        
        return grid[index.x, index.y];
    }

    public Vector2Int GetNextEnemySpawnPosition()
    {
        if (enemySpawnPositions.Count == 0)
            return new Vector2Int(sizeGridX - 5, sizeGridY - 5);

        Vector2Int spawnPosition = enemySpawnPositions[Mathf.Min(_nextEnemySpawnIndex, enemySpawnPositions.Count - 1)];
        _nextEnemySpawnIndex++;
        return spawnPosition;
    }

    public TileManager GetNextGoldSpawnTile()
    {
        while (_nextGoldSpawnIndex < goldSpawnPositions.Count)
        {
            Vector2Int spawnPosition = goldSpawnPositions[_nextGoldSpawnIndex];
            _nextGoldSpawnIndex++;

            TileManager tile = GetNextTile(spawnPosition);

            if (tile != null && tile.CanPlaceGeneratedContent())
                return tile;
        }

        return null;
    }

    public Vector3 GetWorldPosition(Vector2Int index)
    {
        return new Vector3(-sizeGridX / 2f + index.x, sizeGridY / 2f - index.y, 0);
    }

    public void RevealHiddenRoomFromEntrance(Vector2Int entrancePosition)
    {
        if (!_hiddenRoomCovers.TryGetValue(entrancePosition, out List<GameObject> covers))
            return;

        foreach (GameObject cover in covers)
        {
            if (cover != null)
                Destroy(cover);
        }

        List<Vector2Int> revealTiles = new List<Vector2Int>();

        foreach (KeyValuePair<Vector2Int, List<GameObject>> coverByTile in _hiddenRoomCovers)
        {
            if (coverByTile.Value == covers)
                revealTiles.Add(coverByTile.Key);
        }

        foreach (Vector2Int revealTile in revealTiles)
            _hiddenRoomCovers.Remove(revealTile);
    }

    private void CreateHiddenRoomCovers()
    {
        _hiddenRoomCovers.Clear();

        _hiddenRoomSprite ??= CreateBlackSprite();

        if (secretRooms != null)
        {
            foreach (SecretRoomData secretRoom in secretRooms)
            {
                List<GameObject> covers = new List<GameObject>();
                HashSet<Vector2Int> visibleEntranceWalls = GetVisibleSecretRoomWalls(secretRoom);
                HashSet<Vector2Int> visibleCoverTiles = GetVisibleSecretRoomCoverTiles(secretRoom, visibleEntranceWalls);
                RectInt hiddenBounds = new RectInt(
                    secretRoom.Room.xMin - 1,
                    secretRoom.Room.yMin - 1,
                    secretRoom.Room.width + 2,
                    secretRoom.Room.height + 2);

                AddHiddenCoverPieces(hiddenBounds, visibleCoverTiles, covers);
                RegisterHiddenRoomRevealTiles(visibleEntranceWalls, covers);
            }
        }

        CreateHiddenGoldPocketCovers();
    }

    private HashSet<Vector2Int> GetVisibleSecretRoomWalls(SecretRoomData secretRoom)
    {
        HashSet<Vector2Int> visibleWalls = new HashSet<Vector2Int>(secretRoom.EntrancePositions);

        for (int x = secretRoom.Room.xMin - 1; x <= secretRoom.Room.xMax; x++)
        {
            for (int y = secretRoom.Room.yMin - 1; y <= secretRoom.Room.yMax; y++)
            {
                Vector2Int position = new Vector2Int(x, y);
                TileManager tile = GetTile(position);

                if (tile == null || tile.tileType != TileManager.TileType.BREAKABLEWALL)
                    continue;
                if (!HasCardinalNeighborInsideRoom(secretRoom.Room, position))
                    continue;
                if (!HasWalkableNeighborOutsideRoom(secretRoom.Room, position))
                    continue;

                visibleWalls.Add(position);
            }
        }

        return visibleWalls;
    }

    private HashSet<Vector2Int> GetVisibleSecretRoomCoverTiles(
        SecretRoomData secretRoom,
        HashSet<Vector2Int> visibleEntranceWalls)
    {
        HashSet<Vector2Int> visibleTiles = new HashSet<Vector2Int>(visibleEntranceWalls);

        for (int x = secretRoom.Room.xMin - 1; x <= secretRoom.Room.xMax; x++)
        {
            for (int y = secretRoom.Room.yMin - 1; y <= secretRoom.Room.yMax; y++)
            {
                Vector2Int position = new Vector2Int(x, y);
                TileManager tile = GetTile(position);

                if (tile == null || secretRoom.Room.Contains(position))
                    continue;
                if (tile.tileType != TileManager.TileType.WALL &&
                    tile.tileType != TileManager.TileType.BREAKABLEWALL)
                    continue;
                if (!HasWalkableNeighborOutsideRoom(secretRoom.Room, position))
                    continue;

                visibleTiles.Add(position);
            }
        }

        return visibleTiles;
    }


    private void CreateHiddenGoldPocketCovers()
    {
        if (hiddenGoldPockets == null || hiddenGoldPockets.Count == 0)
            return;

        foreach (HiddenGoldPocketData hiddenGoldPocket in hiddenGoldPockets)
        {
            TileManager entranceTile = GetTile(hiddenGoldPocket.CoverEntrancePosition);

            if (entranceTile == null || entranceTile.tileType != TileManager.TileType.BREAKABLEWALL)
                continue;

            List<GameObject> covers = new List<GameObject>();
            AddHiddenCoverRect(new RectInt(hiddenGoldPocket.GoldPosition.x, hiddenGoldPocket.GoldPosition.y, 1, 1), covers);
            _hiddenRoomCovers[hiddenGoldPocket.CoverEntrancePosition] = covers;
        }
    }

    private void RegisterHiddenRoomRevealTiles(HashSet<Vector2Int> visibleEntranceWalls, List<GameObject> covers)
    {
        foreach (Vector2Int position in visibleEntranceWalls)
        {
            TileManager tile = GetTile(position);

            if (tile != null && tile.tileType == TileManager.TileType.BREAKABLEWALL)
                _hiddenRoomCovers[position] = covers;
        }
    }

    private Sprite CreateBlackSprite()
    {
        Texture2D texture = new Texture2D(1, 1)
        {
            filterMode = FilterMode.Point
        };
        texture.SetPixel(0, 0, Color.black);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }

    private void AddHiddenCoverPieces(
        RectInt hiddenBounds,
        HashSet<Vector2Int> visibleCoverTiles,
        List<GameObject> covers)
    {
        for (int x = hiddenBounds.xMin; x < hiddenBounds.xMax; x++)
        {
            for (int y = hiddenBounds.yMin; y < hiddenBounds.yMax; y++)
            {
                Vector2Int position = new Vector2Int(x, y);

                if (IsVisibleCoverTile(position, visibleCoverTiles))
                    continue;

                AddHiddenCoverRect(new RectInt(x, y, 1, 1), covers);
            }
        }
    }

    private bool IsVisibleCoverTile(Vector2Int position, HashSet<Vector2Int> visibleCoverTiles)
    {
        if (!visibleCoverTiles.Contains(position))
            return false;

        TileManager tile = GetTile(position);
        return tile != null &&
               (tile.tileType == TileManager.TileType.WALL ||
                tile.tileType == TileManager.TileType.BREAKABLEWALL);
    }

    private bool HasCardinalNeighborInsideRoom(RectInt room, Vector2Int position)
    {
        foreach (Vector2Int direction in CardinalDirections)
        {
            if (room.Contains(position + direction))
                return true;
        }

        return false;
    }

    private bool HasWalkableNeighborOutsideRoom(RectInt room, Vector2Int position)
    {
        foreach (Vector2Int direction in CardinalDirections)
        {
            Vector2Int neighbor = position + direction;
            TileManager tile = GetTile(neighbor);

            if (room.Contains(neighbor))
                continue;
            if (tile != null && tile.tileType == TileManager.TileType.WALKABLE)
                return true;
        }

        return false;
    }

    private void AddHiddenCoverRect(RectInt rect, List<GameObject> covers)
    {
        if (rect.width <= 0 || rect.height <= 0)
            return;

        GameObject cover = new GameObject("HiddenRoomCover");
        cover.transform.SetParent(transform);

        SpriteRenderer spriteRenderer = cover.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = _hiddenRoomSprite;
        spriteRenderer.color = Color.black;
        spriteRenderer.sortingOrder = 20;

        Vector2Int min = new Vector2Int(rect.xMin, rect.yMin);
        Vector2Int max = new Vector2Int(rect.xMax - 1, rect.yMax - 1);
        Vector3 minPosition = GetWorldPosition(min);
        Vector3 maxPosition = GetWorldPosition(max);

        cover.transform.position = new Vector3(
            (minPosition.x + maxPosition.x) * 0.5f,
            (minPosition.y + maxPosition.y) * 0.5f,
            -0.1f);
        cover.transform.localScale = new Vector3(rect.width, rect.height, 1f);
        covers.Add(cover);
    }

    private TileManager GetTile(Vector2Int index)
    {
        if (index.x < 0 ||
            index.y < 0 ||
            index.x >= sizeGridX ||
            index.y >= sizeGridY)
            return null;

        return grid[index.x, index.y];
    }

    public bool IsClosedDoorBlockingVision(Vector2Int index)
    {
        TileManager tile = GetTile(index);

        return tile?.dropInside is DoorController door && !IsDoorOpen(door);
    }

    public int GetClosedDoorVisionBlockerCount()
    {
        if (grid == null)
            return 0;

        int count = 0;

        for (int x = 0; x < sizeGridX; x++)
        {
            for (int y = 0; y < sizeGridY; y++)
            {
                if (IsClosedDoorBlockingVision(new Vector2Int(x, y)))
                    count++;
            }
        }

        return count;
    }

    private bool IsDoorOpen(DoorController door)
    {
        if (door == null)
            return true;
        if (door.spriteRenderer == null || door.openDoorSprite == null)
            return false;

        return door.spriteRenderer.sprite == door.openDoorSprite;
    }

    private void SpawnGeneratedContent()
    {
        if (!spawnGeneratedContent)
            return;

        EnemyManager[] sceneEnemies = FindObjectsOfType<EnemyManager>(true);
        CoinDropController[] sceneCoins = FindObjectsOfType<CoinDropController>(true);
        ChestController[] sceneChests = FindObjectsOfType<ChestController>(true);
        DoorController[] sceneDoors = FindObjectsOfType<DoorController>(true);
        StairsLevelController[] sceneStairs = FindObjectsOfType<StairsLevelController>(true);

        SpawnDoors();
        SpawnStairs();
        SpawnGold();
        SpawnFixedChests();
        SpawnNormalChests();
        SpawnEnemies();
        CleanupScenePlacedGeneratedContent(sceneEnemies, sceneCoins, sceneChests, sceneDoors, sceneStairs);
    }

    private void SpawnTestContent()
    {
        if (!spawnGeneratedContent)
            return;

        EnemyManager[] sceneEnemies = FindObjectsOfType<EnemyManager>(true);
        CoinDropController[] sceneCoins = FindObjectsOfType<CoinDropController>(true);
        ChestController[] sceneChests = FindObjectsOfType<ChestController>(true);
        DoorController[] sceneDoors = FindObjectsOfType<DoorController>(true);
        StairsLevelController[] sceneStairs = FindObjectsOfType<StairsLevelController>(true);

        SpawnDoors();
        SpawnStairs();
        SpawnGold();
        SpawnFixedChests();
        SpawnTestChests();
        SpawnTestEnemies();
        CleanupScenePlacedGeneratedContent(sceneEnemies, sceneCoins, sceneChests, sceneDoors, sceneStairs);
    }

    private void SpawnDoors()
    {
        if (doorPrefab == null || doorSpawnPositions == null)
            return;

        foreach (DoorSpawnData doorSpawnPosition in doorSpawnPositions)
            SpawnDoor(doorSpawnPosition);
    }

    private void SpawnDoor(DoorSpawnData doorSpawnPosition)
    {
        TileManager tile = GetTile(doorSpawnPosition.Position);

        if (tile == null || !tile.CanPlaceGeneratedContent())
            return;

        GameObject doorObject = Instantiate(doorPrefab, transform);
        DoorController door = doorObject.GetComponent<DoorController>();

        if (door == null)
        {
            Debug.LogWarning($"{doorPrefab.name} does not have a DoorController component.");
            Destroy(doorObject);
            return;
        }

        doorObject.transform.rotation = Quaternion.Euler(0f, 0f, doorSpawnPosition.IsHorizontal ? -90f : 0f);
        door.AssignToTile(tile);
        doorObject.SetActive(true);
    }

    private void SpawnStairs()
    {
        if (stairsPrefab == null)
        {
            Debug.LogWarning("No stairs prefab assigned in MapGenerator.stairsPrefab.");
            return;
        }

        Vector2Int spawnPosition = GetStairsSpawnPosition();
        TileManager tile = GetTile(spawnPosition);

        if (tile == null || !IsValidStairsPosition(spawnPosition))
        {
            Debug.LogWarning($"Could not spawn stairs at {spawnPosition}.");
            return;
        }

        GameObject stairsObject = Instantiate(stairsPrefab, transform);
        StairsLevelController stairs = stairsObject.GetComponent<StairsLevelController>();

        if (stairs == null)
        {
            Debug.LogWarning($"{stairsPrefab.name} does not have a StairsLevelController component.");
            Destroy(stairsObject);
            return;
        }

        stairs.AssignToTile(tile);
        stairsObject.SetActive(true);
    }

    private Vector2Int GetStairsSpawnPosition()
    {
        Vector2Int bestPosition = exitPosition;
        int bestDistance = -1;
        int[,] distances = GetWalkableDistancesFrom(playerSpawnPosition);

        for (int x = 0; x < sizeGridX; x++)
        {
            for (int y = 0; y < sizeGridY; y++)
            {
                Vector2Int candidate = new Vector2Int(x, y);

                if (!IsValidStairsPosition(candidate))
                    continue;

                int distance = distances[x, y];

                if (distance < 0)
                    continue;

                if (distance > bestDistance ||
                    distance == bestDistance &&
                    GetManhattanDistance(playerSpawnPosition, candidate) > GetManhattanDistance(playerSpawnPosition, bestPosition))
                {
                    bestDistance = distance;
                    bestPosition = candidate;
                }
            }
        }

        if (bestDistance >= 0)
            return bestPosition;

        bestDistance = -1;

        for (int x = 0; x < sizeGridX; x++)
        {
            for (int y = 0; y < sizeGridY; y++)
            {
                Vector2Int candidate = new Vector2Int(x, y);

                if (!IsValidStairsPosition(candidate))
                    continue;

                int distance = GetManhattanDistance(playerSpawnPosition, candidate);

                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    bestPosition = candidate;
                }
            }
        }

        return bestPosition;
    }

    private int[,] GetWalkableDistancesFrom(Vector2Int startPosition)
    {
        int[,] distances = new int[sizeGridX, sizeGridY];

        for (int x = 0; x < sizeGridX; x++)
        {
            for (int y = 0; y < sizeGridY; y++)
                distances[x, y] = -1;
        }

        TileManager startTile = GetTile(startPosition);

        if (startTile == null || startTile.tileType != TileManager.TileType.WALKABLE)
            return distances;

        Queue<Vector2Int> pendingTiles = new Queue<Vector2Int>();
        distances[startPosition.x, startPosition.y] = 0;
        pendingTiles.Enqueue(startPosition);

        while (pendingTiles.Count > 0)
        {
            Vector2Int current = pendingTiles.Dequeue();

            foreach (Vector2Int direction in CardinalDirections)
            {
                Vector2Int next = current + direction;
                TileManager nextTile = GetTile(next);

                if (nextTile == null || nextTile.tileType != TileManager.TileType.WALKABLE)
                    continue;
                if (distances[next.x, next.y] >= 0)
                    continue;

                distances[next.x, next.y] = distances[current.x, current.y] + 1;
                pendingTiles.Enqueue(next);
            }
        }

        return distances;
    }

    private RectInt? GetFarthestRoomFromPlayer()
    {
        if (generatedRooms == null || generatedRooms.Count == 0)
            return null;

        RectInt farthestRoom = generatedRooms[0];
        int bestDistance = -1;

        foreach (RectInt room in generatedRooms)
        {
            int distance = GetManhattanDistance(playerSpawnPosition, GetRoomCenter(room));

            if (distance > bestDistance)
            {
                bestDistance = distance;
                farthestRoom = room;
            }
        }

        return farthestRoom;
    }

    private Vector2Int GetRoomCenter(RectInt room)
    {
        return new Vector2Int(room.xMin + room.width / 2, room.yMin + room.height / 2);
    }

    private int GetManhattanDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private bool IsValidStairsPosition(Vector2Int position)
    {
        TileManager tile = GetTile(position);

        return tile != null &&
               tile.CanPlaceGeneratedContent() &&
               position != playerSpawnPosition &&
               !IsReservedForGeneratedContent(position);
    }

    private bool IsReservedForGeneratedContent(Vector2Int position)
    {
        return enemySpawnPositions.Contains(position) ||
               goldSpawnPositions.Contains(position) ||
               chestSpawnPositions.Contains(position) ||
               IsDoorSpawnPosition(position) ||
               IsInsideSecretRoom(position);
    }

    private bool IsDoorSpawnPosition(Vector2Int position)
    {
        if (doorSpawnPositions == null)
            return false;

        foreach (DoorSpawnData doorSpawnPosition in doorSpawnPositions)
        {
            if (doorSpawnPosition.Position == position)
                return true;
        }

        return false;
    }

    private void SpawnTestEnemies()
    {
        for (int i = 0; i < enemyPrefabs.Count && i < enemySpawnPositions.Count; i++)
        {
            if (enemyPrefabs[i] == null)
                continue;

            SpawnEnemy(enemyPrefabs[i], enemySpawnPositions[i]);
        }
    }

    private void SpawnTestChests()
    {
        if (normalChestPrefab == null)
        {
            Debug.LogWarning("No chest prefab assigned in MapGenerator.normalChestPrefab.");
            return;
        }

        Vector2Int[] chestPositions =
        {
            new Vector2Int(sizeGridX / 2 + 2, sizeGridY - 4),
            new Vector2Int(sizeGridX / 2 + 5, sizeGridY - 4)
        };

        int chestsToSpawn = Mathf.Min(maxNormalChestsInMap, chestPositions.Length);

        for (int i = 0; i < chestsToSpawn; i++)
            SpawnChest(normalChestPrefab, chestPositions[i]);
    }

    private void SpawnChest(GameObject chestPrefab, Vector2Int spawnPosition)
    {
        TileManager tile = GetTile(spawnPosition);

        if (tile == null || !tile.CanPlaceGeneratedContent())
            return;

        GameObject chestObject = Instantiate(chestPrefab, transform);
        ChestController chest = chestObject.GetComponent<ChestController>();

        if (chest == null)
        {
            Debug.LogWarning($"{chestPrefab.name} does not have a ChestController component.");
            Destroy(chestObject);
            return;
        }

        chest.AssignToTile(tile);
        chestObject.SetActive(true);
    }

    private void SpawnFixedChests()
    {
        if (normalChestPrefab == null || chestSpawnPositions == null)
            return;

        foreach (Vector2Int chestSpawnPosition in chestSpawnPositions)
            SpawnChest(normalChestPrefab, chestSpawnPosition);
    }

    private void SpawnNormalChests()
    {
        if (normalChestPrefab == null || maxNormalChestsInMap <= 0)
            return;

        List<Vector2Int> chestCandidates = GetNormalChestCandidates();
        System.Random random = new System.Random(seed == 0 ? Environment.TickCount : seed + 15485863);
        Shuffle(chestCandidates, random);

        int chestsToSpawn = Mathf.Min(maxNormalChestsInMap, chestCandidates.Count);

        for (int i = 0; i < chestsToSpawn; i++)
            SpawnNormalChest(chestCandidates[i]);

        if (chestsToSpawn < maxNormalChestsInMap)
            SpawnFallbackNormalChests(maxNormalChestsInMap - chestsToSpawn, chestCandidates);
    }

    private List<Vector2Int> GetNormalChestCandidates()
    {
        List<Vector2Int> candidates = new List<Vector2Int>();

        for (int roomIndex = 0; roomIndex < generatedRooms.Count; roomIndex++)
        {
            RectInt room = generatedRooms[roomIndex];

            for (int x = room.xMin + 1; x < room.xMax - 1; x++)
            {
                for (int y = room.yMin + 1; y < room.yMax - 1; y++)
                {
                    Vector2Int candidate = new Vector2Int(x, y);

                    if (!IsValidNormalChestPosition(candidate))
                        continue;

                    candidates.Add(candidate);
                }
            }
        }

        return candidates;
    }

    private void SpawnFallbackNormalChests(int count, List<Vector2Int> usedPositions)
    {
        if (count <= 0)
            return;

        List<Vector2Int> candidates = new List<Vector2Int>();

        for (int x = 1; x < sizeGridX - 1; x++)
        {
            for (int y = 1; y < sizeGridY - 1; y++)
            {
                Vector2Int candidate = new Vector2Int(x, y);

                if (usedPositions.Contains(candidate))
                    continue;
                if (!IsValidNormalChestPosition(candidate))
                    continue;

                candidates.Add(candidate);
            }
        }

        System.Random random = new System.Random(seed == 0 ? Environment.TickCount + 17 : seed + 32452843);
        Shuffle(candidates, random);

        int chestsToSpawn = Mathf.Min(count, candidates.Count);

        for (int i = 0; i < chestsToSpawn; i++)
            SpawnNormalChest(candidates[i]);
    }

    private bool IsValidNormalChestPosition(Vector2Int position)
    {
        TileManager tile = GetTile(position);

        if (tile == null || !tile.CanPlaceGeneratedContent())
            return false;
        if (position == playerSpawnPosition || position == exitPosition)
            return false;
        if (enemySpawnPositions.Contains(position) ||
            goldSpawnPositions.Contains(position) ||
            chestSpawnPositions.Contains(position))
            return false;
        if (IsInsideSecretRoom(position))
            return false;
        if (CountWalkableNeighborTiles(position) < 2)
            return false;

        return true;
    }

    private void SpawnNormalChest(Vector2Int spawnPosition)
    {
        TileManager tile = GetTile(spawnPosition);

        if (tile == null || !tile.CanPlaceGeneratedContent())
            return;

        SpawnChest(normalChestPrefab, spawnPosition);
    }

    private bool IsInsideSecretRoom(Vector2Int position)
    {
        if (secretRooms == null)
            return false;

        foreach (SecretRoomData secretRoom in secretRooms)
        {
            if (secretRoom.Room.Contains(position))
                return true;
        }

        return false;
    }

    private void SpawnEnemies()
    {
        if (GetValidEnemyPrefabCount() == 0)
        {
            Debug.LogWarning("No enemy prefabs assigned in MapGenerator.enemyPrefabs.");
            return;
        }

        SpawnEnemyEncounters();
    }

    private void SpawnEnemyEncounters()
    {
        System.Random random = new System.Random(seed == 0 ? Environment.TickCount : seed + 7919);
        List<Vector2Int> unassignedPositions = new List<Vector2Int>(enemySpawnPositions);

        for (int roomIndex = 1; roomIndex < generatedRooms.Count; roomIndex++)
        {
            RectInt room = generatedRooms[roomIndex];
            List<Vector2Int> encounterPositions = TakeEnemyPositionsInRoom(room, unassignedPositions, random);

            if (encounterPositions.Count == 0)
                continue;

            SpawnEncounter(roomIndex, room, encounterPositions, random);
        }

        foreach (Vector2Int enemyPosition in unassignedPositions)
            SpawnEnemy(GetCorridorEnemyPrefab(random), enemyPosition);
    }

    private List<Vector2Int> TakeEnemyPositionsInRoom(
        RectInt room,
        List<Vector2Int> availablePositions,
        System.Random random)
    {
        List<Vector2Int> positions = new List<Vector2Int>();

        for (int i = availablePositions.Count - 1; i >= 0; i--)
        {
            Vector2Int position = availablePositions[i];

            if (!room.Contains(position))
                continue;

            positions.Add(position);
            availablePositions.RemoveAt(i);
        }

        Shuffle(positions, random);
        return positions;
    }

    private void SpawnEncounter(int roomIndex, RectInt room, List<Vector2Int> positions, System.Random random)
    {
        float difficulty = GetRoomDifficulty(roomIndex, room);
        PlaceBreakableBlockersForEncounter(room, positions, difficulty, random);

        for (int i = 0; i < positions.Count; i++)
        {
            int enemyTier = ChooseEnemyPrefabTier(difficulty, i, positions.Count, random);
            SpawnEnemy(GetEnemyPrefabByTier(enemyTier), positions[i]);
        }
    }

    private float GetRoomDifficulty(int roomIndex, RectInt room)
    {
        Vector2Int roomCenter = new Vector2Int(room.x + room.width / 2, room.y + room.height / 2);
        float maxDistance = Mathf.Max(sizeGridX, sizeGridY);
        float distanceDifficulty = Mathf.Clamp01(Vector2Int.Distance(playerSpawnPosition, roomCenter) / maxDistance);
        float orderDifficulty = generatedRooms.Count <= 2 ? 0f : roomIndex / (float)(generatedRooms.Count - 1);
        float areaBonus = Mathf.Clamp01((room.width * room.height - 35f) / 45f) * 0.2f;

        return Mathf.Clamp01(Mathf.Max(distanceDifficulty, orderDifficulty) + areaBonus);
    }

    private int ChooseEnemyPrefabTier(float difficulty, int enemySlot, int enemiesInEncounter, System.Random random)
    {
        int prefabCount = GetValidEnemyPrefabCount();

        if (prefabCount <= 1)
            return 0;

        int mainTier = Mathf.Clamp(Mathf.RoundToInt(difficulty * (prefabCount - 1)), 0, prefabCount - 1);

        if (difficulty > 0.55f && enemiesInEncounter >= 2 && random.NextDouble() < 0.35)
            mainTier = Mathf.Min(prefabCount - 1, mainTier + 1);

        if (enemySlot == 0)
            return mainTier;

        int weakestSupportTier = Mathf.Max(0, mainTier - 2);
        int strongSupportTier = Mathf.Max(0, mainTier - 1);

        if (difficulty > 0.65f && enemySlot == 1)
            return strongSupportTier;
        if (random.NextDouble() < 0.35)
            return mainTier;

        return random.Next(weakestSupportTier, strongSupportTier + 1);
    }

    private void PlaceBreakableBlockersForEncounter(
        RectInt room,
        List<Vector2Int> enemyPositions,
        float difficulty,
        System.Random random)
    {
        int blockersPlaced = 0;
        float chance = Mathf.Clamp01(enemyBlockerChance + difficulty * 0.25f);

        foreach (Vector2Int enemyPosition in enemyPositions)
        {
            if (blockersPlaced >= maxBreakableBlockersPerEncounter)
                return;
            if (random.NextDouble() > chance)
                continue;

            List<Vector2Int> candidates = GetBreakableBlockerCandidates(room, enemyPosition, enemyPositions);
            Shuffle(candidates, random);

            foreach (Vector2Int candidate in candidates)
            {
                TileManager tile = GetTile(candidate);

                if (tile == null || !tile.CanPlaceGeneratedContent())
                    continue;

                tile.SetTileType(TileManager.TileType.BREAKABLEWALL);
                blockersPlaced++;
                break;
            }
        }
    }

    private List<Vector2Int> GetBreakableBlockerCandidates(
        RectInt room,
        Vector2Int enemyPosition,
        List<Vector2Int> enemyPositions)
    {
        List<Vector2Int> candidates = new List<Vector2Int>();

        foreach (Vector2Int direction in CardinalDirections)
        {
            Vector2Int candidate = enemyPosition + direction;

            if (!room.Contains(candidate))
                continue;
            if (enemyPositions.Contains(candidate))
                continue;
            if (candidate == playerSpawnPosition || candidate == exitPosition)
                continue;
            if (goldSpawnPositions.Contains(candidate))
                continue;
            if (CountWalkableNeighborTiles(candidate) < 2)
                continue;
            if (CountFreeEnemyMovesAfterBlock(enemyPosition, candidate) < 2)
                continue;

            candidates.Add(candidate);
        }

        return candidates;
    }

    private int CountFreeEnemyMovesAfterBlock(Vector2Int enemyPosition, Vector2Int blockedPosition)
    {
        int freeMoves = 0;

        foreach (Vector2Int direction in CardinalDirections)
        {
            Vector2Int position = enemyPosition + direction;

            if (position == blockedPosition)
                continue;

            TileManager tile = GetTile(position);

            if (tile != null && tile.CanPlaceGeneratedContent())
                freeMoves++;
        }

        return freeMoves;
    }

    private int CountWalkableNeighborTiles(Vector2Int position)
    {
        int neighbors = 0;

        foreach (Vector2Int direction in CardinalDirections)
        {
            TileManager tile = GetTile(position + direction);

            if (tile != null && tile.tileType == TileManager.TileType.WALKABLE)
                neighbors++;
        }

        return neighbors;
    }

    private void SpawnEnemy(GameObject prefab, Vector2Int spawnPosition)
    {
        if (prefab == null)
            return;

        if (spawnPosition == playerSpawnPosition)
            return;

        TileManager tile = GetTile(spawnPosition);

        if (tile == null || !tile.CanPlaceGeneratedContent())
            return;

        GameObject enemyObject = Instantiate(prefab, transform);
        EnemyManager enemy = enemyObject.GetComponent<EnemyManager>();

        if (enemy == null)
        {
            Debug.LogWarning($"{prefab.name} does not have an EnemyManager component.");
            Destroy(enemyObject);
            return;
        }

        enemy.SetSpawnPosition(spawnPosition);
        enemyObject.transform.position = GetWorldPosition(spawnPosition);
        tile.tokenInside = enemy;
        enemyObject.SetActive(true);
    }

    private int GetValidEnemyPrefabCount()
    {
        int count = 0;

        for (int i = 0; i < enemyPrefabs.Count; i++)
        {
            if (enemyPrefabs[i] != null)
                count++;
        }

        return count;
    }

    private GameObject GetEnemyPrefabByTier(int tier)
    {
        int currentTier = 0;

        for (int i = 0; i < enemyPrefabs.Count; i++)
        {
            if (enemyPrefabs[i] == null)
                continue;

            if (currentTier == tier)
                return enemyPrefabs[i];

            currentTier++;
        }

        return null;
    }

    private GameObject GetCorridorEnemyPrefab(System.Random random)
    {
        List<GameObject> corridorPrefabs = new List<GameObject>();
        int maxCorridorEnemyIndex = Mathf.Min(2, enemyPrefabs.Count - 1);

        for (int i = 0; i <= maxCorridorEnemyIndex; i++)
        {
            if (enemyPrefabs[i] != null)
                corridorPrefabs.Add(enemyPrefabs[i]);
        }

        if (corridorPrefabs.Count == 0)
            return null;

        return corridorPrefabs[random.Next(corridorPrefabs.Count)];
    }

    private void Shuffle<T>(List<T> list, System.Random random)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int swapIndex = random.Next(i + 1);
            T temp = list[i];
            list[i] = list[swapIndex];
            list[swapIndex] = temp;
        }
    }

    private static readonly Vector2Int[] CardinalDirections =
    {
        Vector2Int.right,
        Vector2Int.left,
        Vector2Int.up,
        Vector2Int.down
    };

    private void SpawnGold()
    {
        if (GameManager.instance.globalPrefabCoinDrop == null)
        {
            Debug.LogWarning("No coin drop prefab assigned in MapGenerator.");
            return;
        }

        foreach (Vector2Int goldPosition in goldSpawnPositions)
        {
            if (goldPosition == playerSpawnPosition || goldPosition == exitPosition)
                continue;

            TileManager tile = GetNextTile(goldPosition);

            if (tile == null || !tile.CanPlaceGeneratedContent())
                continue;

            GameObject coinObject = Instantiate(GameManager.instance.globalPrefabCoinDrop, transform);
            CoinDropController coinDrop = coinObject.GetComponent<CoinDropController>();

            if (coinDrop == null)
            {
                Debug.LogWarning($"{GameManager.instance.globalPrefabCoinDrop.name} does not have a CoinDropController component.");
                Destroy(coinObject);
                continue;
            }

            coinDrop.AssignToTile(tile);
            coinObject.SetActive(true);
        }
    }

    private void CleanupScenePlacedGeneratedContent(
        EnemyManager[] sceneEnemies,
        CoinDropController[] sceneCoins,
        ChestController[] sceneChests,
        DoorController[] sceneDoors,
        StairsLevelController[] sceneStairs)
    {
        if (!removeScenePlacedEnemiesAndCoins)
            return;

        foreach (EnemyManager enemy in sceneEnemies)
        {
            if (enemy != null)
                Destroy(enemy.gameObject);
        }

        foreach (CoinDropController coin in sceneCoins)
        {
            if (coin == null)
                continue;

            if (coin.gameObject == GameManager.instance.globalPrefabCoinDrop)
                coin.gameObject.SetActive(false);
            else
                Destroy(coin.gameObject);
        }

        foreach (ChestController chest in sceneChests)
        {
            if (chest == null)
                continue;

            if (chest.gameObject == normalChestPrefab)
                chest.gameObject.SetActive(false);
            else
                Destroy(chest.gameObject);
        }

        foreach (DoorController door in sceneDoors)
        {
            if (door == null)
                continue;

            if (door.gameObject == doorPrefab)
                door.gameObject.SetActive(false);
            else
                Destroy(door.gameObject);
        }

        foreach (StairsLevelController stairs in sceneStairs)
        {
            if (stairs == null)
                continue;

            if (stairs.gameObject == stairsPrefab)
                stairs.gameObject.SetActive(false);
            else
                Destroy(stairs.gameObject);
        }
    }
}

[Serializable]
public class TileData
{
    public TileManager.TileType tileType;
    public GameObject prefabTile;
    public Sprite spriteTile;
}

public class TerrainGenerated : IEvent {}

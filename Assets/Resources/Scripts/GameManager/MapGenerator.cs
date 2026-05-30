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
    public List<SecretRoomData> secretRooms = new List<SecretRoomData>();
    public List<RectInt> generatedRooms = new List<RectInt>();

    public List<GameObject> enemyPrefabs = new List<GameObject>();
    public float enemyBlockerChance = 0.35f;
    public int maxBreakableBlockersPerEncounter = 2;
    
    public GameObject coinDropPrefab;
    public bool spawnGeneratedContent = true;
    public bool removeScenePlacedEnemiesAndCoins = true;

    private int _nextEnemySpawnIndex;
    private int _nextGoldSpawnIndex;
    private Sprite _hiddenRoomSprite;
    private readonly Dictionary<Vector2Int, List<GameObject>> _hiddenRoomCovers = new Dictionary<Vector2Int, List<GameObject>>();

    private void Awake()
    {
        instance = this;

        if (useProceduralTerrain)
            CreateProceduralGrid();
        else
            CreateTestGrid();
    }

    private void CreateProceduralGrid()
    {
        NecrodancerTerrainGenerator terrainGenerator = new NecrodancerTerrainGenerator();
        NecrodancerTerrainData terrain = terrainGenerator.Generate(minMapSize, maxMapSize, minRooms, maxRooms, seed);

        sizeGridX = terrain.Width;
        sizeGridY = terrain.Height;
        playerSpawnPosition = terrain.PlayerSpawnPosition;
        exitPosition = terrain.ExitPosition;
        enemySpawnPositions = new List<Vector2Int>(terrain.EnemyPositions);
        goldSpawnPositions = new List<Vector2Int>(terrain.GoldPositions);
        secretRooms = new List<SecretRoomData>(terrain.SecretRooms);
        generatedRooms = new List<RectInt>(terrain.Rooms);

        CreateGridFromTiles(terrain.Tiles);
        CreateHiddenRoomCovers();
        SpawnGeneratedContent();
    }

    private void CreateTestGrid()
    {
        TileManager.TileType[,] tiles = new TileManager.TileType[sizeGridX, sizeGridY];

        for (int x = 0; x < sizeGridX; x++)
        {
            for (int y = 0; y < sizeGridY; y++)
            {
                TileManager.TileType typeTile = TileManager.TileType.WALKABLE;
                
                if (x == 1 || x == sizeGridX - 2 || y == 1 || y == sizeGridY - 2)
                    typeTile = TileManager.TileType.BREAKABLEWALL;
                
                if (x == 0 || x == sizeGridX-1 || y == 0 || y == sizeGridY-1) 
                    typeTile = TileManager.TileType.WALL;

                tiles[x, y] = typeTile;
            }
        }

        playerSpawnPosition = new Vector2Int(sizeGridX / 2, sizeGridY / 2);
        exitPosition = new Vector2Int(sizeGridX - 3, sizeGridY - 3);
        enemySpawnPositions = new List<Vector2Int> { new Vector2Int(sizeGridX - 5, sizeGridY - 5) };
        goldSpawnPositions = new List<Vector2Int> { new Vector2Int(sizeGridX - 7, sizeGridY - 7) };
        secretRooms = new List<SecretRoomData>();
        generatedRooms = new List<RectInt>
        {
            new RectInt(1, 1, sizeGridX - 2, sizeGridY - 2)
        };
        CreateGridFromTiles(tiles);
        SpawnGeneratedContent();
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

            if (tile != null && tile.tileType == TileManager.TileType.WALKABLE && tile.tokenInside == null)
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

        if (secretRooms == null || secretRooms.Count == 0)
            return;

        _hiddenRoomSprite ??= CreateBlackSprite();

        foreach (SecretRoomData secretRoom in secretRooms)
        {
            List<GameObject> covers = new List<GameObject>();
            HashSet<Vector2Int> visibleEntranceWalls = new HashSet<Vector2Int>
            {
                secretRoom.EntrancePosition
            };
            RectInt hiddenBounds = new RectInt(
                secretRoom.Room.xMin - 1,
                secretRoom.Room.yMin - 1,
                secretRoom.Room.width + 2,
                secretRoom.Room.height + 2);

            AddHiddenCoverPieces(hiddenBounds, visibleEntranceWalls, covers);
            RegisterHiddenRoomRevealTiles(visibleEntranceWalls, covers);
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
        HashSet<Vector2Int> visibleEntranceWalls,
        List<GameObject> covers)
    {
        for (int x = hiddenBounds.xMin; x < hiddenBounds.xMax; x++)
        {
            for (int y = hiddenBounds.yMin; y < hiddenBounds.yMax; y++)
            {
                Vector2Int position = new Vector2Int(x, y);

                if (IsVisibleEntranceWall(position, visibleEntranceWalls))
                    continue;

                AddHiddenCoverRect(new RectInt(x, y, 1, 1), covers);
            }
        }
    }

    private bool IsVisibleEntranceWall(Vector2Int position, HashSet<Vector2Int> visibleEntranceWalls)
    {
        if (!visibleEntranceWalls.Contains(position))
            return false;

        TileManager tile = GetTile(position);
        return tile != null && tile.tileType == TileManager.TileType.BREAKABLEWALL;
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

    private void SpawnGeneratedContent()
    {
        if (!spawnGeneratedContent)
            return;

        EnemyManager[] sceneEnemies = FindObjectsOfType<EnemyManager>(true);
        CoinDropController[] sceneCoins = FindObjectsOfType<CoinDropController>(true);

        SpawnGold();
        SpawnEnemies();
        CleanupScenePlacedGeneratedContent(sceneEnemies, sceneCoins);
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
        {
            int enemyTier = ChooseEnemyPrefabTier(0f, 0, 1, random);
            SpawnEnemy(GetEnemyPrefabByTier(enemyTier), enemyPosition);
        }
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

                if (tile == null || tile.tileType != TileManager.TileType.WALKABLE || tile.tokenInside != null)
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

            if (tile != null && tile.tileType == TileManager.TileType.WALKABLE && tile.tokenInside == null)
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

        GameObject enemyObject = Instantiate(prefab, transform);
        EnemyManager enemy = enemyObject.GetComponent<EnemyManager>();

        if (enemy == null)
        {
            Debug.LogWarning($"{prefab.name} does not have an EnemyManager component.");
            Destroy(enemyObject);
            return;
        }

        enemy.SetSpawnPosition(spawnPosition);
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
        if (coinDropPrefab == null)
        {
            Debug.LogWarning("No coin drop prefab assigned in MapGenerator.");
            return;
        }

        if (GameManager.instance != null)
            GameManager.instance.prefabCoinDrop = coinDropPrefab;

        foreach (Vector2Int goldPosition in goldSpawnPositions)
        {
            TileManager tile = GetNextTile(goldPosition);

            if (tile == null || tile.tileType != TileManager.TileType.WALKABLE || tile.tokenInside != null)
                continue;

            GameObject coinObject = Instantiate(coinDropPrefab, transform);
            CoinDropController coinDrop = coinObject.GetComponent<CoinDropController>();

            if (coinDrop == null)
            {
                Debug.LogWarning($"{coinDropPrefab.name} does not have a CoinDropController component.");
                Destroy(coinObject);
                continue;
            }

            coinDrop.AssignToTile(tile);
            coinObject.SetActive(true);
        }
    }

    private void CleanupScenePlacedGeneratedContent(EnemyManager[] sceneEnemies, CoinDropController[] sceneCoins)
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

            if (coin.gameObject == coinDropPrefab)
                coin.gameObject.SetActive(false);
            else
                Destroy(coin.gameObject);
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

using System;
using System.Collections.Generic;
using Resources.Scripts;
using Resources.Scripts.Drops;
using UnityEngine;

public class MapTestGenerator : MonoBehaviour
{
    public static MapTestGenerator instance;
    
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

    public GameObject goblinPrefab;
    public GameObject slimePrefab;
    
    public GameObject coinDropPrefab;
    public bool spawnGeneratedContent = true;
    public bool removeScenePlacedEnemiesAndCoins = true;
    public int maxGoblins = 3;

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
            RectInt hiddenBounds = new RectInt(
                secretRoom.Room.xMin - 1,
                secretRoom.Room.yMin - 1,
                secretRoom.Room.width + 2,
                secretRoom.Room.height + 2);

            AddHiddenCoverPieces(secretRoom.EntrancePosition, hiddenBounds, covers);
            RegisterHiddenRoomRevealTiles(hiddenBounds, covers);
        }
    }

    private void RegisterHiddenRoomRevealTiles(RectInt hiddenBounds, List<GameObject> covers)
    {
        for (int x = hiddenBounds.xMin; x < hiddenBounds.xMax; x++)
        {
            for (int y = hiddenBounds.yMin; y < hiddenBounds.yMax; y++)
            {
                bool isBorder = x == hiddenBounds.xMin ||
                                y == hiddenBounds.yMin ||
                                x == hiddenBounds.xMax - 1 ||
                                y == hiddenBounds.yMax - 1;

                if (!isBorder)
                    continue;

                Vector2Int position = new Vector2Int(x, y);
                TileManager tile = GetTile(position);

                if (tile != null && tile.tileType == TileManager.TileType.BREAKABLEWALL)
                    _hiddenRoomCovers[position] = covers;
            }
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

    private void AddHiddenCoverPieces(Vector2Int entrancePosition, RectInt hiddenBounds, List<GameObject> covers)
    {
        bool entranceOnVerticalEdge = entrancePosition.x == hiddenBounds.xMin ||
                                      entrancePosition.x == hiddenBounds.xMax - 1;

        if (entranceOnVerticalEdge)
        {
            int mainX = entrancePosition.x == hiddenBounds.xMin ? hiddenBounds.xMin + 1 : hiddenBounds.xMin;
            int mainWidth = hiddenBounds.width - 1;
            AddHiddenCoverRect(new RectInt(mainX, hiddenBounds.yMin, mainWidth, hiddenBounds.height), covers);
        }
        else
        {
            int mainY = entrancePosition.y == hiddenBounds.yMin ? hiddenBounds.yMin + 1 : hiddenBounds.yMin;
            int mainHeight = hiddenBounds.height - 1;
            AddHiddenCoverRect(new RectInt(hiddenBounds.xMin, mainY, hiddenBounds.width, mainHeight), covers);
        }
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
        if (slimePrefab == null && goblinPrefab == null)
        {
            Debug.LogWarning("No enemy prefabs assigned in MapTestGenerator.");
            return;
        }

        int spawnedGoblins = 0;
        int goblinBudget = Mathf.Min(maxGoblins, Mathf.Max(1, enemySpawnPositions.Count / 4));

        for (int i = 0; i < enemySpawnPositions.Count; i++)
        {
            bool useGoblin = goblinPrefab != null &&
                             spawnedGoblins < goblinBudget &&
                             i >= 2 &&
                             i % 3 == 0;
            GameObject prefab = useGoblin || slimePrefab == null ? goblinPrefab : slimePrefab;

            if (prefab == null)
                continue;

            GameObject enemyObject = Instantiate(prefab, transform);
            EnemyManager enemy = enemyObject.GetComponent<EnemyManager>();

            if (enemy == null)
            {
                Debug.LogWarning($"{prefab.name} does not have an EnemyManager component.");
                Destroy(enemyObject);
                continue;
            }

            enemy.SetSpawnPosition(enemySpawnPositions[i]);
            enemyObject.SetActive(true);

            if (useGoblin)
                spawnedGoblins++;
        }
    }

    private void SpawnGold()
    {
        if (coinDropPrefab == null)
        {
            Debug.LogWarning("No coin drop prefab assigned in MapTestGenerator.");
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

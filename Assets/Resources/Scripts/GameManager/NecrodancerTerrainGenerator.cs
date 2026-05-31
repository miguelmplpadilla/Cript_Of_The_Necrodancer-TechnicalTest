using System;
using System.Collections.Generic;
using Resources.Scripts;
using UnityEngine;

public sealed class NecrodancerTerrainGenerator
{
    private const int BorderSize = 1;
    private const int SpawnSafeRadius = 2;
    private const int MaxGenerationAttempts = 80;
    private const int PreferredRoomSeparation = 4;
    private const int MinimumRoomSeparation = 2;
    private const double RoomWallBreakableChance = 0.75;

    public NecrodancerTerrainData Generate(
        int minMapSize,
        int maxMapSize,
        int minRooms,
        int maxRooms,
        int seed,
        int maxEnemiesPerRoom = 3)
    {
        minMapSize = Mathf.Clamp(minMapSize, 25, 40);
        maxMapSize = Mathf.Clamp(maxMapSize, minMapSize, 40);
        minRooms = Mathf.Clamp(minRooms, 5, 14);
        maxRooms = Mathf.Clamp(maxRooms, minRooms, 14);
        maxEnemiesPerRoom = Mathf.Clamp(maxEnemiesPerRoom, 1, 8);

        int baseSeed = seed == 0 ? Environment.TickCount : seed;
        NecrodancerTerrainData bestTerrain = null;
        int bestTerrainScore = -1;

        for (int attempt = 0; attempt < MaxGenerationAttempts; attempt++)
        {
            System.Random random = new System.Random(baseSeed + attempt);
            NecrodancerTerrainData terrain = BuildTerrain(
                minMapSize,
                maxMapSize,
                minRooms,
                maxRooms,
                maxEnemiesPerRoom,
                random);

            if (IsValidTerrain(terrain, minRooms, maxRooms))
                return terrain;

            int terrainScore = GetTerrainScore(terrain, minRooms);

            if (terrainScore > bestTerrainScore)
            {
                bestTerrainScore = terrainScore;
                bestTerrain = terrain;
            }
        }

        if (bestTerrain != null)
        {
            Debug.LogWarning("Could not generate a fully valid Necrodancer terrain. Using the best repaired terrain instead.");
            return bestTerrain;
        }

        throw new InvalidOperationException("Could not generate a playable Necrodancer terrain with the current rules.");
    }

    private NecrodancerTerrainData BuildTerrain(
        int minMapSize,
        int maxMapSize,
        int minRooms,
        int maxRooms,
        int maxEnemiesPerRoom,
        System.Random random)
    {
        int width = random.Next(minMapSize, maxMapSize + 1);
        int height = random.Next(minMapSize, maxMapSize + 1);
        int targetRooms = random.Next(minRooms, maxRooms + 1);

        NecrodancerTerrainData terrain = new NecrodancerTerrainData(width, height);
        Fill(terrain.Tiles, TileManager.TileType.WALL);

        CreateRooms(terrain, minRooms, targetRooms, random);
        terrain.PlayerSpawnPosition = GetRoomCenter(terrain.Rooms[0]);
        AddRoomWallLayers(terrain, terrain.Rooms, random);
        ConnectRooms(terrain, random);
        AddIrregularOpenZones(terrain, random);
        AddSecretRooms(terrain, random, random.Next(2, 5), 8);

        if (terrain.SecretRooms.Count == 0)
            AddSecretRooms(terrain, random, 1, 0);

        CarveSpawnSafeZone(terrain);
        terrain.ExitPosition = GetFarthestRoomCenter(terrain.Rooms, terrain.PlayerSpawnPosition);

        PlaceEnemies(terrain, random, maxEnemiesPerRoom);
        EnsureEnemyPosition(terrain);
        PlaceBreakableWallsAndGold(terrain, random);

        return terrain;
    }

    private void CreateRooms(NecrodancerTerrainData terrain, int minRooms, int targetRooms, System.Random random)
    {
        int firstRoomWidth = random.Next(6, 9);
        int firstRoomHeight = random.Next(5, 8);
        RectInt spawnRoom = new RectInt(
            terrain.Width / 2 - firstRoomWidth / 2,
            terrain.Height / 2 - firstRoomHeight / 2,
            firstRoomWidth,
            firstRoomHeight);

        terrain.Rooms.Add(spawnRoom);
        CarveRoom(terrain, spawnRoom);

        for (int attempts = 0; terrain.Rooms.Count < targetRooms && attempts < 900; attempts++)
        {
            int roomWidth = random.Next(5, 11);
            int roomHeight = random.Next(5, 10);
            int x = random.Next(BorderSize + 1, terrain.Width - roomWidth - BorderSize);
            int y = random.Next(BorderSize + 1, terrain.Height - roomHeight - BorderSize);
            RectInt room = new RectInt(x, y, roomWidth, roomHeight);
            int separation = attempts < 650 ? PreferredRoomSeparation : MinimumRoomSeparation;

            if (OverlapsExistingRoom(room, terrain.Rooms, separation))
                continue;

            terrain.Rooms.Add(room);
            CarveRoom(terrain, room);
        }

        AddRequiredRoomsFromGrid(terrain, minRooms);
    }

    private void AddRequiredRoomsFromGrid(NecrodancerTerrainData terrain, int minRooms)
    {
        const int fallbackRoomSize = 5;

        for (int y = BorderSize + 1; y <= terrain.Height - fallbackRoomSize - BorderSize; y++)
        {
            for (int x = BorderSize + 1; x <= terrain.Width - fallbackRoomSize - BorderSize; x++)
            {
                if (terrain.Rooms.Count >= minRooms)
                    return;

                RectInt room = new RectInt(x, y, fallbackRoomSize, fallbackRoomSize);

                if (OverlapsExistingRoom(room, terrain.Rooms, MinimumRoomSeparation))
                    continue;

                terrain.Rooms.Add(room);
                CarveRoom(terrain, room);
            }
        }
    }

    private void ConnectRooms(NecrodancerTerrainData terrain, System.Random random)
    {
        List<RectInt> connectedRooms = new List<RectInt> { terrain.Rooms[0] };
        List<RectInt> remainingRooms = new List<RectInt>(terrain.Rooms);
        remainingRooms.RemoveAt(0);

        while (remainingRooms.Count > 0)
        {
            RectInt fromRoom = connectedRooms[connectedRooms.Count - 1];
            int nearestIndex = GetNearestRoomIndex(GetRoomCenter(fromRoom), remainingRooms);
            RectInt toRoom = remainingRooms[nearestIndex];
            Vector2Int from = GetRoomCenter(fromRoom);
            Vector2Int to = GetRoomCenter(toRoom);
            int corridorWidth = random.NextDouble() < 0.35 ? 2 : 1;
            bool canPlaceDoor = corridorWidth == 1 && random.NextDouble() < 0.55;

            if (random.NextDouble() < 0.5)
            {
                CarveHorizontalCorridor(terrain, from.x, to.x, from.y, corridorWidth);
                CarveVerticalCorridor(terrain, from.y, to.y, to.x, corridorWidth);

                if (canPlaceDoor)
                    TryAddDoorOnCorridor(terrain, from.x, to.x, from.y, true, random);
            }
            else
            {
                CarveVerticalCorridor(terrain, from.y, to.y, from.x, corridorWidth);
                CarveHorizontalCorridor(terrain, from.x, to.x, to.y, corridorWidth);

                if (canPlaceDoor)
                    TryAddDoorOnCorridor(terrain, from.y, to.y, from.x, false, random);
            }

            CarveOpenPatch(terrain, to, 1);
            connectedRooms.Add(toRoom);
            remainingRooms.RemoveAt(nearestIndex);
        }
    }

    private void TryAddDoorOnCorridor(
        NecrodancerTerrainData terrain,
        int from,
        int to,
        int fixedAxis,
        bool isHorizontal,
        System.Random random)
    {
        List<Vector2Int> candidates = new List<Vector2Int>();
        int start = Mathf.Min(from, to);
        int end = Mathf.Max(from, to);

        for (int value = start + 2; value <= end - 2; value++)
        {
            Vector2Int position = isHorizontal
                ? new Vector2Int(value, fixedAxis)
                : new Vector2Int(fixedAxis, value);

            if (!IsValidDoorPosition(terrain, position, isHorizontal))
                continue;

            candidates.Add(position);
        }

        if (candidates.Count == 0)
            return;

        terrain.DoorPositions.Add(new DoorSpawnData(candidates[random.Next(candidates.Count)], isHorizontal));
    }

    private bool IsValidDoorPosition(NecrodancerTerrainData terrain, Vector2Int position, bool isHorizontal)
    {
        if (!IsWalkable(terrain, position))
            return false;
        if (IsInsideAnyRoom(terrain, position))
            return false;
        if (IsOccupiedByImportantPosition(terrain, position))
            return false;

        foreach (DoorSpawnData doorPosition in terrain.DoorPositions)
        {
            if (ManhattanDistance(doorPosition.Position, position) < 5)
                return false;
        }

        Vector2Int forward = isHorizontal ? new Vector2Int(1, 0) : new Vector2Int(0, 1);
        Vector2Int side = isHorizontal ? new Vector2Int(0, 1) : new Vector2Int(1, 0);

        return IsWalkable(terrain, position - forward) &&
               IsWalkable(terrain, position + forward) &&
               !IsWalkable(terrain, position - side) &&
               !IsWalkable(terrain, position + side);
    }

    private void AddIrregularOpenZones(NecrodancerTerrainData terrain, System.Random random)
    {
        int blobCount = random.Next(4, 8);

        for (int i = 0; i < blobCount; i++)
        {
            RectInt room = terrain.Rooms[random.Next(terrain.Rooms.Count)];
            Vector2Int current = new Vector2Int(
                random.Next(room.xMin + 1, room.xMax - 1),
                random.Next(room.yMin + 1, room.yMax - 1));

            int steps = random.Next(5, 13);

            for (int step = 0; step < steps; step++)
            {
                if (room.Contains(current) && IsInsidePlayableBounds(terrain, current))
                    terrain.Tiles[current.x, current.y] = TileManager.TileType.WALKABLE;

                current += GetRandomDirection(random);
            }
        }
    }

    private void CarveSpawnSafeZone(NecrodancerTerrainData terrain)
    {
        for (int x = terrain.PlayerSpawnPosition.x - SpawnSafeRadius; x <= terrain.PlayerSpawnPosition.x + SpawnSafeRadius; x++)
        {
            for (int y = terrain.PlayerSpawnPosition.y - SpawnSafeRadius; y <= terrain.PlayerSpawnPosition.y + SpawnSafeRadius; y++)
            {
                Vector2Int position = new Vector2Int(x, y);

                if (IsInsidePlayableBounds(terrain, position) &&
                    ManhattanDistance(position, terrain.PlayerSpawnPosition) <= SpawnSafeRadius)
                    terrain.Tiles[x, y] = TileManager.TileType.WALKABLE;
            }
        }
    }

    private void PlaceEnemies(NecrodancerTerrainData terrain, System.Random random, int maxEnemiesPerRoom)
    {
        for (int roomIndex = 1; roomIndex < terrain.Rooms.Count; roomIndex++)
        {
            RectInt room = terrain.Rooms[roomIndex];
            int roomArea = room.width * room.height;
            int enemyCount = 1;

            if (roomArea >= 35)
                enemyCount++;
            if (roomArea >= 55 && random.NextDouble() < 0.65)
                enemyCount++;
            if (roomArea >= 75 && random.NextDouble() < 0.35)
                enemyCount++;
            
            enemyCount = Mathf.Min(enemyCount, maxEnemiesPerRoom);

            for (int i = 0; i < enemyCount; i++)
            {
                if (terrain.EnemyPositions.Count >= 28)
                    return;

                Vector2Int? enemyPosition = FindEnemyPositionInRoom(terrain, room, random);

                if (enemyPosition.HasValue)
                    terrain.EnemyPositions.Add(enemyPosition.Value);
            }
        }
    }

    private Vector2Int? FindEnemyPositionInRoom(NecrodancerTerrainData terrain, RectInt room, System.Random random)
    {
        for (int attempt = 0; attempt < 120; attempt++)
        {
            Vector2Int candidate = new Vector2Int(
                random.Next(room.xMin + 1, room.xMax - 1),
                random.Next(room.yMin + 1, room.yMax - 1));

            if (ManhattanDistance(candidate, terrain.PlayerSpawnPosition) <= SpawnSafeRadius + 2)
                continue;
            if (!IsWalkable(terrain, candidate))
                continue;
            if (candidate == terrain.ExitPosition)
                continue;
            if (CountWalkableNeighbors(terrain, candidate, true) < 6)
                continue;
            if (CountReachableWalkableTilesInRadius(terrain, candidate, 2) < 10)
                continue;
            if (!IsFarEnoughFromEnemies(candidate, terrain.EnemyPositions, 2))
                continue;

            return candidate;
        }

        return null;
    }

    private void EnsureEnemyPosition(NecrodancerTerrainData terrain)
    {
        if (terrain.EnemyPositions.Count > 0)
            return;

        Vector2Int? bestPosition = null;
        int bestDistance = -1;

        foreach (RectInt room in terrain.Rooms)
        {
            for (int x = room.xMin + 1; x < room.xMax - 1; x++)
            {
                for (int y = room.yMin + 1; y < room.yMax - 1; y++)
                {
                    Vector2Int position = new Vector2Int(x, y);

                    if (!IsWalkable(terrain, position))
                        continue;
                    if (position == terrain.ExitPosition)
                        continue;
                    if (ManhattanDistance(position, terrain.PlayerSpawnPosition) <= SpawnSafeRadius + 2)
                        continue;
                    if (CountWalkableNeighbors(terrain, position, true) < 6)
                        continue;

                    int distance = ManhattanDistance(position, terrain.PlayerSpawnPosition);

                    if (distance <= bestDistance)
                        continue;

                    bestDistance = distance;
                    bestPosition = position;
                }
            }
        }

        if (bestPosition.HasValue)
            terrain.EnemyPositions.Add(bestPosition.Value);
    }

    private void PlaceBreakableWallsAndGold(NecrodancerTerrainData terrain, System.Random random)
    {
        AddHiddenGoldPockets(terrain, random, random.Next(4, 8));
        EnforceOuterBorderWalls(terrain);
    }

    private void AddBreakableShortcuts(NecrodancerTerrainData terrain, System.Random random, int count)
    {
        List<Vector2Int> candidates = new List<Vector2Int>();

        for (int x = 2; x < terrain.Width - 2; x++)
        {
            for (int y = 2; y < terrain.Height - 2; y++)
            {
                if (terrain.Tiles[x, y] != TileManager.TileType.WALL)
                    continue;

                bool connectsHorizontal = IsWalkable(terrain, new Vector2Int(x - 1, y)) &&
                                          IsWalkable(terrain, new Vector2Int(x + 1, y));
                bool connectsVertical = IsWalkable(terrain, new Vector2Int(x, y - 1)) &&
                                        IsWalkable(terrain, new Vector2Int(x, y + 1));

                if (connectsHorizontal || connectsVertical)
                    candidates.Add(new Vector2Int(x, y));
            }
        }

        Shuffle(candidates, random);

        for (int i = 0; i < candidates.Count && count > 0; i++, count--)
            terrain.Tiles[candidates[i].x, candidates[i].y] = TileManager.TileType.BREAKABLEWALL;
    }

    private void AddHiddenGoldPockets(NecrodancerTerrainData terrain, System.Random random, int count)
    {
        List<Vector2Int> floorCandidates = GetWalkablePositions(terrain);
        Shuffle(floorCandidates, random);

        foreach (Vector2Int floor in floorCandidates)
        {
            if (count <= 0)
                return;
            if (ManhattanDistance(floor, terrain.PlayerSpawnPosition) < 8)
                continue;
            if (IsInsideSecretRoom(terrain, floor))
                continue;

            Vector2Int direction = GetRandomDirection(random);
            Vector2Int breakableWall = floor + direction;
            Vector2Int pocket = breakableWall + direction;

            if (!IsInsidePlayableBounds(terrain, pocket))
                continue;
            if (terrain.Tiles[breakableWall.x, breakableWall.y] != TileManager.TileType.WALL)
                continue;
            if (terrain.Tiles[pocket.x, pocket.y] != TileManager.TileType.WALL)
                continue;
            if (IsInsideSecretRoom(terrain, pocket))
                continue;
            if (CountWalkableNeighbors(terrain, pocket, false) > 0)
                continue;

            terrain.Tiles[breakableWall.x, breakableWall.y] = TileManager.TileType.BREAKABLEWALL;
            terrain.Tiles[pocket.x, pocket.y] = TileManager.TileType.WALKABLE;
            terrain.HiddenGoldPockets.Add(new HiddenGoldPocketData(pocket, breakableWall));
            AddGoldPosition(terrain, pocket);
            count--;
        }
    }

    private void AddGoldNearEnemies(NecrodancerTerrainData terrain, System.Random random)
    {
        List<Vector2Int> enemies = new List<Vector2Int>(terrain.EnemyPositions);
        Shuffle(enemies, random);

        foreach (Vector2Int enemy in enemies)
        {
            if (terrain.GoldPositions.Count >= 10)
                return;

            Vector2Int? position = FindNearbyGoldPosition(terrain, enemy, random);

            if (position.HasValue)
                AddGoldPosition(terrain, position.Value);
        }
    }

    private Vector2Int? FindNearbyGoldPosition(NecrodancerTerrainData terrain, Vector2Int enemy, System.Random random)
    {
        List<Vector2Int> candidates = new List<Vector2Int>();

        for (int x = enemy.x - 2; x <= enemy.x + 2; x++)
        {
            for (int y = enemy.y - 2; y <= enemy.y + 2; y++)
            {
                Vector2Int position = new Vector2Int(x, y);

                if (position == enemy)
                    continue;
                if (!IsWalkable(terrain, position))
                    continue;
                if (ManhattanDistance(position, enemy) > 2)
                    continue;
                if (IsOccupiedByImportantPosition(terrain, position))
                    continue;

                candidates.Add(position);
            }
        }

        if (candidates.Count == 0)
            return null;

        return candidates[random.Next(candidates.Count)];
    }

    private void AddCornerGold(NecrodancerTerrainData terrain, System.Random random, int count)
    {
        List<Vector2Int> candidates = GetWalkablePositions(terrain);
        Shuffle(candidates, random);

        foreach (Vector2Int candidate in candidates)
        {
            if (count <= 0)
                return;
            if (ManhattanDistance(candidate, terrain.PlayerSpawnPosition) < 7)
                continue;
            if (IsInsideSecretRoom(terrain, candidate))
                continue;
            if (IsOccupiedByImportantPosition(terrain, candidate))
                continue;

            int cardinalNeighbors = CountWalkableNeighbors(terrain, candidate, false);

            if (cardinalNeighbors < 2 || cardinalNeighbors > 3)
                continue;

            AddGoldPosition(terrain, candidate);
            count--;
        }
    }

    private bool IsValidTerrain(NecrodancerTerrainData terrain, int minRooms, int maxRooms)
    {
        if (terrain.Rooms.Count < minRooms || terrain.Rooms.Count > maxRooms)
            return false;
        if (terrain.SecretRooms.Count < 1)
            return false;
        if (!IsWalkable(terrain, terrain.PlayerSpawnPosition))
            return false;
        if (!IsWalkable(terrain, terrain.ExitPosition))
            return false;
        if (ManhattanDistance(terrain.PlayerSpawnPosition, terrain.ExitPosition) < Mathf.Min(terrain.Width, terrain.Height) / 2)
            return false;
        if (!AllGameplayTilesReachable(terrain))
            return false;
        if (terrain.EnemyPositions.Count == 0)
            return false;

        foreach (Vector2Int enemyPosition in terrain.EnemyPositions)
        {
            if (ManhattanDistance(enemyPosition, terrain.PlayerSpawnPosition) <= SpawnSafeRadius + 2)
                return false;
            if (CountWalkableNeighbors(terrain, enemyPosition, true) < 6)
                return false;
        }

        return true;
    }

    private int GetTerrainScore(NecrodancerTerrainData terrain, int minRooms)
    {
        int score = Mathf.Min(terrain.Rooms.Count, minRooms) * 10;

        if (terrain.SecretRooms.Count > 0)
            score += 30;
        if (terrain.EnemyPositions.Count > 0)
            score += 20;
        if (IsWalkable(terrain, terrain.PlayerSpawnPosition))
            score += 10;
        if (IsWalkable(terrain, terrain.ExitPosition))
            score += 10;
        if (ManhattanDistance(terrain.PlayerSpawnPosition, terrain.ExitPosition) >= Mathf.Min(terrain.Width, terrain.Height) / 2)
            score += 10;
        if (AllGameplayTilesReachable(terrain))
            score += 40;

        return score;
    }

    private bool AllGameplayTilesReachable(NecrodancerTerrainData terrain)
    {
        bool[,] visited = new bool[terrain.Width, terrain.Height];
        Queue<Vector2Int> pending = new Queue<Vector2Int>();
        pending.Enqueue(terrain.PlayerSpawnPosition);
        visited[terrain.PlayerSpawnPosition.x, terrain.PlayerSpawnPosition.y] = true;

        while (pending.Count > 0)
        {
            Vector2Int current = pending.Dequeue();

            foreach (Vector2Int direction in CardinalDirections)
            {
                Vector2Int next = current + direction;

                if (!IsInsideBounds(terrain, next) || visited[next.x, next.y])
                    continue;
                if (terrain.Tiles[next.x, next.y] == TileManager.TileType.WALL)
                    continue;

                visited[next.x, next.y] = true;
                pending.Enqueue(next);
            }
        }

        for (int x = 0; x < terrain.Width; x++)
        {
            for (int y = 0; y < terrain.Height; y++)
            {
                if (terrain.Tiles[x, y] != TileManager.TileType.WALL && !visited[x, y])
                    return false;
            }
        }

        return true;
    }

    private void CarveRoom(NecrodancerTerrainData terrain, RectInt room)
    {
        for (int x = room.xMin; x < room.xMax; x++)
        {
            for (int y = room.yMin; y < room.yMax; y++)
                terrain.Tiles[x, y] = TileManager.TileType.WALKABLE;
        }
    }

    private void AddSecretRooms(
        NecrodancerTerrainData terrain,
        System.Random random,
        int targetSecretRooms,
        int minDistanceFromSpawn)
    {
        List<Vector2Int> floors = GetWalkablePositions(terrain);
        Shuffle(floors, random);

        foreach (Vector2Int floor in floors)
        {
            if (terrain.SecretRooms.Count >= targetSecretRooms)
                return;
            if (ManhattanDistance(floor, terrain.PlayerSpawnPosition) < minDistanceFromSpawn)
                continue;

            List<Vector2Int> directions = new List<Vector2Int>(CardinalDirections);
            Shuffle(directions, random);

            foreach (Vector2Int direction in directions)
            {
                RectInt? room = TryCreateSecretRoomAt(terrain, floor, direction, random);

                if (!room.HasValue)
                    continue;

                List<Vector2Int> entrancePositions = GetSecretRoomEntrancePositions(terrain, room.Value);

                foreach (Vector2Int entrancePosition in entrancePositions)
                    terrain.Tiles[entrancePosition.x, entrancePosition.y] = TileManager.TileType.BREAKABLEWALL;

                terrain.SecretRooms.Add(new SecretRoomData(room.Value, entrancePositions));
                CarveRoom(terrain, room.Value);
                AddRoomWallLayers(terrain, room.Value, random, entrancePositions);
                break;
            }
        }
    }

    private RectInt? TryCreateSecretRoomAt(
        NecrodancerTerrainData terrain,
        Vector2Int floor,
        Vector2Int direction,
        System.Random random)
    {
        Vector2Int breakableEntrance = floor + direction;

        if (!IsInsidePlayableBounds(terrain, breakableEntrance) ||
            !IsWallTile(terrain.Tiles[breakableEntrance.x, breakableEntrance.y]))
            return null;

        List<Vector2Int> roomSizes = new List<Vector2Int>();

        for (int width = 3; width <= 6; width++)
        {
            for (int height = 3; height <= 6; height++)
                roomSizes.Add(new Vector2Int(width, height));
        }

        Shuffle(roomSizes, random);

        foreach (Vector2Int roomSize in roomSizes)
        {
            RectInt room = GetSecretRoomRect(terrain, breakableEntrance, direction, roomSize.x, roomSize.y);

            if (!IsRoomInsidePlayableBounds(terrain, room))
                continue;
            if (OverlapsExistingRoom(room, terrain.Rooms, 1) ||
                OverlapsExistingSecretRoom(room, terrain.SecretRooms, 1))
                continue;
            if (!CanHideSecretRoom(terrain, room, breakableEntrance))
                continue;

            return room;
        }

        return null;
    }

    private RectInt GetSecretRoomRect(
        NecrodancerTerrainData terrain,
        Vector2Int breakableEntrance,
        Vector2Int direction,
        int width,
        int height)
    {
        if (direction == Vector2Int.right)
        {
            int y = Mathf.Clamp(breakableEntrance.y - height / 2, 2, terrain.Height - height - 2);
            return new RectInt(breakableEntrance.x + 1, y, width, height);
        }

        if (direction == Vector2Int.left)
        {
            int y = Mathf.Clamp(breakableEntrance.y - height / 2, 2, terrain.Height - height - 2);
            return new RectInt(breakableEntrance.x - width, y, width, height);
        }

        if (direction == Vector2Int.up)
        {
            int x = Mathf.Clamp(breakableEntrance.x - width / 2, 2, terrain.Width - width - 2);
            return new RectInt(x, breakableEntrance.y - height, width, height);
        }

        int downX = Mathf.Clamp(breakableEntrance.x - width / 2, 2, terrain.Width - width - 2);
        return new RectInt(downX, breakableEntrance.y + 1, width, height);
    }

    private bool CanHideSecretRoom(
        NecrodancerTerrainData terrain,
        RectInt room,
        Vector2Int breakableEntrance)
    {
        List<Vector2Int> entrancePositions = GetSecretRoomEntrancePositions(terrain, room);

        if (!entrancePositions.Contains(breakableEntrance))
            return false;
        if (entrancePositions.Count == 0)
            return false;

        for (int x = room.xMin - 1; x <= room.xMax; x++)
        {
            for (int y = room.yMin - 1; y <= room.yMax; y++)
            {
                Vector2Int position = new Vector2Int(x, y);

                if (!IsInsideBounds(terrain, position) || room.Contains(position))
                    continue;
                if (!HasCardinalNeighborInsideRoom(room, position))
                    continue;
                if (entrancePositions.Contains(position))
                    continue;
                if (!IsWallTile(terrain.Tiles[position.x, position.y]))
                    return false;
            }
        }

        return true;
    }

    private List<Vector2Int> GetSecretRoomEntrancePositions(NecrodancerTerrainData terrain, RectInt room)
    {
        List<Vector2Int> entrancePositions = new List<Vector2Int>();

        for (int x = room.xMin - 1; x <= room.xMax; x++)
        {
            for (int y = room.yMin - 1; y <= room.yMax; y++)
            {
                Vector2Int position = new Vector2Int(x, y);

                if (!IsInsideBounds(terrain, position))
                    continue;
                if (!HasCardinalNeighborInsideRoom(room, position))
                    continue;
                if (!IsWallTile(terrain.Tiles[position.x, position.y]))
                    continue;
                if (!HasWalkableNeighborOutsideRoom(terrain, room, position))
                    continue;

                entrancePositions.Add(position);
            }
        }

        return entrancePositions;
    }

    private void AddRoomWallLayers(
        NecrodancerTerrainData terrain,
        List<RectInt> rooms,
        System.Random random)
    {
        foreach (RectInt room in rooms)
            AddRoomWallLayers(terrain, room, random, null);
    }

    private void AddRoomWallLayers(
        NecrodancerTerrainData terrain,
        RectInt room,
        System.Random random,
        List<Vector2Int> forcedBreakableWalls)
    {
        AddRoomWallLayer(terrain, room, 2, random, false, null);
        AddRoomWallLayer(terrain, room, 1, random, true, forcedBreakableWalls);
    }

    private void AddRoomWallLayer(
        NecrodancerTerrainData terrain,
        RectInt room,
        int layer,
        System.Random random,
        bool allowBreakable,
        List<Vector2Int> forcedBreakableWalls)
    {
        for (int x = room.xMin - layer; x < room.xMax + layer; x++)
        {
            for (int y = room.yMin - layer; y < room.yMax + layer; y++)
            {
                Vector2Int position = new Vector2Int(x, y);

                if (!IsInsidePlayableBounds(terrain, position))
                    continue;
                if (!IsOnRoomWallLayer(room, position, layer))
                    continue;
                if (!IsWallTile(terrain.Tiles[x, y]))
                    continue;

                bool forceBreakable = forcedBreakableWalls != null && forcedBreakableWalls.Contains(position);
                bool canBeBreakable = forceBreakable || IsRoomWallBackedByNormalWall(terrain, room, position);

                terrain.Tiles[x, y] = allowBreakable && canBeBreakable && (forceBreakable || random.NextDouble() < RoomWallBreakableChance)
                    ? TileManager.TileType.BREAKABLEWALL
                    : TileManager.TileType.WALL;
            }
        }
    }

    private bool IsRoomWallBackedByNormalWall(NecrodancerTerrainData terrain, RectInt room, Vector2Int wallPosition)
    {
        foreach (Vector2Int direction in CardinalDirections)
        {
            Vector2Int insidePosition = wallPosition + direction;

            if (!room.Contains(insidePosition))
                continue;

            Vector2Int backedPosition = wallPosition - direction;

            return IsInsideBounds(terrain, backedPosition) &&
                   terrain.Tiles[backedPosition.x, backedPosition.y] == TileManager.TileType.WALL;
        }

        return false;
    }

    private bool IsOnRoomWallLayer(RectInt room, Vector2Int position, int layer)
    {
        return position.x == room.xMin - layer ||
               position.x == room.xMax + layer - 1 ||
               position.y == room.yMin - layer ||
               position.y == room.yMax + layer - 1;
    }

    private void CarveHorizontalCorridor(NecrodancerTerrainData terrain, int fromX, int toX, int y, int width)
    {
        int start = Mathf.Min(fromX, toX);
        int end = Mathf.Max(fromX, toX);

        for (int x = start; x <= end; x++)
        {
            for (int offset = 0; offset < width; offset++)
            {
                Vector2Int position = new Vector2Int(x, y + offset);

                if (IsInsidePlayableBounds(terrain, position))
                    terrain.Tiles[position.x, position.y] = TileManager.TileType.WALKABLE;
            }
        }
    }

    private void CarveVerticalCorridor(NecrodancerTerrainData terrain, int fromY, int toY, int x, int width)
    {
        int start = Mathf.Min(fromY, toY);
        int end = Mathf.Max(fromY, toY);

        for (int y = start; y <= end; y++)
        {
            for (int offset = 0; offset < width; offset++)
            {
                Vector2Int position = new Vector2Int(x + offset, y);

                if (IsInsidePlayableBounds(terrain, position))
                    terrain.Tiles[position.x, position.y] = TileManager.TileType.WALKABLE;
            }
        }
    }

    private void CarveOpenPatch(NecrodancerTerrainData terrain, Vector2Int center, int radius)
    {
        for (int x = center.x - radius; x <= center.x + radius; x++)
        {
            for (int y = center.y - radius; y <= center.y + radius; y++)
            {
                Vector2Int position = new Vector2Int(x, y);

                if (IsInsidePlayableBounds(terrain, position))
                    terrain.Tiles[x, y] = TileManager.TileType.WALKABLE;
            }
        }
    }

    private int CountReachableWalkableTilesInRadius(NecrodancerTerrainData terrain, Vector2Int center, int radius)
    {
        int count = 0;

        for (int x = center.x - radius; x <= center.x + radius; x++)
        {
            for (int y = center.y - radius; y <= center.y + radius; y++)
            {
                Vector2Int position = new Vector2Int(x, y);

                if (IsWalkable(terrain, position) && ManhattanDistance(center, position) <= radius + 1)
                    count++;
            }
        }

        return count;
    }

    private int CountWalkableNeighbors(NecrodancerTerrainData terrain, Vector2Int position, bool includeDiagonals)
    {
        int count = 0;
        Vector2Int[] directions = includeDiagonals ? EightDirections : CardinalDirections;

        foreach (Vector2Int direction in directions)
        {
            if (IsWalkable(terrain, position + direction))
                count++;
        }

        return count;
    }

    private bool IsWalkable(NecrodancerTerrainData terrain, Vector2Int position)
    {
        return IsInsideBounds(terrain, position) &&
               terrain.Tiles[position.x, position.y] == TileManager.TileType.WALKABLE;
    }

    private bool IsInsideBounds(NecrodancerTerrainData terrain, Vector2Int position)
    {
        return position.x >= 0 &&
               position.y >= 0 &&
               position.x < terrain.Width &&
               position.y < terrain.Height;
    }

    private bool IsInsidePlayableBounds(NecrodancerTerrainData terrain, Vector2Int position)
    {
        return position.x >= BorderSize &&
               position.y >= BorderSize &&
               position.x < terrain.Width - BorderSize &&
               position.y < terrain.Height - BorderSize;
    }

    private bool IsRoomInsidePlayableBounds(NecrodancerTerrainData terrain, RectInt room)
    {
        return room.xMin >= BorderSize + 1 &&
               room.yMin >= BorderSize + 1 &&
               room.xMax < terrain.Width - BorderSize &&
               room.yMax < terrain.Height - BorderSize;
    }

    private bool IsInsideSecretRoom(NecrodancerTerrainData terrain, Vector2Int position)
    {
        foreach (SecretRoomData secretRoom in terrain.SecretRooms)
        {
            if (secretRoom.Room.Contains(position))
                return true;
        }

        return false;
    }

    private bool IsInsideAnyRoom(NecrodancerTerrainData terrain, Vector2Int position)
    {
        foreach (RectInt room in terrain.Rooms)
        {
            if (room.Contains(position))
                return true;
        }

        return IsInsideSecretRoom(terrain, position);
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

    private bool HasWalkableNeighborOutsideRoom(NecrodancerTerrainData terrain, RectInt room, Vector2Int position)
    {
        foreach (Vector2Int direction in CardinalDirections)
        {
            Vector2Int neighbor = position + direction;

            if (room.Contains(neighbor))
                continue;
            if (IsWalkable(terrain, neighbor))
                return true;
        }

        return false;
    }

    private bool OverlapsExistingSecretRoom(RectInt room, List<SecretRoomData> secretRooms, int margin)
    {
        RectInt expandedRoom = new RectInt(
            room.xMin - margin,
            room.yMin - margin,
            room.width + margin * 2,
            room.height + margin * 2);

        foreach (SecretRoomData secretRoom in secretRooms)
        {
            if (expandedRoom.Overlaps(secretRoom.Room))
                return true;
        }

        return false;
    }

    private bool IsOccupiedByImportantPosition(NecrodancerTerrainData terrain, Vector2Int position)
    {
        return position == terrain.PlayerSpawnPosition ||
               position == terrain.ExitPosition ||
               terrain.EnemyPositions.Contains(position) ||
               terrain.GoldPositions.Contains(position);
    }

    private bool IsFarEnoughFromEnemies(Vector2Int candidate, List<Vector2Int> enemyPositions, int minDistance)
    {
        foreach (Vector2Int enemyPosition in enemyPositions)
        {
            int chebyshevDistance = Mathf.Max(
                Mathf.Abs(candidate.x - enemyPosition.x),
                Mathf.Abs(candidate.y - enemyPosition.y));

            if (chebyshevDistance < minDistance)
                return false;
        }

        return true;
    }

    private void AddGoldPosition(NecrodancerTerrainData terrain, Vector2Int position)
    {
        if (!terrain.GoldPositions.Contains(position) && !IsOccupiedByImportantPosition(terrain, position))
            terrain.GoldPositions.Add(position);
    }

    private bool IsWallTile(TileManager.TileType tileType)
    {
        return tileType == TileManager.TileType.WALL ||
               tileType == TileManager.TileType.BREAKABLEWALL;
    }

    private void EnforceOuterBorderWalls(NecrodancerTerrainData terrain)
    {
        for (int x = 0; x < terrain.Width; x++)
        {
            terrain.Tiles[x, 0] = TileManager.TileType.WALL;
            terrain.Tiles[x, terrain.Height - 1] = TileManager.TileType.WALL;
        }

        for (int y = 0; y < terrain.Height; y++)
        {
            terrain.Tiles[0, y] = TileManager.TileType.WALL;
            terrain.Tiles[terrain.Width - 1, y] = TileManager.TileType.WALL;
        }
    }

    private List<Vector2Int> GetWalkablePositions(NecrodancerTerrainData terrain)
    {
        List<Vector2Int> positions = new List<Vector2Int>();

        for (int x = 1; x < terrain.Width - 1; x++)
        {
            for (int y = 1; y < terrain.Height - 1; y++)
            {
                Vector2Int position = new Vector2Int(x, y);

                if (IsWalkable(terrain, position))
                    positions.Add(position);
            }
        }

        return positions;
    }

    private Vector2Int GetFarthestRoomCenter(List<RectInt> rooms, Vector2Int fromPosition)
    {
        Vector2Int farthest = GetRoomCenter(rooms[0]);
        int bestDistance = -1;

        foreach (RectInt room in rooms)
        {
            Vector2Int center = GetRoomCenter(room);
            int distance = ManhattanDistance(center, fromPosition);

            if (distance > bestDistance)
            {
                bestDistance = distance;
                farthest = center;
            }
        }

        return farthest;
    }

    private int GetNearestRoomIndex(Vector2Int from, List<RectInt> rooms)
    {
        int nearestIndex = 0;
        int nearestDistance = int.MaxValue;

        for (int i = 0; i < rooms.Count; i++)
        {
            int distance = ManhattanDistance(from, GetRoomCenter(rooms[i]));

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = i;
            }
        }

        return nearestIndex;
    }

    private bool OverlapsExistingRoom(RectInt room, List<RectInt> rooms, int margin)
    {
        RectInt expandedRoom = new RectInt(
            room.xMin - margin,
            room.yMin - margin,
            room.width + margin * 2,
            room.height + margin * 2);

        foreach (RectInt existingRoom in rooms)
        {
            if (expandedRoom.Overlaps(existingRoom))
                return true;
        }

        return false;
    }

    private Vector2Int GetRoomCenter(RectInt room)
    {
        return new Vector2Int(room.xMin + room.width / 2, room.yMin + room.height / 2);
    }

    private Vector2Int GetRandomDirection(System.Random random)
    {
        return CardinalDirections[random.Next(CardinalDirections.Length)];
    }

    private int ManhattanDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private void Fill(TileManager.TileType[,] tiles, TileManager.TileType tileType)
    {
        for (int x = 0; x < tiles.GetLength(0); x++)
        {
            for (int y = 0; y < tiles.GetLength(1); y++)
                tiles[x, y] = tileType;
        }
    }

    private void Shuffle<T>(List<T> list, System.Random random)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int swapIndex = random.Next(i + 1);
            T value = list[i];
            list[i] = list[swapIndex];
            list[swapIndex] = value;
        }
    }

    private static readonly Vector2Int[] CardinalDirections =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1)
    };

    private static readonly Vector2Int[] EightDirections =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1),
        new Vector2Int(1, 1),
        new Vector2Int(1, -1),
        new Vector2Int(-1, 1),
        new Vector2Int(-1, -1)
    };
}

public sealed class NecrodancerTerrainData
{
    public NecrodancerTerrainData(int width, int height)
    {
        Width = width;
        Height = height;
        Tiles = new TileManager.TileType[width, height];
    }

    public int Width { get; }
    public int Height { get; }
    public TileManager.TileType[,] Tiles { get; }
    public List<RectInt> Rooms { get; } = new List<RectInt>();
    public List<SecretRoomData> SecretRooms { get; } = new List<SecretRoomData>();
    public Vector2Int PlayerSpawnPosition { get; set; }
    public Vector2Int ExitPosition { get; set; }
    public List<Vector2Int> EnemyPositions { get; } = new List<Vector2Int>();
    public List<Vector2Int> GoldPositions { get; } = new List<Vector2Int>();
    public List<DoorSpawnData> DoorPositions { get; } = new List<DoorSpawnData>();
    public List<HiddenGoldPocketData> HiddenGoldPockets { get; } = new List<HiddenGoldPocketData>();
}

public sealed class DoorSpawnData
{
    public DoorSpawnData(Vector2Int position, bool isHorizontal)
    {
        Position = position;
        IsHorizontal = isHorizontal;
    }

    public Vector2Int Position { get; }
    public bool IsHorizontal { get; }
}

public sealed class SecretRoomData
{
    public SecretRoomData(RectInt room, Vector2Int entrancePosition)
        : this(room, new List<Vector2Int> { entrancePosition })
    {
    }

    public SecretRoomData(RectInt room, List<Vector2Int> entrancePositions)
    {
        Room = room;
        EntrancePositions = new List<Vector2Int>(entrancePositions);
        EntrancePosition = EntrancePositions.Count > 0 ? EntrancePositions[0] : Vector2Int.zero;
    }

    public RectInt Room { get; }
    public List<Vector2Int> EntrancePositions { get; }
    public Vector2Int EntrancePosition { get; }
}

public sealed class HiddenGoldPocketData
{
    public HiddenGoldPocketData(Vector2Int goldPosition, Vector2Int coverEntrancePosition)
    {
        GoldPosition = goldPosition;
        CoverEntrancePosition = coverEntrancePosition;
    }

    public Vector2Int GoldPosition { get; }
    public Vector2Int CoverEntrancePosition { get; }
}

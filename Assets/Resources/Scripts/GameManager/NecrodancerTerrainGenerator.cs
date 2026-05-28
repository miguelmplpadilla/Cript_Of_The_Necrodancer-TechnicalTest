using System;
using System.Collections.Generic;
using Resources.Scripts;
using UnityEngine;

public sealed class NecrodancerTerrainGenerator
{
    private const int BorderSize = 1;
    private const int SpawnSafeRadius = 3;
    private const int MaxGenerationAttempts = 80;

    public NecrodancerTerrainData Generate(int minMapSize, int maxMapSize, int minRooms, int maxRooms, int seed)
    {
        minMapSize = Mathf.Clamp(minMapSize, 25, 40);
        maxMapSize = Mathf.Clamp(maxMapSize, minMapSize, 40);
        minRooms = Mathf.Clamp(minRooms, 5, 14);
        maxRooms = Mathf.Clamp(maxRooms, minRooms, 14);

        int baseSeed = seed == 0 ? Environment.TickCount : seed;

        for (int attempt = 0; attempt < MaxGenerationAttempts; attempt++)
        {
            System.Random random = new System.Random(baseSeed + attempt);
            NecrodancerTerrainData terrain = BuildTerrain(minMapSize, maxMapSize, minRooms, maxRooms, random);

            if (IsValidTerrain(terrain, minRooms, maxRooms))
                return terrain;
        }

        throw new InvalidOperationException("Could not generate a valid Necrodancer terrain with the current rules.");
    }

    private NecrodancerTerrainData BuildTerrain(int minMapSize, int maxMapSize, int minRooms, int maxRooms, System.Random random)
    {
        int width = random.Next(minMapSize, maxMapSize + 1);
        int height = random.Next(minMapSize, maxMapSize + 1);
        int targetRooms = random.Next(minRooms, maxRooms + 1);

        NecrodancerTerrainData terrain = new NecrodancerTerrainData(width, height);
        Fill(terrain.Tiles, TileManager.TileType.WALL);

        CreateRooms(terrain, targetRooms, random);
        ConnectRooms(terrain, random);
        terrain.PlayerSpawnPosition = GetRoomCenter(terrain.Rooms[0]);
        AddSecretRooms(terrain, random, random.Next(2, 5));
        AddIrregularOpenZones(terrain, random);

        CarveSpawnSafeZone(terrain);
        terrain.ExitPosition = GetFarthestRoomCenter(terrain.Rooms, terrain.PlayerSpawnPosition);

        PlaceEnemies(terrain, random);
        PlaceBreakableWallsAndGold(terrain, random);

        return terrain;
    }

    private void CreateRooms(NecrodancerTerrainData terrain, int targetRooms, System.Random random)
    {
        int firstRoomWidth = random.Next(8, 11);
        int firstRoomHeight = random.Next(7, 10);
        RectInt spawnRoom = new RectInt(
            terrain.Width / 2 - firstRoomWidth / 2,
            terrain.Height / 2 - firstRoomHeight / 2,
            firstRoomWidth,
            firstRoomHeight);

        terrain.Rooms.Add(spawnRoom);
        CarveRoom(terrain, spawnRoom);

        for (int attempts = 0; terrain.Rooms.Count < targetRooms && attempts < 450; attempts++)
        {
            int roomWidth = random.Next(5, 11);
            int roomHeight = random.Next(5, 10);
            int x = random.Next(BorderSize + 1, terrain.Width - roomWidth - BorderSize);
            int y = random.Next(BorderSize + 1, terrain.Height - roomHeight - BorderSize);
            RectInt room = new RectInt(x, y, roomWidth, roomHeight);

            if (OverlapsExistingRoom(room, terrain.Rooms, 2))
                continue;

            terrain.Rooms.Add(room);
            CarveRoom(terrain, room);
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

            if (random.NextDouble() < 0.5)
            {
                CarveHorizontalCorridor(terrain, from.x, to.x, from.y, corridorWidth);
                CarveVerticalCorridor(terrain, from.y, to.y, to.x, corridorWidth);
            }
            else
            {
                CarveVerticalCorridor(terrain, from.y, to.y, from.x, corridorWidth);
                CarveHorizontalCorridor(terrain, from.x, to.x, to.y, corridorWidth);
            }

            CarveOpenPatch(terrain, to, 1);
            connectedRooms.Add(toRoom);
            remainingRooms.RemoveAt(nearestIndex);
        }
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
                if (IsInsidePlayableBounds(terrain, current))
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

    private void PlaceEnemies(NecrodancerTerrainData terrain, System.Random random)
    {
        for (int roomIndex = 1; roomIndex < terrain.Rooms.Count; roomIndex++)
        {
            RectInt room = terrain.Rooms[roomIndex];
            int roomArea = room.width * room.height;
            int enemyCount = 1;

            if (roomArea >= 45 && random.NextDouble() < 0.45)
                enemyCount++;
            if (roomArea >= 65 && random.NextDouble() < 0.15)
                enemyCount++;

            for (int i = 0; i < enemyCount; i++)
            {
                if (terrain.EnemyPositions.Count >= 16)
                    return;

                Vector2Int? enemyPosition = FindEnemyPositionInRoom(terrain, room, random);

                if (enemyPosition.HasValue)
                    terrain.EnemyPositions.Add(enemyPosition.Value);
            }
        }
    }

    private Vector2Int? FindEnemyPositionInRoom(NecrodancerTerrainData terrain, RectInt room, System.Random random)
    {
        for (int attempt = 0; attempt < 70; attempt++)
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
            if (CountReachableWalkableTilesInRadius(terrain, candidate, 2) < 12)
                continue;
            if (!IsFarEnoughFromEnemies(candidate, terrain.EnemyPositions, 2))
                continue;

            return candidate;
        }

        return null;
    }

    private void PlaceBreakableWallsAndGold(NecrodancerTerrainData terrain, System.Random random)
    {
        AddBreakableShortcuts(terrain, random, random.Next(3, 6));
        AddHiddenGoldPockets(terrain, random, random.Next(2, 5));
        AddGoldNearEnemies(terrain, random);
        AddCornerGold(terrain, random, random.Next(2, 4));
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

    private void AddSecretRooms(NecrodancerTerrainData terrain, System.Random random, int targetSecretRooms)
    {
        List<Vector2Int> floors = GetWalkablePositions(terrain);
        Shuffle(floors, random);

        foreach (Vector2Int floor in floors)
        {
            if (terrain.SecretRooms.Count >= targetSecretRooms)
                return;
            if (ManhattanDistance(floor, terrain.PlayerSpawnPosition) < 8)
                continue;

            List<Vector2Int> directions = new List<Vector2Int>(CardinalDirections);
            Shuffle(directions, random);

            foreach (Vector2Int direction in directions)
            {
                RectInt? room = TryCreateSecretRoomAt(terrain, floor, direction, random);

                if (!room.HasValue)
                    continue;

                Vector2Int breakableEntrance = floor + direction;
                terrain.Tiles[breakableEntrance.x, breakableEntrance.y] = TileManager.TileType.BREAKABLEWALL;
                terrain.SecretRooms.Add(new SecretRoomData(room.Value, breakableEntrance));
                CarveRoom(terrain, room.Value);
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
            terrain.Tiles[breakableEntrance.x, breakableEntrance.y] != TileManager.TileType.WALL)
            return null;

        int width = random.Next(4, 7);
        int height = random.Next(4, 7);
        RectInt room;

        if (direction == Vector2Int.right)
        {
            int y = Mathf.Clamp(breakableEntrance.y - height / 2, 2, terrain.Height - height - 2);
            room = new RectInt(breakableEntrance.x + 1, y, width, height);
        }
        else if (direction == Vector2Int.left)
        {
            int y = Mathf.Clamp(breakableEntrance.y - height / 2, 2, terrain.Height - height - 2);
            room = new RectInt(breakableEntrance.x - width, y, width, height);
        }
        else if (direction == Vector2Int.up)
        {
            int x = Mathf.Clamp(breakableEntrance.x - width / 2, 2, terrain.Width - width - 2);
            room = new RectInt(x, breakableEntrance.y - height, width, height);
        }
        else
        {
            int x = Mathf.Clamp(breakableEntrance.x - width / 2, 2, terrain.Width - width - 2);
            room = new RectInt(x, breakableEntrance.y + 1, width, height);
        }

        if (!IsRoomInsidePlayableBounds(terrain, room))
            return null;
        if (OverlapsExistingRoom(room, terrain.Rooms, 1) ||
            OverlapsExistingSecretRoom(room, terrain.SecretRooms, 1))
            return null;
        if (!SecretRoomHasSingleBreakableConnection(terrain, room, breakableEntrance))
            return null;

        return room;
    }

    private bool SecretRoomHasSingleBreakableConnection(
        NecrodancerTerrainData terrain,
        RectInt room,
        Vector2Int breakableEntrance)
    {
        for (int x = room.xMin - 1; x <= room.xMax; x++)
        {
            for (int y = room.yMin - 1; y <= room.yMax; y++)
            {
                Vector2Int position = new Vector2Int(x, y);

                if (!IsInsideBounds(terrain, position) || room.Contains(position))
                    continue;
                if (position == breakableEntrance)
                    continue;
                if (terrain.Tiles[position.x, position.y] != TileManager.TileType.WALL)
                    return false;
            }
        }

        return true;
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
}

public sealed class SecretRoomData
{
    public SecretRoomData(RectInt room, Vector2Int entrancePosition)
    {
        Room = room;
        EntrancePosition = entrancePosition;
    }

    public RectInt Room { get; }
    public Vector2Int EntrancePosition { get; }
}

using UnityEngine;

namespace Resources.Scripts
{
    [RequireComponent(typeof(Camera))]
    public class PlayerTorchDarknessController : MonoBehaviour
    {
        [SerializeField] private bool enableDarkness = true;
        [SerializeField] private int fullLightTileRadius = 2;
        [SerializeField, Range(0f, 1f)] private float litAlpha = 0f;
        [SerializeField, Range(0f, 1f)] private float firstFalloffAlpha = 0.32f;
        [SerializeField, Range(0f, 1f)] private float secondFalloffAlpha = 0.58f;
        [SerializeField, Range(0f, 1f)] private float beatAlphaPulse = 0.1f;
        [SerializeField, Range(0f, 1f)] private float darknessAlpha = 0.88f;
        [SerializeField] private int sortingOrder = 100;

        private EventBinding<BeatEvent> _beatBinding;
        private GameObject _darknessRoot;
        private Sprite _shadowSprite;
        private SpriteRenderer[,] _shadowTiles;
        private Vector2Int _lastPlayerPosition = new Vector2Int(int.MinValue, int.MinValue);
        private int _lastClosedDoorBlockerCount = -1;
        private bool _beatPulsePhase;

        private void Awake()
        {
            _beatBinding = new EventBinding<BeatEvent>(OnBeat, gameObject);
            EventBus<BeatEvent>.Register(_beatBinding);
            _shadowSprite = CreateShadowSprite();
        }

        private void OnDestroy()
        {
            if (_beatBinding != null)
                EventBus<BeatEvent>.Deregister(_beatBinding);
        }

        private void LateUpdate()
        {
            if (MapGenerator.instance == null || MapGenerator.instance.grid == null)
                return;

            if (_shadowTiles == null)
                CreateShadowTiles();

            if (_darknessRoot != null)
                _darknessRoot.SetActive(enableDarkness);

            if (!enableDarkness || PlayerController.instance == null)
                return;

            Vector2Int playerPosition = PlayerController.instance.indexPosition;
            int closedDoorBlockerCount = MapGenerator.instance.GetClosedDoorVisionBlockerCount();

            if (playerPosition == _lastPlayerPosition && closedDoorBlockerCount == _lastClosedDoorBlockerCount)
                return;

            _lastPlayerPosition = playerPosition;
            _lastClosedDoorBlockerCount = closedDoorBlockerCount;
            UpdateShadowAlphas();
        }

        private void OnBeat()
        {
            _beatPulsePhase = !_beatPulsePhase;
            UpdateShadowAlphas();
        }

        private void CreateShadowTiles()
        {
            int width = MapGenerator.instance.sizeGridX;
            int height = MapGenerator.instance.sizeGridY;
            _shadowTiles = new SpriteRenderer[width, height];

            _darknessRoot = new GameObject("PlayerTorchTileDarkness");
            _darknessRoot.transform.SetParent(null);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (MapGenerator.instance.grid[x, y] == null)
                        continue;

                    GameObject shadowTile = new GameObject($"Shadow_{x}_{y}");
                    shadowTile.transform.SetParent(_darknessRoot.transform);
                    shadowTile.transform.position = MapGenerator.instance.GetWorldPosition(new Vector2Int(x, y));
                    shadowTile.transform.localScale = Vector3.one;

                    SpriteRenderer shadowRenderer = shadowTile.AddComponent<SpriteRenderer>();
                    shadowRenderer.sprite = _shadowSprite;
                    shadowRenderer.sortingOrder = sortingOrder;
                    shadowRenderer.color = new Color(0f, 0f, 0f, darknessAlpha);
                    _shadowTiles[x, y] = shadowRenderer;
                }
            }
        }

        private Sprite CreateShadowSprite()
        {
            Texture2D texture = new Texture2D(1, 1)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        private void UpdateShadowAlphas()
        {
            if (_shadowTiles == null || PlayerController.instance == null)
                return;

            Vector2Int playerPosition = PlayerController.instance.indexPosition;
            RectInt? currentRoom = GetCurrentRoom(playerPosition);

            for (int x = 0; x < _shadowTiles.GetLength(0); x++)
            {
                for (int y = 0; y < _shadowTiles.GetLength(1); y++)
                {
                    SpriteRenderer shadowTile = _shadowTiles[x, y];

                    if (shadowTile == null)
                        continue;

                    Vector2Int tilePosition = new Vector2Int(x, y);
                    int tileDistance = currentRoom.HasValue
                        ? GetDistanceFromRoom(tilePosition, currentRoom.Value)
                        : GetDistanceFromPlayer(tilePosition, playerPosition);
                    float pulseAlpha = ShouldPulseTile(tilePosition) ? beatAlphaPulse : 0f;
                    float alpha = GetAlphaForTileDistance(tileDistance, pulseAlpha);

                    if (IsViewBlockedByClosedDoor(playerPosition, tilePosition))
                        alpha = darknessAlpha;

                    shadowTile.color = new Color(0f, 0f, 0f, alpha);
                }
            }
        }

        private bool IsViewBlockedByClosedDoor(Vector2Int from, Vector2Int to)
        {
            if (from == to)
                return false;

            int x = from.x;
            int y = from.y;
            int dx = Mathf.Abs(to.x - from.x);
            int dy = Mathf.Abs(to.y - from.y);
            int stepX = from.x < to.x ? 1 : -1;
            int stepY = from.y < to.y ? 1 : -1;
            int error = dx - dy;

            while (x != to.x || y != to.y)
            {
                int doubledError = error * 2;

                if (doubledError > -dy)
                {
                    error -= dy;
                    x += stepX;
                }

                if (doubledError < dx)
                {
                    error += dx;
                    y += stepY;
                }

                Vector2Int checkPosition = new Vector2Int(x, y);

                if (checkPosition == to)
                    return false;
                if (MapGenerator.instance.IsClosedDoorBlockingVision(checkPosition))
                    return true;
            }

            return false;
        }

        private RectInt? GetCurrentRoom(Vector2Int playerPosition)
        {
            if (MapGenerator.instance.generatedRooms != null)
            {
                foreach (RectInt room in MapGenerator.instance.generatedRooms)
                {
                    if (room.Contains(playerPosition))
                        return room;
                }
            }

            if (MapGenerator.instance.secretRooms != null)
            {
                foreach (SecretRoomData secretRoom in MapGenerator.instance.secretRooms)
                {
                    if (secretRoom.Room.Contains(playerPosition))
                        return secretRoom.Room;
                }
            }

            return null;
        }

        private int GetDistanceFromPlayer(Vector2Int tilePosition, Vector2Int playerPosition)
        {
            return Mathf.Max(
                Mathf.Abs(tilePosition.x - playerPosition.x),
                Mathf.Abs(tilePosition.y - playerPosition.y));
        }

        private int GetDistanceFromRoom(Vector2Int tilePosition, RectInt room)
        {
            int distanceX = 0;
            int distanceY = 0;

            if (tilePosition.x < room.xMin)
                distanceX = room.xMin - tilePosition.x;
            else if (tilePosition.x >= room.xMax)
                distanceX = tilePosition.x - room.xMax + 1;

            if (tilePosition.y < room.yMin)
                distanceY = room.yMin - tilePosition.y;
            else if (tilePosition.y >= room.yMax)
                distanceY = tilePosition.y - room.yMax + 1;

            return Mathf.Max(distanceX, distanceY);
        }

        private bool ShouldPulseTile(Vector2Int tilePosition)
        {
            int phaseOffset = _beatPulsePhase ? 1 : 0;
            return (tilePosition.x + tilePosition.y + phaseOffset) % 2 == 0;
        }

        private float GetAlphaForTileDistance(int tileDistance, float pulseAlpha)
        {
            if (tileDistance <= fullLightTileRadius)
                return Mathf.Clamp01(litAlpha + pulseAlpha);
            if (tileDistance == fullLightTileRadius + 1)
                return Mathf.Clamp01(firstFalloffAlpha + pulseAlpha);
            if (tileDistance == fullLightTileRadius + 2)
                return Mathf.Clamp01(secondFalloffAlpha + pulseAlpha);

            return darknessAlpha;
        }
    }
}

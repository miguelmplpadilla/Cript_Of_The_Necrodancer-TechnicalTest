using UnityEngine;

namespace Resources.Scripts
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class TileManager : MonoBehaviour
    {
        private SpriteRenderer _sr;
        
        public TileType tileType;
        public Vector2Int indexPosition;
        
        public enum TileType
        {
            WALKABLE, WALL, BREAKABLEWALL
        }

        public MonoBehaviour tokenInside;

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
        }

        public void SetTileType(TileType type)
        {
            TileType previousType = tileType;
            tileType = type;

            TileData tileData = MapTestGenerator.instance.tileDatas.Find(it => it.tileType == tileType);

            _sr.sprite = tileData.spriteTile;

            if (previousType == TileType.BREAKABLEWALL && tileType == TileType.WALKABLE)
                MapTestGenerator.instance.RevealHiddenRoomFromEntrance(indexPosition);
        }
    }
}

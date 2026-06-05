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
        public MonoBehaviour dropInside;

        public bool HasToken => tokenInside != null;
        public bool HasDrop => dropInside != null;
        public bool HasAnyOccupant => HasToken || HasDrop;

        public bool CanPlaceDrop()
        {
            return tileType == TileType.WALKABLE && !HasDrop;
        }

        public bool CanPlaceGeneratedContent()
        {
            return tileType == TileType.WALKABLE && !HasAnyOccupant;
        }

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
        }

        public void SetTileType(TileType type)
        {
            TileType previousType = tileType;
            tileType = type;

            TileData tileData = MapGenerator.instance.tileDatas.Find(it => it.tileType == tileType);

            if (tileData == null) return;
            
            _sr.sprite = tileData.spriteTile;

            if (previousType == TileType.BREAKABLEWALL && tileType == TileType.WALKABLE)
                MapGenerator.instance.RevealHiddenRoomFromEntrance(indexPosition);
        }
    }
}

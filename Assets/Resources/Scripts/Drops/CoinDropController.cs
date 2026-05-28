namespace Resources.Scripts.Drops
{
    public class CoinDropController : DropBaseController
    {
        public int cantCoins = 5;
        private bool _hasAssignedTile;

        private void Start()
        {
            if (_hasAssignedTile)
                return;

            TileManager currentTile = MapTestGenerator.instance.GetNextGoldSpawnTile();

            if (currentTile != null)
                AssignToTile(currentTile);
        }

        public void AssignToTile(TileManager tile)
        {
            if (tile == null)
                return;

            tile.tokenInside = this;
            transform.position = tile.transform.position;
            _hasAssignedTile = true;
        }

        public override void GetDropItem()
        {
            GameManager.instance.SumCoins(cantCoins);
            Destroy(gameObject);
        }
    }
}

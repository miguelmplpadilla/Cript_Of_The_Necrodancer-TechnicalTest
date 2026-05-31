using System.Collections;

namespace Resources.Scripts.Drops
{
    public class CoinDropController : DropBaseController
    {
        private void Start()
        {
            if (_hasAssignedTile)
                return;

            currentTile = MapGenerator.instance.GetNextGoldSpawnTile();

            if (currentTile != null)
                AssignToTile(currentTile);
        }

        public override IEnumerator GetDropItem()
        {
            GameManager.instance.SumCoins(cantValue);
            Destroy(gameObject);
            yield break;
        }
    }
}

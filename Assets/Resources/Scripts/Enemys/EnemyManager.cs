using System.Collections;
using Resources.Scripts;
using UnityEngine;

public class EnemyManager : TokenController
{
    public MovementType[] movements;
    public int indexMovement;

    public int cantCoinsDrop = 3;
    private bool _hasSpawnPosition;

    private LifeBarEnemyController lifeBarEnemy;
    
    
    public enum MovementType
    {
        WAIT, MOVELEFT, MOVERIGHT, MOVEUP, MOVEDOWN
    }
    
    protected override void Start()
    {
        lifeBarEnemy = Instantiate(GameManager.instance.prefabLifeEnemy, transform)
            .GetComponent<LifeBarEnemyController>();
        
        lifeBarEnemy.SetUpLife(life);
        
        if (!_hasSpawnPosition)
            indexPosition = MapGenerator.instance.GetNextEnemySpawnPosition();

        base.Start();
    }

    public void SetSpawnPosition(Vector2Int spawnPosition)
    {
        indexPosition = spawnPosition;
        _hasSpawnPosition = true;
    }

    protected override IEnumerator PlayBeat()
    {
        yield return null;
        
        if (movements == null || movements.Length == 0)
            yield break;

        if (movements[indexMovement] != MovementType.WAIT)
            yield return MoveEnemy(movements[indexMovement]);

        indexMovement++;
        if (indexMovement >= movements.Length)
            indexMovement = 0;
    }

    protected IEnumerator Attack()
    {
        yield return null;

        var player = GetPlayer();
        if (player != null)
            player.ChangeLife(-1);
    }

    private TokenController GetPlayer()
    {
        Vector2Int[] directions =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1)
        };
        
        foreach (Vector2Int direction in directions)
        {
            TileManager tile = MapGenerator.instance.GetNextTile(indexPosition + direction);

            if (tile?.tokenInside is PlayerController player)
                return player;
        }

        return null;
    }

    protected IEnumerator MoveEnemy(MovementType direction)
    {
        Vector2Int provisionalIndex = indexPosition + GetSumIndex(direction);
        nextTilePosition = MapGenerator.instance.GetNextTile(provisionalIndex);

        transform.localScale = new Vector3(provisionalIndex.x > indexPosition.x ? 1 : -1, 1, 1);

        if (nextTilePosition == null || 
            nextTilePosition.tileType == TileManager.TileType.BREAKABLEWALL) yield break;


        if (nextTilePosition == PlayerController.instance.nextTilePosition ||
            nextTilePosition?.tokenInside is PlayerController)
        {
            yield return Attack();
            yield break;
        }

        _nextIndexPosition = provisionalIndex;

        yield return Move();
    }

    protected Vector2Int GetSumIndex(MovementType direction)
    {
        if (direction == MovementType.MOVELEFT) return new Vector2Int(-1, 0);
        if (direction == MovementType.MOVERIGHT) return new Vector2Int(1, 0);
        if (direction == MovementType.MOVEUP) return new Vector2Int(0, -1);
        if (direction == MovementType.MOVEDOWN) return new Vector2Int(0, 1);
        
        return new  Vector2Int(0, 0);
    }
    
    protected IEnumerator SpecialMove()
    {
        yield return null;
    }

    protected override void CheckLife()
    {
        if (life <= 0)
            GameManager.instance.CreateCoinDrop(cantCoinsDrop, _currentTilePosition);
        base.CheckLife();
    }

    protected override void PrintLife()
    {
        lifeBarEnemy.ChangeLife(life, maxLife);
        lifeBarEnemy.gameObject.SetActive(true);
    }
}

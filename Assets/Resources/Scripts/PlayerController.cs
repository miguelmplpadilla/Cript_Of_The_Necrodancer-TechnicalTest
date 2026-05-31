using System.Collections;
using Resources.Scripts;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : TokenController
{
    public static PlayerController instance;
    
    private ActionType _actionType = ActionType.MOVE;

    private KeyCode lastInput;
    
    private KeyCode[] directionKeys =
    {
        KeyCode.UpArrow,
        KeyCode.DownArrow,
        KeyCode.RightArrow,
        KeyCode.LeftArrow
    };
    
    public enum ActionType
    {
        MOVE, ATTACK, DIG
    }

    protected override void Awake()
    {
        base.Awake();

        instance = this;
    }

    protected override void Start()
    {
        indexPosition = MapGenerator.instance.playerSpawnPosition;
        base.Start();
    }

    private void Update()
    {
        if (_isMoving || !BeatController.instance.canRegisterPlay)
        {
            foreach (var key in directionKeys)
                if (Input.GetKeyDown(key)) GameManager.instance.RestartMultiplier();
            return;
        }
        
        TryMove(KeyCode.UpArrow, 0, -1);
        TryMove(KeyCode.DownArrow, 0, 1);
        TryMove(KeyCode.RightArrow, 1, 0);
        TryMove(KeyCode.LeftArrow, -1, 0);
        
        _actionType = ActionType.MOVE;

        if (nextTilePosition != null)
        {
            if (nextTilePosition.tileType == TileManager.TileType.BREAKABLEWALL)
                _actionType = ActionType.DIG;

            if (nextTilePosition.tokenInside != null && nextTilePosition.tokenInside is EnemyManager)
                _actionType = ActionType.ATTACK;

            EventBus<BeatEvent>.Raise(new BeatEvent());
        }
    }

    protected override IEnumerator PlayBeat()
    {
        if (nextTilePosition == null)
        {
            GameManager.instance.RestartMultiplier();
            yield break;
        }

        if (lastInput is KeyCode.LeftArrow or KeyCode.RightArrow)
            transform.localScale =
                new Vector3(lastInput is KeyCode.LeftArrow ? -1 : 1, 1, 1);
        
        switch (_actionType)
        {
            case ActionType.MOVE:
                yield return Move();
                break;
            case ActionType.DIG:
                Dig();
                break;
            case  ActionType.ATTACK:
                Attack();
                break;
        }
        
        nextTilePosition = null;
    }

    private void Dig()
    {
        nextTilePosition.SetTileType(TileManager.TileType.WALKABLE);
        CameraController.instance.ShakeCamera(0.1f, 0.1f);
    }

    private void Attack()
    {
        if (nextTilePosition?.tokenInside is not EnemyManager enemy) return;

        enemy.ChangeLife(-1);
        CameraController.instance.ShakeCamera(0.1f, 0.1f);
    }

    public override void ChangeLife(int value)
    {
        base.ChangeLife(value);
        if (value < 0) GameManager.instance.RestartMultiplier();
    }

    protected override void CheckLife()
    {
        Debug.Log("Player killed");
    }

    private void TryMove(KeyCode key, int x, int y)
    {
        if (Input.GetKeyDown(key))
        {
            Vector2Int provisionalIndex = indexPosition + new Vector2Int(x, y);
            nextTilePosition = MapGenerator.instance.GetNextTile(provisionalIndex);
            if (nextTilePosition != null) _nextIndexPosition = provisionalIndex;
            lastInput = key;
        }
    }
}

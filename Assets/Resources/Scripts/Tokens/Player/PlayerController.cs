using System.Collections;
using Resources.Scripts;
using Resources.Scripts.Drops;
using Resources.Scripts.Drops.OpenType;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : TokenController
{
    public static PlayerController instance;
    
    private ActionType _actionType = ActionType.MOVE;

    private KeyCode lastInput;

    public GameObject imageHit;
    
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

    protected override void TerrainGenerated()
    {
        indexPosition = MapGenerator.instance.playerSpawnPosition;
        base.TerrainGenerated();
    }

    private void Update()
    {
        if (_isMoving || !BeatController.instance.canRegisterPlay)
        {
            foreach (var key in directionKeys)
                if (Input.GetKeyDown(key)) GameManager.instance.RestartMultiplier();
            return;
        }

        if (Input.GetKey(KeyCode.DownArrow) && Input.GetKey(KeyCode.LeftArrow) && GameManager.instance.hasBomb)
        {
            BombDropController bomb =
                GameManager.instance.CreateDrop(1, GameManager.instance.globalPrefabBombDrop, _currentTilePosition) as
                    BombDropController;
            bomb.currentTile.tokenInside = this;
            if (bomb != null) bomb.PlayAnimationExplode();
            GameManager.instance.hasBomb = false;
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

            if (!BeatController.instance.beatPlayed) 
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
                if (nextTilePosition.tokenInside is DropBaseController dropBaseController)
                {
                    StartCoroutine(dropBaseController.GetDropItem());
                    if (dropBaseController is OpenDropController) break;
                }
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

    public override void ChangeLife(float value)
    {
        base.ChangeLife(value);
        if (value < 0)
        {
            CameraController.instance.ShakeCamera(0.1f, 0.08f);
            GameManager.instance.RestartMultiplier();
            StartCoroutine(AnimationHit());
        }
    }

    private IEnumerator AnimationHit()
    {
        for (int i = 0; i < 3; i++)
        {
            imageHit.SetActive(true);
            yield return new WaitForSeconds(0.05f);
            imageHit.SetActive(false);
            yield return new WaitForSeconds(0.05f);
        }
    }

    protected override void CheckLife()
    {
        if (life > 0) return;
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

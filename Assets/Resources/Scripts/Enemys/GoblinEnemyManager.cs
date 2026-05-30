using System.Collections;
using UnityEngine;

public class GoblinEnemyManager : EnemyManager
{
    private int countBeats = 0;
    public int waitBeats = 2;
    
    protected override IEnumerator PlayBeat()
    {
        countBeats++;
        
        if (countBeats < waitBeats) yield break;

        countBeats = 0;
        
        Vector2Int positionIndexPlayer = PlayerController.instance.indexPosition;
        float distance = Vector2Int.Distance(positionIndexPlayer, indexPosition);

        if (distance > 5) yield break;

        MovementType nextMovementType;
        
        if (positionIndexPlayer.y - indexPosition.y > positionIndexPlayer.x - indexPosition.x)
            nextMovementType = positionIndexPlayer.y > indexPosition.y ? MovementType.MOVEDOWN : MovementType.MOVEUP;
        else
            nextMovementType = positionIndexPlayer.x > indexPosition.x ? MovementType.MOVELEFT : MovementType.MOVERIGHT;

        yield return MoveEnemy(nextMovementType);
    }
}

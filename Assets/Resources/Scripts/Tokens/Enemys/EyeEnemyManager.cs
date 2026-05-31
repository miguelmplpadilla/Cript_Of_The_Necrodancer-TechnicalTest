using System.Collections;
using System.Collections.Generic;
using Random = UnityEngine.Random;

public class EyeEnemyManager : GoblinEnemyManager
{
    private List<MovementType> _movementTypes = new List<MovementType>
    {
        MovementType.MOVEDOWN,
        MovementType.MOVEUP,
        MovementType.MOVELEFT,
        MovementType.MOVERIGHT
    };
    
    protected override IEnumerator PlayBeat()
    {
        countBeats++;
        
        if (countBeats < waitBeats) yield break;

        countBeats = 0;

        yield return MoveEnemy(_movementTypes[Random.Range(0, _movementTypes.Count)]);
    }
}

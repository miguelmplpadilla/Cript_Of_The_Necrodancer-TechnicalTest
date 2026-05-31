using System.Collections;
using Resources.Scripts;
using UnityEngine;

public class DropBaseController : MonoBehaviour
{
    public int cantValue = 1;
    public TileManager currentTile;
    
    protected bool _hasAssignedTile;
    
    public virtual IEnumerator GetDropItem()
    {
        yield return null;
    }
    
    public void AssignToTile(TileManager tile)
    {
        if (tile == null)
            return;

        tile.tokenInside = this;
        transform.position = tile.transform.position;
        _hasAssignedTile = true;

        currentTile = tile;
    }

    public void SetRandomCant()
    {
        cantValue = Random.Range(3, 10);
    }
}

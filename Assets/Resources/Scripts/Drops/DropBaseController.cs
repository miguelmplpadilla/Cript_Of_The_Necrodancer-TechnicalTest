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
    
    public bool AssignToTile(TileManager tile)
    {
        if (tile == null)
            return false;

        if (tile.dropInside != null && tile.dropInside != this)
        {
            Debug.LogWarning($"Cannot assign {name} to tile {tile.indexPosition}: tile already has a drop.");
            return false;
        }

        ClearCurrentTile();

        currentTile = tile;
        tile.dropInside = this;
        transform.position = tile.transform.position;
        _hasAssignedTile = true;
        return true;
    }

    public void SetRandomCant()
    {
        cantValue = Random.Range(3, 10);
    }

    protected virtual void OnDestroy()
    {
        ClearCurrentTile();
    }

    protected void ClearCurrentTile()
    {
        if (currentTile == null)
            return;

        if (currentTile.dropInside == this)
            currentTile.dropInside = null;

        if (currentTile.tokenInside == this)
            currentTile.tokenInside = null;

        currentTile = null;
    }
}

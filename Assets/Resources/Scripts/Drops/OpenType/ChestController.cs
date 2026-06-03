using System.Collections;
using Resources.Scripts;
using Resources.Scripts.Drops.OpenType;
using UnityEngine;

public class ChestController : OpenDropController
{
    public GameObject[] prefabDrops;
    public Animator animator;
    private bool _isOpen;
    
    public override IEnumerator GetDropItem()
    {
        if (_isOpen)
            yield break;

        _isOpen = true;
        animator.SetTrigger("open");
        yield break;
    }

    public void DropItem()
    {
        TileManager tile = currentTile;

        if (tile != null && tile.dropInside == this)
            tile.dropInside = null;

        var prefabDrop = prefabDrops[Random.Range(0, prefabDrops.Length)];
        DropBaseController drop = GameManager.instance.CreateDrop(1, prefabDrop, tile);

        if (drop != null)
            drop.SetRandomCant();
        
        Destroy(gameObject);
    }
}

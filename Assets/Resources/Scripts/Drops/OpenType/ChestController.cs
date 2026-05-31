using System.Collections;
using Resources.Scripts;
using Resources.Scripts.Drops.OpenType;
using UnityEngine;

public class ChestController : OpenDropController
{
    public GameObject[] prefabDrops;
    public Animator animator;
    
    public override IEnumerator GetDropItem()
    {
        animator.SetTrigger("open");
        currentTile.tokenInside = null;
        yield break;
    }

    public void DropItem()
    {
        var prefabDrop = prefabDrops[Random.Range(0, prefabDrops.Length)];
        DropBaseController drop = GameManager.instance.CreateDrop(1, prefabDrop, currentTile);
        drop.SetRandomCant();
        
        Destroy(gameObject);
    }
}

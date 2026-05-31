using System.Collections;
using Resources.Scripts.Drops.OpenType;
using UnityEngine;

public class DoorController : OpenDropController
{
    public SpriteRenderer spriteRenderer;
    
    public Sprite openDoorSprite;
    
    public override IEnumerator GetDropItem()
    {
        spriteRenderer.sprite = openDoorSprite;
        currentTile.tokenInside = null;
        yield break;
    }
}

using System.Collections;
using Resources.Scripts.Drops.OpenType;
using UnityEngine;

public class DoorController : OpenDropController
{
    public SpriteRenderer spriteRenderer;
    
    public Sprite openDoorSprite;
    private bool _isOpen;
    
    public override IEnumerator GetDropItem()
    {
        if (_isOpen)
            yield break;

        _isOpen = true;
        spriteRenderer.sprite = openDoorSprite;

        if (currentTile != null && currentTile.dropInside == this)
            currentTile.dropInside = null;

        if (currentTile != null && currentTile.tokenInside == this)
            currentTile.tokenInside = null;

        yield break;
    }
}

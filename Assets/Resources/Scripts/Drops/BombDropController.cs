using System.Collections;
using UnityEngine;

namespace Resources.Scripts.Drops
{
    public class BombDropController : DropBaseController
    {
        public bool exploding = false;

        public Animator animator;

        private int countBeat = 0;

        private void Start()
        {
            EventBus<BeatEvent>.Register(new EventBinding<BeatEvent>(PlayBeat, gameObject));
        }

        private void OnDestroy()
        {
            EventBus<BeatEvent>.Deregister(new EventBinding<BeatEvent>(PlayBeat, gameObject));
        }

        public override IEnumerator GetDropItem()
        {
            if (exploding) yield break;
            
            GameManager.instance.hasBomb = true;
            Destroy(gameObject);
        }

        public void PlayAnimationExplode()
        {
            exploding = true;
            animator.SetTrigger("explode");
        }

        public void Explode()
        {
            Vector2Int[] directions =
            {
                new Vector2Int(0, 0),
                
                new Vector2Int(1, 0),
                new Vector2Int(-1, 0),
                new Vector2Int(0, 1),
                new Vector2Int(0, -1),
                
                new Vector2Int(1, 1),
                new Vector2Int(-1, -1),
                new Vector2Int(-1, 1),
                new Vector2Int(1, -1)
            };

            for (int i = 0; i < directions.Length; i++)
            {
                TileManager tile = MapGenerator.instance.GetNextTile(currentTile.indexPosition + directions[i]);
                if (tile.tokenInside != null && tile.tokenInside is TokenController token)
                    token.ChangeLife(-4);
            }
        }

        private void PlayBeat()
        {
            countBeat++;
            if (countBeat == 5)
            {
                Explode();
                animator.SetTrigger("bomb");
            }
        }

        public void DestroyBomb()
        {
            Destroy(gameObject);
        }
    }
}
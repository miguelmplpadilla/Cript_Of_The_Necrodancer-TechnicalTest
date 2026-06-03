using System.Collections;
using UnityEngine;

namespace Resources.Scripts.Drops
{
    public class BombDropController : DropBaseController
    {
        public bool exploding = false;

        public Animator animator;

        private int countBeat = 0;
        private EventBinding<BeatEvent> _beatBinding;

        private void Start()
        {
            _beatBinding = new EventBinding<BeatEvent>(PlayBeat, gameObject);
            EventBus<BeatEvent>.Register(_beatBinding);
        }

        protected override void OnDestroy()
        {
            if (_beatBinding != null)
                EventBus<BeatEvent>.Deregister(_beatBinding);

            base.OnDestroy();
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
                if (tile != null && tile.tokenInside is TokenController token)
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

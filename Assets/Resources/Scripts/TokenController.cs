using System.Collections;
using UnityEngine;

namespace Resources.Scripts
{
    public class TokenController : MonoBehaviour
    {
        private Rigidbody2D rb;
    
        protected bool _isMoving = false;

        public Animator spriteAnimator;

        protected Vector2Int _indexPosition;
        protected Vector2Int _nextIndexPosition;

        protected TileManager _currentTilePosition;
        public TileManager nextTilePosition = null;

        public float speed = 5;
        public int life = 3;
        public int maxLife = 3;
        
        protected virtual void Awake()
        {
            life = maxLife;
            rb = GetComponent<Rigidbody2D>();
            EventBus<BeatEvent>.Register(new EventBinding<BeatEvent>(() => { StartCoroutine(PlayBeat()); }, gameObject));
        }

        protected virtual void Start()
        {
            _currentTilePosition = MapTestGenerator.instance.grid[_indexPosition.x, _indexPosition.y];
            transform.position = _currentTilePosition.transform.position;
            _currentTilePosition.tokenInside = this;
        }

        protected void OnDestroy()
        {
            EventBus<BeatEvent>.Deregister(new EventBinding<BeatEvent>(() => { StartCoroutine(PlayBeat()); }, gameObject));
        }
        
        protected virtual IEnumerator PlayBeat()
        {
            yield return null;
        }
        
        protected IEnumerator Move()
        {
            _isMoving = true;
            Vector2 startPosition = rb.position;
            Vector2 targetPosition = nextTilePosition.transform.position;
            float movementDuration = Vector2.Distance(startPosition, targetPosition) / Mathf.Max(speed, 0.01f);
            float elapsed = 0f;

            if (movementDuration <= 0f)
            {
                rb.MovePosition(targetPosition);
                _currentTilePosition = nextTilePosition;
                _indexPosition = _nextIndexPosition;
                _isMoving = false;
                yield break;
            }

            Coroutine jumpCoroutine = StartCoroutine(JumpAnimation(movementDuration, spriteAnimator.gameObject));
            spriteAnimator.SetTrigger("jump");
        
            while (elapsed < movementDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / movementDuration;

                rb.MovePosition(Vector2.Lerp(startPosition, targetPosition, t));

                yield return null;
            }

            rb.MovePosition(targetPosition);
            yield return jumpCoroutine;
            
            _currentTilePosition.tokenInside = null;

            _currentTilePosition = nextTilePosition;
            _indexPosition = _nextIndexPosition;
            _isMoving = false;
            
            if (_currentTilePosition.tokenInside is DropBaseController coin)
                coin.GetDropItem();

            _currentTilePosition.tokenInside = this;
        }
        
        protected IEnumerator JumpAnimation(float duration, GameObject objAnimation)
        {
            Vector3 originalPosition = objAnimation.transform.localPosition;
            Vector3 nextPosition = originalPosition + Vector3.up * 0.42f;
            float halfDuration = duration * 0.5f;
        
            yield return MoveJumpAnimation(nextPosition, halfDuration, objAnimation);
            yield return MoveJumpAnimation(originalPosition, halfDuration, objAnimation);
        }

        protected IEnumerator MoveJumpAnimation(Vector3 finalPosition, float duration, GameObject objAnimation)
        {
            Vector3 initialPosition = objAnimation.transform.localPosition;
            float elapsed = 0f;

            if (duration <= 0f)
            {
                objAnimation.transform.localPosition = finalPosition;
                yield break;
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                objAnimation.transform.localPosition = Vector3.Lerp(initialPosition, finalPosition, t);
            
                yield return null;
            }

            objAnimation.transform.localPosition = finalPosition;
        }

        public virtual void ChangeLife(int value)
        {
            life += value;
            PrintLife();
            
            CheckLife();
        }

        protected virtual void CheckLife()
        {
            if (life > 0) return;
            
            GameManager.instance.SumKill(1);
            Destroy(gameObject);
        }

        protected void PrintLife()
        {
            Debug.Log("Life: "+life);
        }
    }
}

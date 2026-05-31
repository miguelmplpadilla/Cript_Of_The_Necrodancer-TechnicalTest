using System.Collections;
using UnityEngine;

namespace Resources.Scripts
{
    public class TokenController : MonoBehaviour
    {
        private Rigidbody2D rb;
        private Vector3 _spriteAnimatorBaseLocalPosition;
    
        protected bool _isMoving = false;

        public Animator spriteAnimator;

        public Vector2Int indexPosition;
        protected Vector2Int _nextIndexPosition;

        protected TileManager _currentTilePosition;
        public TileManager nextTilePosition = null;

        public float speed = 5;
        public float life = 3;
        public float maxLife = 3;
        
        protected virtual void Awake()
        {
            life = maxLife;
            rb = GetComponent<Rigidbody2D>();

            if (spriteAnimator == null)
                spriteAnimator = GetComponentInChildren<Animator>();

            if (spriteAnimator != null)
                _spriteAnimatorBaseLocalPosition = spriteAnimator.transform.localPosition;

            EventBus<BeatEvent>.Register(new EventBinding<BeatEvent>(() => { StartCoroutine(PlayBeat()); }, gameObject));
            EventBus<TerrainGenerated>.Register(new EventBinding<TerrainGenerated>(TerrainGenerated, gameObject));
        }

        protected void OnDestroy()
        {
            EventBus<BeatEvent>.Deregister(new EventBinding<BeatEvent>(() => { StartCoroutine(PlayBeat()); }, gameObject));
            EventBus<TerrainGenerated>.Deregister(new EventBinding<TerrainGenerated>(TerrainGenerated, gameObject));
        }
        
        protected virtual IEnumerator PlayBeat()
        {
            yield return null;
        }

        protected virtual void TerrainGenerated()
        {
            _currentTilePosition = MapGenerator.instance.grid[indexPosition.x, indexPosition.y];
            transform.position = _currentTilePosition.transform.position;
            ResetSpriteAnimatorPosition();
            _currentTilePosition.tokenInside = this;
        }
        
        protected IEnumerator Move()
        {
            if (_isMoving)
                yield break;

            _isMoving = true;
            Vector2 startPosition = rb.position;
            Vector2 targetPosition = nextTilePosition.transform.position;
            float movementDuration = Vector2.Distance(startPosition, targetPosition) / Mathf.Max(speed, 0.01f);
            float elapsed = 0f;

            if (movementDuration <= 0f)
            {
                rb.MovePosition(targetPosition);
                _currentTilePosition = nextTilePosition;
                indexPosition = _nextIndexPosition;
                _isMoving = false;
                yield break;
            }

            Coroutine jumpCoroutine = null;

            if (spriteAnimator != null)
            {
                jumpCoroutine = StartCoroutine(JumpAnimation(movementDuration, spriteAnimator.gameObject));
                spriteAnimator.SetTrigger("jump");
            }
        
            while (elapsed < movementDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / movementDuration;

                rb.MovePosition(Vector2.Lerp(startPosition, targetPosition, t));

                yield return null;
            }

            rb.MovePosition(targetPosition);

            if (jumpCoroutine != null)
                yield return jumpCoroutine;
            
            _currentTilePosition.tokenInside = null;

            _currentTilePosition = nextTilePosition;
            indexPosition = _nextIndexPosition;
            _isMoving = false;

            _currentTilePosition.tokenInside = this;
        }
        
        protected IEnumerator JumpAnimation(float duration, GameObject objAnimation)
        {
            Vector3 originalPosition = objAnimation == spriteAnimator?.gameObject
                ? _spriteAnimatorBaseLocalPosition
                : objAnimation.transform.localPosition;
            Vector3 nextPosition = originalPosition + Vector3.up * 0.42f;
            float halfDuration = duration * 0.5f;

            objAnimation.transform.localPosition = originalPosition;
        
            yield return MoveJumpAnimation(nextPosition, halfDuration, objAnimation);
            yield return MoveJumpAnimation(originalPosition, halfDuration, objAnimation);
        }

        private void ResetSpriteAnimatorPosition()
        {
            if (spriteAnimator != null)
                spriteAnimator.transform.localPosition = _spriteAnimatorBaseLocalPosition;
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

        public virtual void ChangeLife(float value)
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

        protected virtual void PrintLife()
        {
            Debug.Log("Life: "+life);
        }
    }
}

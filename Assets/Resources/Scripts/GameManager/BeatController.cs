using System.Collections;
using UnityEngine;

namespace Resources.Scripts
{
    public class BeatController : MonoBehaviour
    {
        public static BeatController instance;
        
        private float beatTimeCanPlayRest = 1;
        private float beatTimeCanPlaySum = 1;
        public float beatTimeInSeconds = 1;
        public int beatsToReachCenter = 2;
        private float timer = 0;

        public bool canRegisterPlay = false;

        public GameObject lineBeatPrefab;
        public GameObject lineBeatParent;

        public GameObject startLeftPosition;
        public GameObject startRightPosition;

        public GameObject heart;

        private void Awake()
        {
            instance = this;
        }

        private void Start()
        {
            beatTimeCanPlayRest = beatTimeInSeconds - 0.35f;
            beatTimeCanPlaySum = beatTimeInSeconds + 0.35f;

            CreateInitialBeatLineQueue();
        }

        private void Update()
        {
            timer += Time.deltaTime;

            canRegisterPlay = timer >= beatTimeCanPlayRest || timer <= beatTimeCanPlaySum;

            while (timer >= beatTimeInSeconds)
            {
                EventBus<BeatEvent>.Raise(new BeatEvent());
                StartCoroutine(GameManager.instance.Heartbeat(heart, 1.1f, 0.05f));
                timer -= beatTimeInSeconds;
                CreateBeatLines(0f);
            }
        }

        private void CreateInitialBeatLineQueue()
        {
            var lineCount = Mathf.Max(1, beatsToReachCenter);

            for (var i = 0; i < lineCount; i++)
            {
                CreateBeatLines(i * beatTimeInSeconds);
            }
        }

        private void CreateBeatLines(float initialMoveTime)
        {
            StartCoroutine(CreateLineBeat(startLeftPosition.transform.position, initialMoveTime));
            StartCoroutine(CreateLineBeat(startRightPosition.transform.position, initialMoveTime));
        }

        private IEnumerator CreateLineBeat(Vector2 startPos, float initialMoveTime)
        {
            GameObject lineBeat = Instantiate(lineBeatPrefab, lineBeatParent.transform);
            lineBeat.transform.position = startPos;

            var endPos = (Vector2)lineBeatParent.transform.position;
            var moveDuration = beatTimeInSeconds * Mathf.Max(1, beatsToReachCenter);
            var moveTimer = Mathf.Clamp(initialMoveTime, 0f, moveDuration);
            lineBeat.transform.position = Vector2.Lerp(startPos, endPos, moveTimer / moveDuration);
            
            while (moveTimer < moveDuration)
            {
                moveTimer += Time.deltaTime;

                float t = moveTimer / moveDuration;
                t = Mathf.Clamp01(t);

                Vector2 newPosition = Vector2.Lerp(startPos, endPos, t);
                lineBeat.transform.position = newPosition;

                yield return null;
            }

            lineBeat.transform.position = endPos;
            
            Destroy(lineBeat);
        }
    }
    
    public class BeatEvent : IEvent {}
}

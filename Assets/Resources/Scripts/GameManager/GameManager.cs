using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Resources.Scripts
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager instance;
        
        public int cantPlayerCoins = 5;
        public int coinsMultiplier = 1;

        public int cantKillsMultiplier = 0;
        
        public TextMeshProUGUI coinsText;
        public TextMeshProUGUI multiplierText;

        public Image lifeBarPlayer;
        public GameObject panelBomb;

        public GameObject globalPrefabCoinDrop;
        public GameObject globalPrefabBombDrop;
        public GameObject globalPrefabHit;

        public GameObject prefabLifeEnemy;

        public Image imageTransition;
        public GameObject pausePanel;

        public bool hasBomb = false;
        public bool isPaused = false;

        private void Awake()
        {
            instance = this;
        }

        private void Start()
        {
            StartCoroutine(StartGame());
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) PauseGame();
        }

        private void LateUpdate()
        {
            lifeBarPlayer.fillAmount = PlayerController.instance.life / PlayerController.instance.maxLife;
            panelBomb.SetActive(hasBomb);
        }

        public void SumCoins(int cantCoinsSum)
        {
            cantPlayerCoins += cantCoinsSum * coinsMultiplier;
            PlayerPrefs.SetInt("cantCoins", cantPlayerCoins);
            
            PrintCoins();
        }

        public void PrintCoins()
        {
            coinsText.text = cantPlayerCoins.ToString();
            StartCoroutine(Heartbeat(coinsText.gameObject, 1.1f, 0.05f));
        }

        public void SumKill(int cantKillValue)
        {
            cantKillsMultiplier += cantKillValue;

            var originalMultiplier = coinsMultiplier;
            
            if (cantKillsMultiplier == 1) coinsMultiplier = 2;
            if (cantKillsMultiplier == 5) coinsMultiplier = 3;

            if (originalMultiplier != coinsMultiplier)
                StartCoroutine(Heartbeat(multiplierText.gameObject, 1.1f, 0.05f));
            
            multiplierText.color = coinsMultiplier == 3 ? Color.red : Color.white;
            multiplierText.text = "x" + coinsMultiplier;
            
            multiplierText.gameObject.SetActive(true);
        }
        
        public void RestartMultiplier()
        {
            coinsMultiplier = 1;
            cantKillsMultiplier = 0;
            
            multiplierText.gameObject.SetActive(false);
        }
        
        public IEnumerator Heartbeat(GameObject target, float scaleMultiplier, float timePulse)
        {
            Vector3 originalScale = target.transform.localScale;
            Vector3 targetScale = originalScale * scaleMultiplier;

            yield return ScaleTo(target, targetScale, timePulse);
            yield return ScaleTo(target, originalScale, timePulse);
        }

        public IEnumerator ScaleTo(GameObject target, Vector3 targetScale, float duration)
        {
            Vector3 startScale = target.transform.localScale;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                target.transform.localScale = Vector3.Lerp(startScale, targetScale, t);

                yield return null;
            }

            target.transform.localScale = targetScale;
        }
        
        public IEnumerator Shake(GameObject target, float duration, float strength, int vibrations, Vector3 originalLocalPosition)
        {
            duration = Mathf.Max(0f, duration);
            strength = Mathf.Max(0f, strength);
            vibrations = Mathf.Max(1, vibrations);

            if (duration <= 0f || strength <= 0f)
            {
                target.transform.localPosition = originalLocalPosition;
                yield break;
            }

            float elapsed = 0f;
            float stepDuration = duration / vibrations;

            while (elapsed < duration)
            {
                float normalizedTime = elapsed / duration;
                float currentStrength = strength * (1f - normalizedTime);
                Vector2 offset = Random.insideUnitCircle * currentStrength;

                Vector3 startPosition = target.transform.localPosition;
                Vector3 targetPosition = originalLocalPosition + new Vector3(offset.x, offset.y, 0f);
                float stepElapsed = 0f;

                while (stepElapsed < stepDuration && elapsed < duration)
                {
                    stepElapsed += Time.deltaTime;
                    elapsed += Time.deltaTime;

                    float t = Mathf.Clamp01(stepElapsed / stepDuration);
                    target.transform.localPosition = Vector3.Lerp(startPosition, targetPosition, t);

                    yield return null;
                }
            }

            target.transform.localPosition = originalLocalPosition;
        }

        public DropBaseController CreateDrop(int amount, GameObject prefab, TileManager parentTile)
        {
            if (prefab == null || parentTile == null)
                return null;

            if (!parentTile.CanPlaceDrop())
            {
                Debug.LogWarning($"Cannot create drop {prefab.name} at {parentTile.indexPosition}: tile already has a drop.");
                return null;
            }

            GameObject dropObject = Instantiate(prefab);
            DropBaseController drop = dropObject.GetComponent<DropBaseController>();

            if (drop == null)
            {
                Debug.LogWarning($"{prefab.name} does not have a DropBaseController component.");
                Destroy(dropObject);
                return null;
            }

            drop.cantValue = amount;

            if (!drop.AssignToTile(parentTile))
            {
                Destroy(dropObject);
                return null;
            }

            drop.gameObject.SetActive(true);

            return drop;
        }

        public IEnumerator StartGame()
        {
            Time.timeScale = 1;
            SumCoins(PlayerPrefs.GetInt("cantCoins", 0));
            yield return FadeImage(imageTransition, 1, 0, 1);
        }

        public IEnumerator RestartLevel(bool sumLevel)
        {
            if (sumLevel)
                PlayerPrefs.SetInt("cantLevels", PlayerPrefs.GetInt("cantLevels", 0) + 1);
            
            Time.timeScale = 0;
            yield return FadeImage(imageTransition, 0, 1, 1);
            SceneManager.LoadScene("Game");
        }

        private void PauseGame()
        {
            isPaused = !isPaused;
            pausePanel.SetActive(isPaused);
            Time.timeScale = isPaused ? 0 : 1;
        }

        public IEnumerator FadeImage(Image imageFade, float startFade, float endFade, float duration)
        {
            float elapsed = 0f;
            
            Color colorImage = imageFade.color;
            colorImage.a = startFade;
            imageFade.color = colorImage;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                
                colorImage.a = Mathf.Lerp(startFade, endFade, t);
                imageFade.color = colorImage;

                yield return null;
            }

            colorImage.a = endFade;
            imageFade.color = colorImage;
        }
    }
}

using System;
using System.Collections;
using Resources.Scripts.Drops;
using TMPro;
using UnityEngine;
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

        public GameObject prefabCoinDrop;
        
        private Coroutine _shakeCoroutine;

        private void Awake()
        {
            instance = this;
        }

        private void LateUpdate()
        {
            lifeBarPlayer.fillAmount = (float)PlayerController.instance.life / PlayerController.instance.maxLife;
        }

        public void SumCoins(int cantCoinsSum)
        {
            cantPlayerCoins += cantCoinsSum * coinsMultiplier;
            
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
                _shakeCoroutine = null;
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
            _shakeCoroutine = null;
        }

        public void CreateCoinDrop(int amountCoins, TileManager parentTile)
        {
            GameObject coinPrefab = prefabCoinDrop != null
                ? prefabCoinDrop
                : MapTestGenerator.instance?.coinDropPrefab;

            if (coinPrefab == null)
            {
                Debug.LogWarning("No coin drop prefab assigned.");
                return;
            }

            GameObject coinObject = Instantiate(coinPrefab);
            CoinDropController coinDrop = coinObject.GetComponent<CoinDropController>();

            if (coinDrop == null)
            {
                Debug.LogWarning($"{coinPrefab.name} does not have a CoinDropController component.");
                Destroy(coinObject);
                return;
            }

            coinDrop.cantCoins = amountCoins;
            coinDrop.AssignToTile(parentTile);
            coinDrop.gameObject.SetActive(true);
        }
    }
}

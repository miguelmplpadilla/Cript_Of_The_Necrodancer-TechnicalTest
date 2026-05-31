using UnityEngine;
using UnityEngine.UI;

public class LifeBarEnemyController : MonoBehaviour
{
    public RectTransform barLife;
    public RectTransform barFront;

    public void SetUpLife(float life)
    {
        barLife.sizeDelta = new Vector2(0.15f * life, 0.12f);
        gameObject.SetActive(false);
    }
    
    public void ChangeLife(float life, float maxLife)
    {
        barFront.sizeDelta = new Vector2(0.15f * (maxLife - life), 0.12f);
    }
}

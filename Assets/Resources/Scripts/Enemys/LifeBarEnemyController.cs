using UnityEngine;
using UnityEngine.UI;

public class LifeBarEnemyController : MonoBehaviour
{
    public Image barLife;
    public Image barFront;

    public void SetUpLife(int life)
    {
        barLife.fillAmount = 0.15f * life;
        gameObject.SetActive(false);
    }
    
    public void ChangeLife(int life, int maxLife)
    {
        barFront.fillAmount = 0.15f * (maxLife - life);
    }
}

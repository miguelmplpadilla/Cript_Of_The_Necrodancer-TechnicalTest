using System.Collections;
using UnityEngine;

namespace Resources.Scripts.Drops
{
    public class StairsLevelController : DropBaseController
    {
        public override IEnumerator GetDropItem()
        {
            PlayerPrefs.SetFloat("playerLife", PlayerController.instance.life);
            yield return GameManager.instance.RestartLevel(false); //TODO: Set true for boss level
        }
    }
}

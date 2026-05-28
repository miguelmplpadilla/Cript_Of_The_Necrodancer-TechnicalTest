using System.Collections;
using UnityEngine;

namespace Resources.Scripts
{
    public class CameraController : MonoBehaviour
    {
        public static CameraController instance;

        [SerializeField] private float defaultShakeDuration = 0.15f;
        [SerializeField] private float defaultShakeStrength = 0.18f;
        [SerializeField] private int defaultShakeVibrations = 12;

        private Vector3 _originalLocalPosition;

        private void Awake()
        {
            instance = this;
            _originalLocalPosition = transform.localPosition;
            _originalLocalPosition.z = -15;
        }

        private void OnDisable()
        {
            transform.localPosition = _originalLocalPosition;
        }

        public void ShakeCamera()
        {
            ShakeCamera(defaultShakeDuration, defaultShakeStrength, defaultShakeVibrations);
        }

        public void ShakeCamera(float duration, float strength)
        {
            ShakeCamera(duration, strength, defaultShakeVibrations);
        }

        public void ShakeCamera(float duration, float strength, int vibrations)
        {
            transform.localPosition = _originalLocalPosition;
            StartCoroutine(GameManager.instance.Shake(gameObject, duration, strength, vibrations, _originalLocalPosition));
        }
    }
}

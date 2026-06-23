using UnityEngine;

namespace GlassSystem.Sample
{
    public class CameraShake : MonoBehaviour
    {
        public static CameraShake Instance { get; private set; }

        [Header("Shake Settings")]
        public float duration = 0.35f;

        [Range(0f, 0.1f)]
        public float magnitude = 0.025f;

        [Range(5f, 40f)]
        public float frequency = 20f;

        private float _elapsed;
        private bool _isShaking;
        private Vector3 _originalPosition;

        void Awake()
        {
            Instance = this;
        }

        public void Shake()
        {
            _originalPosition = transform.position;
            _elapsed = 0f;
            _isShaking = true;
        }

        private void LateUpdate()
        {
            if (!_isShaking) return;

            _elapsed += Time.unscaledDeltaTime;

            if (_elapsed >= duration)
            {
                _isShaking = false;
                transform.position = _originalPosition;
                return;
            }

            float progress = _elapsed / duration;

            // Smooth fade-out: full strength at start, eases to zero.
            float envelope = 1f - Mathf.SmoothStep(0f, 1f, progress);

            float x = Mathf.Sin(_elapsed * frequency * Mathf.PI * 2f)
                      * magnitude * envelope;

            float y = Mathf.Sin(_elapsed * frequency * 1.17f * Mathf.PI * 2f)
                      * magnitude * envelope * 0.55f;

            transform.position = _originalPosition + new Vector3(x, y, 0f);
        }
    }
}
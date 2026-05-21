using UnityEngine;

namespace GlassSystem.Sample
{
    public class CameraShake : MonoBehaviour
    {
        public static CameraShake Instance { get; private set; }

        [Header("Shake Settings")]
        public float duration  = 0.35f;
        [Range(0f, 0.1f)]
        public float magnitude = 0.025f;
        [Range(5f, 40f)]
        public float frequency = 20f;   // oscillations per second

        private float   _elapsed;
        private bool    _isShaking;

        void Awake() => Instance = this;

        public void Shake()
        {
            _elapsed   = 0f;
            _isShaking = true;
        }

        void LateUpdate()
        {
            if (!_isShaking) return;

            _elapsed += Time.deltaTime;

            if (_elapsed >= duration)
            {
                _isShaking = false;
                return;
            }

            // Smooth fade-out: full strength at start, eases to zero
            float envelope = 1f - Mathf.SmoothStep(0f, 1f, _elapsed / duration);

            // Two sine waves at slightly different frequencies give an organic
            // "beating" tremor instead of a mechanical single-frequency buzz
            float x = Mathf.Sin(_elapsed * frequency         * Mathf.PI * 2f) * magnitude * envelope;
            float y = Mathf.Sin(_elapsed * frequency * 1.17f * Mathf.PI * 2f) * magnitude * envelope * 0.55f;

            transform.position += new Vector3(x, y, 0f);
        }
    }
}

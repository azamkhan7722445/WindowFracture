using System.Collections;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Azam_data.Bomb
{
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(AudioSource))]
    public class TimerControllerCanvas : MonoBehaviour,IManagerInterface
    {
        [Header("UI References")] public TextMeshProUGUI timerText;
        public Image timerImage; // optional — drag Image for color shift

        [Header("Audio")] public AudioClip tickSound; // drag tick AudioClip here
        public AudioClip blastSound; // drag blast AudioClip here

        [Header("Canvas Shake  (last 20 s, escalates)")] [Range(0f, 50f)]
        public float shakeMagnitude = 15f;

        [Range(0f, 20f)] public float shakeSpeed = 6f;

        [Header("Camera Shake")] public Camera targetCamera;
        [Range(0f, 0.5f)] public float cameraShakeMagnitude = 0.05f;
        [Range(0f, 20f)] public float cameraShakeSpeed = 8f;
        [Range(1f, 100f)] public float blastShakeMultiplier = 8f;

        // ── public hooks for BombPostFX ────────────────────────────────
        public System.Action OnBlast;
        public float ElapsedTime => elapsedTime;
        public float ElapsedNormalized => Mathf.Clamp01(elapsedTime / duration);
        public float Duration => duration;

        [Header("Debug")]
        [ShowInInspector, ReadOnly] public float DebugElapsedTime => elapsedTime;
        [ShowInInspector, ReadOnly] public float DebugDuration => duration;
        [ShowInInspector, ReadOnly] public float DebugElapsedNormalized => ElapsedNormalized;

        // ── private state ──────────────────────────────────────────────
        float elapsedTime;
        float duration = 60f;
        bool blastTriggered;
        int lastTickSecond = -1;

        RectTransform canvasRect;
        Vector2 originalAnchoredPos;

        Vector3 cameraOriginalLocalPos;
        AudioSource audioSource;
        bool initialized;

        // ──────────────────────────────────────────────────────────────
        
        public IEnumerator Initialize()
        {
            EnsureInitialized();
            
            yield return null;
        }

        public IEnumerator PostInitialize()
        {
            yield return null;
        }

        public IEnumerator SetForGameplay()
        {
            yield return null;
        }

        void EnsureInitialized()
        {
            if (initialized)
                return;

            canvasRect = GetComponent<RectTransform>();
            if (canvasRect != null)
                originalAnchoredPos = canvasRect.anchoredPosition;

            if (targetCamera == null)
                targetCamera = Camera.main;
            if (targetCamera != null)
                cameraOriginalLocalPos = targetCamera.transform.localPosition;

            audioSource = GetComponent<AudioSource>();
            initialized = true;
        }

        public void SetTime(float elapsed, float timerDuration)
        {
            if (!initialized)
                EnsureInitialized();

            duration = Mathf.Max(0.01f, timerDuration);
            elapsedTime = Mathf.Clamp(elapsed, 0f, duration);
            float current = Mathf.Clamp(elapsedTime, 0f, duration);

            // ── Timer text  SS:MS (centiseconds) ──────────────────────
            int secs = Mathf.FloorToInt(current);
            int centis = Mathf.FloorToInt((current - secs) * 100f);
            if (timerText != null)
                timerText.text = $"{secs:00}:{centis:00}";

            // ── Color  white → red ────────────────────────────────────
            float intensity = current / duration;
            if (timerImage != null)
                timerImage.color = Color.Lerp(Color.white, Color.red, intensity);

            // ── Tick sound once per second ────────────────────────────
            int currentSecond = Mathf.FloorToInt(current);
            if (currentSecond != lastTickSecond && current > 0f && current < duration)
            {
                lastTickSecond = currentSecond;
                if (tickSound != null)
                    audioSource.PlayOneShot(tickSound);
            }

            // ── Canvas shake  (starts at 40 s, escalates to 60 s) ────
            if (current >= duration - 20f && canvasRect != null)
            {
                float progress = (current - (duration - 20f)) / 20f; // 0 → 1
                float mag = shakeMagnitude * progress;
                float spd = shakeSpeed * (1f + progress * 2f); // speed ramps too

                float ox = Mathf.Sin(Time.time * spd) * mag;
                float oy = Mathf.Cos(Time.time * spd * 0.73f) * mag;
                canvasRect.anchoredPosition = originalAnchoredPos + new Vector2(ox, oy);
            }

            // ── Camera shake  (mild, grows continuously with time) ────
            if (targetCamera != null)
            {
                float camMag = cameraShakeMagnitude * intensity;
                float camX = Mathf.Sin(Time.time * cameraShakeSpeed) * camMag;
                float camY = Mathf.Cos(Time.time * cameraShakeSpeed * 1.4f) * camMag;
                targetCamera.transform.localPosition =
                    cameraOriginalLocalPos + new Vector3(camX, camY, 0f);
            }
        }

        public void PlayBlastFeedback()
        {
            if (!initialized)
                EnsureInitialized();

            if (blastTriggered)
                return;

            blastTriggered = true;
            StopTimerFeedback();

            SetTime(duration, duration);

            if (canvasRect != null)
                canvasRect.anchoredPosition = originalAnchoredPos;

            if (blastSound != null)
                audioSource.PlayOneShot(blastSound);

            StartCoroutine(BlastCameraShake());
            OnBlast?.Invoke();
        }

        public void StopTimerFeedback()
        {
            if (!initialized)
                EnsureInitialized();

            if (canvasRect != null)
                canvasRect.anchoredPosition = originalAnchoredPos;

            if (targetCamera != null)
                targetCamera.transform.localPosition = cameraOriginalLocalPos;

            if (audioSource != null)
                audioSource.Stop();

            lastTickSecond = -1;
        }

        // ── Aggressive randomised camera shake after blast ─────────────
        IEnumerator BlastCameraShake()
        {
            if (targetCamera == null) yield break;

            float elapsed = 0f;
            float shakeDuration = 2.5f;
            float blastMag = cameraShakeMagnitude * blastShakeMultiplier;

            while (elapsed < shakeDuration)
            {
                elapsed += Time.deltaTime;
                float fade = 1f - (elapsed / shakeDuration);
                float mag = blastMag * fade;
                targetCamera.transform.localPosition = cameraOriginalLocalPos + new Vector3(
                    Random.Range(-1f, 1f) * mag,
                    Random.Range(-1f, 1f) * mag,
                    0f
                );
                yield return null;
            }

            targetCamera.transform.localPosition = cameraOriginalLocalPos;
        }

       
    }
}
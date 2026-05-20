using System.Collections;
using GlassSystem.Scripts;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(AudioSource))]
public class TimerControllerCanvas : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI timerText;
    public Image timerImage;                  // optional — drag Image for color shift

    [Header("Audio")]
    public AudioClip tickSound;               // drag tick AudioClip here
    public AudioClip blastSound;              // drag blast AudioClip here

    [Header("Timer Settings")]
    public float duration = 60f;

    [Header("Canvas Shake  (last 20 s, escalates)")]
    [Range(0f, 50f)] public float shakeMagnitude = 15f;
    [Range(0f, 20f)] public float shakeSpeed = 6f;

    [Header("Camera Shake")]
    public Camera targetCamera;
    [Range(0f, 0.5f)]  public float cameraShakeMagnitude = 0.05f;
    [Range(0f, 20f)]   public float cameraShakeSpeed = 8f;
    [Range(1f, 10f)]   public float blastShakeMultiplier = 8f;

    // ── public hooks for BombPostFX ────────────────────────────────
    public System.Action OnBlast;
    public float ElapsedTime       => elapsedTime;
    public float ElapsedNormalized => Mathf.Clamp01(elapsedTime / duration);
    public float Duration          => duration;

    // ── private state ──────────────────────────────────────────────
    float        elapsedTime;
    bool         isRunning      = true;
    bool         blastTriggered;
    int          lastTickSecond = -1;

    RectTransform canvasRect;
    Vector2       originalAnchoredPos;

    Vector3     cameraOriginalLocalPos;
    AudioSource audioSource;

    // ──────────────────────────────────────────────────────────────
    void Start()
    {
        canvasRect = GetComponent<RectTransform>();
        if (canvasRect != null)
            originalAnchoredPos = canvasRect.anchoredPosition;

        if (targetCamera == null)
            targetCamera = Camera.main;
        if (targetCamera != null)
            cameraOriginalLocalPos = targetCamera.transform.localPosition;

        audioSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        if (!isRunning) return;

        elapsedTime += Time.deltaTime;
        float current = Mathf.Clamp(elapsedTime, 0f, duration);

        // ── Timer text  SS:MS (centiseconds) ──────────────────────
        int secs   = Mathf.FloorToInt(current);
        int centis = Mathf.FloorToInt((current - secs) * 100f);
        if (timerText != null)
            timerText.text = $"{secs:00}:{centis:00}";

        // ── Color  white → red ────────────────────────────────────
        float intensity = current / duration;
        if (timerImage != null)
            timerImage.color = Color.Lerp(Color.white, Color.red, intensity);

        // ── Tick sound once per second ────────────────────────────
        int currentSecond = Mathf.FloorToInt(current);
        if (currentSecond != lastTickSecond)
        {
            lastTickSecond = currentSecond;
            if (tickSound != null)
                audioSource.PlayOneShot(tickSound);
        }

        // ── Canvas shake  (starts at 40 s, escalates to 60 s) ────
        if (current >= duration - 20f && canvasRect != null)
        {
            float progress = (current - (duration - 20f)) / 20f;          // 0 → 1
            float mag      = shakeMagnitude * progress;
            float spd      = shakeSpeed * (1f + progress * 2f);           // speed ramps too

            float ox = Mathf.Sin(Time.time * spd)         * mag;
            float oy = Mathf.Cos(Time.time * spd * 0.73f) * mag;
            canvasRect.anchoredPosition = originalAnchoredPos + new Vector2(ox, oy);
        }

        // ── Camera shake  (mild, grows continuously with time) ────
        if (targetCamera != null)
        {
            float camMag = cameraShakeMagnitude * intensity;
            float camX   = Mathf.Sin(Time.time * cameraShakeSpeed)        * camMag;
            float camY   = Mathf.Cos(Time.time * cameraShakeSpeed * 1.4f) * camMag;
            targetCamera.transform.localPosition =
                cameraOriginalLocalPos + new Vector3(camX, camY, 0f);
        }

        // ── BLAST at 60 s ─────────────────────────────────────────
        if (elapsedTime >= duration && !blastTriggered)
        {
            blastTriggered = true;
            isRunning      = false;

            if (timerText != null)
                timerText.text = "60:00";

            if (canvasRect != null)
                canvasRect.anchoredPosition = originalAnchoredPos;

            if (blastSound != null)
                audioSource.PlayOneShot(blastSound);

            ShatterAllGlass();
            StartCoroutine(BlastCameraShake());
            OnBlast?.Invoke();
        }
    }

    // ── Shatter every GlassPanel in the scene ─────────────────────
    void ShatterAllGlass()
    {
        var panels = FindObjectsByType<GlassPanel>(FindObjectsSortMode.None);
        foreach (var glass in panels)
        {
            try
            {
                var mf     = glass.GetComponent<MeshFilter>();
                float frontZ = mf != null ? mf.sharedMesh.bounds.max.z : 0f;
                Vector3 breakPos = glass.transform.TransformPoint(new Vector3(0f, 0f, frontZ));
                glass.Break(breakPos, -glass.transform.forward * 5000f);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Glass shatter skipped: {e.Message}");
            }
        }
    }

    // ── Aggressive randomised camera shake after blast ─────────────
    IEnumerator BlastCameraShake()
    {
        if (targetCamera == null) yield break;

        float elapsed       = 0f;
        float shakeDuration = 2.5f;
        float blastMag      = cameraShakeMagnitude * blastShakeMultiplier;

        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;
            float fade = 1f - (elapsed / shakeDuration);
            float mag  = blastMag * fade;
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

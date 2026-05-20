using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Attach to a GameObject that also has a Volume component.
/// Drives URP post-processing in sync with the bomb timer,
/// then fires a blast FX sequence and fades the screen to black.
/// </summary>
[RequireComponent(typeof(Volume))]
public class BombPostFX : MonoBehaviour
{
    [Header("Timer Reference")]
    public TimerControllerCanvas timer;     // drag your Canvas (TimerControllerCanvas)

    [Header("Black Screen Fade")]
    public Image blackOverlay;              // full-screen black Image child of Canvas
    [Range(0.5f, 8f)]  public float fadeDuration = 4f;
    [Range(0f,   5f)]  public float fadeDelay    = 1.5f;

    [Header("Blast Bloom Burst")]
    [Range(2f,  20f)]  public float blastBloomPeak    = 12f;
    [Range(0.1f, 3f)]  public float blastBloomFalloff = 1.2f;

    // ── URP effect references ──────────────────────────────────────
    Vignette            _vignette;
    Bloom               _bloom;
    ChromaticAberration _chromatic;
    ColorAdjustments    _colorAdj;
    LensDistortion      _lensDist;
    MotionBlur          _motionBlur;

    Volume _volume;
    bool   _timerDone;

    // ──────────────────────────────────────────────────────────────
    void Start()
    {
        _volume          = GetComponent<Volume>();
        _volume.isGlobal = true;
        _volume.priority = 10f;    // wins over scene volumes

        // Runtime-only profile — never touches any asset on disk
        var profile        = ScriptableObject.CreateInstance<VolumeProfile>();
        _volume.sharedProfile = profile;

        // Vignette — darkens edges, grows with time
        _vignette                  = profile.Add<Vignette>(true);
        _vignette.color.value      = Color.black;
        _vignette.intensity.value  = 0.2f;
        _vignette.smoothness.value = 0.4f;

        // Bloom — idle, surges on blast
        _bloom               = profile.Add<Bloom>(true);
        _bloom.threshold.value = 0.9f;
        _bloom.intensity.value = 0f;      // our volume adds on top of scene bloom
        _bloom.scatter.value   = 0.5f;
        _bloom.active          = false;

        // Chromatic aberration — last 20 s
        _chromatic               = profile.Add<ChromaticAberration>(true);
        _chromatic.intensity.value = 0f;

        // Color adjustments — desaturate + warm tint over time
        _colorAdj                     = profile.Add<ColorAdjustments>(true);
        _colorAdj.colorFilter.value   = Color.white;
        _colorAdj.saturation.value    = 0f;
        _colorAdj.postExposure.value  = 0f;

        // Lens distortion — blast impact
        _lensDist               = profile.Add<LensDistortion>(true);
        _lensDist.intensity.value = 0f;
        _lensDist.active          = false;

        // Motion blur — brief window around blast
        _motionBlur               = profile.Add<MotionBlur>(true);
        _motionBlur.mode.value    = MotionBlurMode.CameraOnly;
        _motionBlur.quality.value = MotionBlurQuality.High;
        _motionBlur.intensity.value = 0.6f;
        _motionBlur.active          = false;

        if (blackOverlay != null)
        {
            blackOverlay.color          = new Color(0f, 0f, 0f, 0f);
            blackOverlay.raycastTarget  = false;
        }

        if (timer != null)
            timer.OnBlast += HandleBlast;
    }

    void OnDestroy()
    {
        if (timer != null)
            timer.OnBlast -= HandleBlast;
    }

    // ── Per-frame progressive FX ───────────────────────────────────
    void Update()
    {
        if (timer == null || _timerDone) return;

        float t       = timer.ElapsedNormalized;  // 0 → 1
        float elapsed = timer.ElapsedTime;
        float dur     = timer.Duration;

        // Vignette intensifies: 0.2 → 0.55
        _vignette.intensity.value = Mathf.Lerp(0.2f, 0.55f, t);

        // Color: warm + desaturate
        _colorAdj.saturation.value  = Mathf.Lerp(0f, -25f, t);
        _colorAdj.colorFilter.value = Color.Lerp(Color.white, new Color(1f, 0.82f, 0.55f), t);

        // Chromatic aberration: enters last 20 s, ramps 0 → 0.5
        if (elapsed >= dur - 20f)
        {
            float p = (elapsed - (dur - 20f)) / 20f;
            _chromatic.intensity.value = Mathf.Lerp(0f, 0.5f, p);
        }
    }

    // ── Blast handler ──────────────────────────────────────────────
    void HandleBlast()
    {
        _timerDone = true;
        StartCoroutine(BlastSequence());
        StartCoroutine(FadeToBlack());
    }

    // ── Post-FX burst: spike → falloff ────────────────────────────
    IEnumerator BlastSequence()
    {
        _bloom.active      = true;
        _lensDist.active   = true;
        _motionBlur.active = true;

        // --- Spike (0.12 s) ---
        float elapsed = 0f, spikeDur = 0.12f;
        while (elapsed < spikeDur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / spikeDur;

            _bloom.intensity.value       = Mathf.Lerp(0f,    blastBloomPeak, t);
            _vignette.intensity.value    = Mathf.Lerp(0.55f, 0.90f,          t);
            _chromatic.intensity.value   = Mathf.Lerp(0.5f,  1f,             t);
            _lensDist.intensity.value    = Mathf.Lerp(0f,   -0.65f,          t);
            _colorAdj.postExposure.value = Mathf.Lerp(0f,    3f,             t);
            yield return null;
        }

        // --- Falloff ---
        elapsed = 0f;
        while (elapsed < blastBloomFalloff)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / blastBloomFalloff;

            _bloom.intensity.value       = Mathf.Lerp(blastBloomPeak, 0.25f, t);
            _vignette.intensity.value    = Mathf.Lerp(0.90f,  0.65f,  t);
            _chromatic.intensity.value   = Mathf.Lerp(1f,     0.15f,  t);
            _lensDist.intensity.value    = Mathf.Lerp(-0.65f, 0f,     t);
            _colorAdj.postExposure.value = Mathf.Lerp(3f,     0f,     t);
            yield return null;
        }

        _motionBlur.active = false;
        _lensDist.active   = false;
    }

    // ── Slow black vignette screen cover ──────────────────────────
    IEnumerator FadeToBlack()
    {
        if (blackOverlay == null) yield break;

        yield return new WaitForSeconds(fadeDelay);

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            blackOverlay.color = new Color(0f, 0f, 0f,
                Mathf.SmoothStep(0f, 1f, elapsed / fadeDuration));
            yield return null;
        }
        blackOverlay.color = Color.black;
    }
}

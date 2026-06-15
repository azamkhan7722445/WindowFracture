using System.Collections;
using Azam_data.Bomb;
using Sirenix.OdinInspector;
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
    public bool enableBlackFadeOnBlast = false;
    [Range(0.5f, 8f)]  public float fadeDuration = 4f;
    [Range(0f,   5f)]  public float fadeDelay    = 1.5f;

    [Header("Blast Bloom Burst")]
    [Range(2f,  20f)]  public float blastBloomPeak    = 12f;
    [Range(0.1f, 3f)]  public float blastBloomFalloff = 1.2f;

    [Header("Blast Post FX Strength")]
    [Range(0f, 1f)] public float blastEffectStrength = 1f;

    // ── URP effect references ──────────────────────────────────────
    Vignette            _vignette;
    Bloom               _bloom;
    ChromaticAberration _chromatic;
    ColorAdjustments    _colorAdj;
    LensDistortion      _lensDist;
    MotionBlur          _motionBlur;

    Volume _volume;
    VolumeProfile _runtimeProfile;
    bool   _timerDone;
    bool   _initialized;
    Coroutine _fadeCoroutine;
    bool _subscribedToTimer;

    [Header("Debug")]
    [ShowInInspector, ReadOnly] bool IsTimerHooked => timer != null;
    [ShowInInspector, ReadOnly] bool HasReceivedBlast => _timerDone;

    // ──────────────────────────────────────────────────────────────
    void OnEnable()
    {
        EnsureInitialized();
    }

    void Start()
    {
        EnsureInitialized();
    }

    void EnsureInitialized()
    {
        if (_initialized)
            return;

        _volume          = GetComponent<Volume>();
        _volume.isGlobal = true;
        _volume.priority = 10f;    // wins over scene volumes

        // Runtime-only profile - never touches any asset on disk.
        _runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        _runtimeProfile.hideFlags = HideFlags.HideAndDontSave;
        _volume.sharedProfile = _runtimeProfile;

        // Vignette — darkens edges, grows with time
        _vignette                  = _runtimeProfile.Add<Vignette>(true);
        _vignette.color.value      = Color.black;
        _vignette.intensity.value  = 0.2f;
        _vignette.smoothness.value = 0.4f;

        // Bloom — idle, surges on blast
        _bloom               = _runtimeProfile.Add<Bloom>(true);
        _bloom.threshold.value = 0.9f;
        _bloom.intensity.value = 0f;      // our volume adds on top of scene bloom
        _bloom.scatter.value   = 0.5f;
        _bloom.active          = false;

        // Chromatic aberration — last 20 s
        _chromatic               = _runtimeProfile.Add<ChromaticAberration>(true);
        _chromatic.intensity.value = 0f;

        // Color adjustments — desaturate + warm tint over time
        _colorAdj                     = _runtimeProfile.Add<ColorAdjustments>(true);
        _colorAdj.colorFilter.value   = Color.white;
        _colorAdj.saturation.value    = 0f;
        _colorAdj.postExposure.value  = 0f;

        // Lens distortion — blast impact
        _lensDist               = _runtimeProfile.Add<LensDistortion>(true);
        _lensDist.intensity.value = 0f;
        _lensDist.active          = false;

        // Motion blur — brief window around blast
        _motionBlur               = _runtimeProfile.Add<MotionBlur>(true);
        _motionBlur.mode.value    = MotionBlurMode.CameraOnly;
        _motionBlur.quality.value = MotionBlurQuality.High;
        _motionBlur.intensity.value = 0.6f;
        _motionBlur.active          = false;

        ClearBlackOverlay();

        if (timer == null)
            timer = FindFirstObjectByType<TimerControllerCanvas>();

        if (timer != null)
        {
            timer.OnBlast += HandleBlast;
            _subscribedToTimer = true;
        }
        else
        {
            Debug.LogWarning($"{nameof(BombPostFX)} could not find a {nameof(TimerControllerCanvas)}.", this);
        }

        _initialized = true;
    }

    void OnDestroy()
    {
        Cleanup();
    }

    void OnDisable()
    {
        Cleanup();
    }

    private void Cleanup()
    {
        StopAllCoroutines();
        _fadeCoroutine = null;

        if (_subscribedToTimer && timer != null)
        {
            timer.OnBlast -= HandleBlast;
            _subscribedToTimer = false;
        }

        if (_volume != null)
            _volume.sharedProfile = null;

        if (_runtimeProfile != null)
        {
            if (Application.isPlaying)
                Destroy(_runtimeProfile);
            else
                DestroyImmediate(_runtimeProfile);

            _runtimeProfile = null;
        }

        _initialized = false;
    }

    // ── Per-frame progressive FX ───────────────────────────────────
    void Update()
    {
        if (!enableBlackFadeOnBlast)
            ClearBlackOverlay();

        if (timer == null || _timerDone) return;

        float t       = timer.ElapsedNormalized;  // 0 → 1
        float elapsed = timer.ElapsedTime;
        float dur     = timer.Duration;

        // Vignette intensifies: 0.2 -> 0.55
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
        PlayBlastEffect();
    }

    public void PlayBlastEffect()
    {
        if (!_initialized)
            EnsureInitialized();

        _timerDone = true;
        StartCoroutine(BlastSequence());

        if (enableBlackFadeOnBlast)
        {
            _fadeCoroutine = StartCoroutine(FadeToBlack());
        }
        else
        {
            StopFadeToBlack();
            ClearBlackOverlay();
        }
    }

    private void StopFadeToBlack()
    {
        if (_fadeCoroutine == null)
            return;

        StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = null;
    }

    private void ClearBlackOverlay()
    {
        if (blackOverlay == null)
            return;

        blackOverlay.enabled = false;
        blackOverlay.color = new Color(0f, 0f, 0f, 0f);
        blackOverlay.raycastTarget = false;
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

            float strength = blastEffectStrength;
            _bloom.intensity.value       = Mathf.Lerp(0f,    blastBloomPeak * strength, t);
            _vignette.intensity.value    = Mathf.Lerp(0.55f, Mathf.Lerp(0.55f, 0.90f, strength), t);
            _chromatic.intensity.value   = Mathf.Lerp(0.5f,  Mathf.Lerp(0.5f,  1f,    strength), t);
            _lensDist.intensity.value    = Mathf.Lerp(0f,   -0.65f * strength, t);
            _colorAdj.postExposure.value = Mathf.Lerp(0f,    3f * strength,    t);
            yield return null;
        }

        // --- Falloff ---
        elapsed = 0f;
        while (elapsed < blastBloomFalloff)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / blastBloomFalloff;

            float strength = blastEffectStrength;
            _bloom.intensity.value       = Mathf.Lerp(blastBloomPeak * strength, 0.1f, t);
            _vignette.intensity.value    = Mathf.Lerp(Mathf.Lerp(0.55f, 0.90f, strength), 0f, t);
            _chromatic.intensity.value   = Mathf.Lerp(Mathf.Lerp(0.5f,  1f,    strength), 0f, t);
            _lensDist.intensity.value    = Mathf.Lerp(-0.65f * strength, 0f, t);
            _colorAdj.postExposure.value = Mathf.Lerp(3f * strength, 0f, t);
            yield return null;
        }

        _vignette.intensity.value = 0f;
        _chromatic.intensity.value = 0f;
        _colorAdj.postExposure.value = 0f;
        _bloom.intensity.value = 0f;
        _motionBlur.active = false;
        _lensDist.active   = false;
    }

    // ── Slow black vignette screen cover ──────────────────────────
    IEnumerator FadeToBlack()
    {
        if (blackOverlay == null) yield break;

        blackOverlay.enabled = true;
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
        _fadeCoroutine = null;
    }
}

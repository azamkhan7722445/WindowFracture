using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PaintSoundGenerator : MonoBehaviour
{
    [Header("Refs")] [SerializeField] private Paint_Effect paintScript;

    [Header("Sound")] [SerializeField] private AudioClip paintSound;
    [SerializeField, Range(0f, 1f)] private float maxVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] private float stationaryVolume = 0f;
    [SerializeField, Range(0.1f, 3f)] private float minPitch = 0.85f;
    [SerializeField, Range(0.1f, 3f)] private float maxPitch = 1.2f;

    [Header("Swipe Response")] [SerializeField]
    private float screenSpeedForMaxSound = 1200f;

    [SerializeField] private float minScreenSpeedToPlay = 20f;
    [SerializeField] private float responseSpeed = 12f;

    private AudioSource _audioSource;
    private float _targetVolume;
    private float _targetPitch = 1f;

    void Reset()
    {
        paintScript = GetComponent<Paint_Effect>();
    }

    void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        ConfigureAudioSource();
    }

    void OnEnable()
    {
        if (paintScript == null)
            paintScript = GetComponent<Paint_Effect>();

        if (paintScript != null)
            paintScript.PaintInputChanged += HandlePaintInputChanged;
    }

    void OnDisable()
    {
        if (paintScript != null)
            paintScript.PaintInputChanged -= HandlePaintInputChanged;

        StopSound();
    }

    void Update()
    {
        if (_audioSource == null || !_audioSource.isPlaying)
            return;

        float t = 1f - Mathf.Exp(-responseSpeed * Time.deltaTime);
        _audioSource.volume = Mathf.Lerp(_audioSource.volume, _targetVolume, t);
        _audioSource.pitch = Mathf.Lerp(_audioSource.pitch, _targetPitch, t);
    }

    void HandlePaintInputChanged(Paint_Effect.PaintInputData input)
    {
        if (!input.IsPainting || paintSound == null)
        {
            StopSound();
            return;
        }

        float speedForMaxSound = Mathf.Max(1f, screenSpeedForMaxSound);
        float speed01 = Mathf.Clamp01(input.ScreenSpeed / speedForMaxSound);
        bool isSwiping = input.ScreenSpeed >= minScreenSpeedToPlay;

        _targetVolume = isSwiping ? Mathf.Lerp(stationaryVolume, maxVolume, speed01) : stationaryVolume;
        _targetPitch = Mathf.Lerp(minPitch, maxPitch, speed01);

        if (!_audioSource.isPlaying || _audioSource.clip != paintSound)
            PlaySound();
    }

    void PlaySound()
    {
        ConfigureAudioSource();

        _audioSource.clip = paintSound;
        _audioSource.volume = _targetVolume;
        _audioSource.pitch = _targetPitch;
        _audioSource.Play();
    }

    void StopSound()
    {
        _targetVolume = 0f;

        if (_audioSource != null && _audioSource.isPlaying)
            _audioSource.Stop();
    }

    void ConfigureAudioSource()
    {
        if (_audioSource == null)
            return;

        _audioSource.loop = true;
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0f;
    }
}
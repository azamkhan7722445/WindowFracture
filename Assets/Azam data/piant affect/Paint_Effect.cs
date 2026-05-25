using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Renderer))]
public class Paint_Effect : MonoBehaviour
{
    [Header("Textures")]
    [Tooltip("Assign a white crumpled paper texture here.")]
    public Texture2D paperTexture;

    [Tooltip("Resolution of the paint RenderTexture (width = height).")]
    public int canvasResolution = 1024;

    [Header("Brush")]
    [Range(0.005f, 0.15f)] public float brushSize = 0.03f;
    public Color brushColor = Color.black;
    [Range(0f, 1f)] public float brushHardness = 0.8f;

    [Tooltip("Optional custom brush shape. Drag any greyscale texture here - white = full paint, black = no paint.")]
    public Texture2D brushTexture;

    [Header("Brush Shader")]
    [Tooltip("Drag BrushStampShader here so it is guaranteed included in builds.")]
    public Shader brushStampShader;

    [Header("Sounds Optional")]
    [Tooltip("Played in a loop while the user is painting.")]
    public AudioClip paintSound;

    [Range(0f, 1f)] public float soundVolume = 0.7f;

    [Header("Level Complete")]
    [SerializeField, Range(0f, 100f)] private float levelCompletePercent = 90f;
    [SerializeField] private GameObject levelCompleteUI;

    [Tooltip("How often paint percentage is checked while painting.")]
    [SerializeField] private float percentageCheckInterval = 0.25f;

    [Tooltip("Pixels with alpha above this value count as painted.")]
    [SerializeField, Range(0f, 1f)] private float paintedAlphaThreshold = 0.1f;

    public float PaintedPercentage { get; private set; }

    RenderTexture _paintRT;
    RenderTexture _tempRT;
    Material _surfaceMat;
    Material _brushMat;
    Camera _cam;
    MeshCollider _col;

    bool _wasPainting;
    Vector2 _prevUV;
    AudioSource _audioSource;

    Texture2D _percentageReadTexture;
    float _nextPercentageCheckTime;
    bool _levelCompleteShown;

    static readonly int ID_PaintTex = Shader.PropertyToID("_PaintTexture");
    static readonly int ID_BrushUV = Shader.PropertyToID("_BrushUV");
    static readonly int ID_BrushSize = Shader.PropertyToID("_BrushSize");
    static readonly int ID_BrushCol = Shader.PropertyToID("_BrushColor");
    static readonly int ID_BrushHard = Shader.PropertyToID("_BrushHardness");
    static readonly int ID_EraseMode = Shader.PropertyToID("_EraseMode");
    static readonly int ID_PaperTex = Shader.PropertyToID("_PaperTexture");
    static readonly int ID_BrushTex = Shader.PropertyToID("_BrushTexture");
    static readonly int ID_UseBrushTx = Shader.PropertyToID("_UseBrushTex");

    void Start()
    {
        _cam = Camera.main;

        if (levelCompleteUI != null)
            levelCompleteUI.SetActive(false);

        _col = GetComponent<MeshCollider>();
        if (_col == null)
            _col = gameObject.AddComponent<MeshCollider>();

        var mf = GetComponent<MeshFilter>();
        if (mf != null && _col.sharedMesh == null)
            _col.sharedMesh = mf.sharedMesh;

        _paintRT = CreateRT(canvasResolution);
        _tempRT = CreateRT(canvasResolution);
        ClearToTransparent(_paintRT);

        _percentageReadTexture = new Texture2D(
            canvasResolution,
            canvasResolution,
            TextureFormat.RGBA32,
            false
        );

        _surfaceMat = GetComponent<Renderer>().material;

        if (paperTexture != null)
            _surfaceMat.SetTexture(ID_PaperTex, paperTexture);

        _surfaceMat.SetTexture(ID_PaintTex, _paintRT);

        // Use directly assigned shader first — guaranteed included in build.
        // Fall back to Shader.Find only in editor where stripping doesn't apply.
        var brushShader = brushStampShader != null ? brushStampShader : Shader.Find("Hidden/BrushStamp");
        if (brushShader == null)
        {
            Debug.LogError("[Paint_Effect] BrushStamp shader not found. Assign it to the 'Brush Stamp Shader' field in the Inspector.");
            enabled = false;
            return;
        }

        _brushMat = new Material(brushShader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.loop = true;
        _audioSource.playOnAwake = false;
        _audioSource.volume = soundVolume;
        _audioSource.spatialBlend = 0f;
    }

    bool GetInputPosition(out Vector2 screenPos, out int fingerId)
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            TouchPhase phase = touch.phase;
            if (phase == TouchPhase.Began || phase == TouchPhase.Moved || phase == TouchPhase.Stationary)
            {
                // Skip if finger is over a UI element
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                {
                    screenPos = Vector2.zero;
                    fingerId = -1;
                    return false;
                }
                screenPos = touch.position;
                fingerId = touch.fingerId;
                return true;
            }
        }
        else if (Input.GetMouseButton(0))
        {
            // Skip if mouse is over a UI element (editor)
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                screenPos = Vector2.zero;
                fingerId = -1;
                return false;
            }
            screenPos = Input.mousePosition;
            fingerId = -1;
            return true;
        }

        screenPos = Vector2.zero;
        fingerId = -1;
        return false;
    }

    void Update()
    {
        if (_brushMat == null || _cam == null)
            return;

        if (!GetInputPosition(out Vector2 inputPos, out _) || !CanShoot)
        {
            StopBrushSound();
            _wasPainting = false;
            return;
        }

        Ray ray = _cam.ScreenPointToRay(inputPos);

        if (!Physics.Raycast(ray, out RaycastHit hit) || hit.collider != _col)
        {
            StopBrushSound();
            _wasPainting = false;
            return;
        }

        PlayBrushSound();

        Vector2 uv = hit.textureCoord;

        if (_wasPainting)
            StampAlongPath(_prevUV, uv);
        else
            Stamp(uv);

        _prevUV = uv;
        _wasPainting = true;

        CheckPaintPercentageTimer();
    }

    void PlayBrushSound()
    {
        if (_audioSource == null || paintSound == null)
        {
            StopBrushSound();
            return;
        }

        if (_audioSource.clip != paintSound || !_audioSource.isPlaying)
        {
            _audioSource.clip = paintSound;
            _audioSource.volume = soundVolume;
            _audioSource.Play();
        }
    }

    void StopBrushSound()
    {
        if (_audioSource != null && _audioSource.isPlaying)
            _audioSource.Stop();
    }

    void StampAlongPath(Vector2 from, Vector2 to)
    {
        float step = brushSize * (brushTexture != null ? 0.15f : 0.35f);
        int count = Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(from, to) / step), 1, 32);

        for (int i = 0; i <= count; i++)
            Stamp(Vector2.Lerp(from, to, (float)i / count));
    }

    void Stamp(Vector2 uv)
    {
        _brushMat.SetVector(ID_BrushUV, new Vector4(uv.x, uv.y, 0f, 0f));
        _brushMat.SetFloat(ID_BrushSize, brushSize);
        _brushMat.SetColor(ID_BrushCol, brushColor);
        _brushMat.SetFloat(ID_BrushHard, brushHardness);
        _brushMat.SetFloat(ID_EraseMode, 0f);

        bool hasTex = brushTexture != null;

        _brushMat.SetFloat(ID_UseBrushTx, hasTex ? 1f : 0f);

        if (hasTex)
            _brushMat.SetTexture(ID_BrushTex, brushTexture);

        Graphics.Blit(_paintRT, _tempRT, _brushMat);
        Graphics.Blit(_tempRT, _paintRT);
    }

    void CheckPaintPercentageTimer()
    {
        if (Time.time < _nextPercentageCheckTime)
            return;

        _nextPercentageCheckTime = Time.time + percentageCheckInterval;
        UpdatePaintedPercentage();
    }

    void UpdatePaintedPercentage()
    {
        if (_paintRT == null || _percentageReadTexture == null)
            return;

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = _paintRT;

        _percentageReadTexture.ReadPixels(
            new Rect(0, 0, canvasResolution, canvasResolution),
            0,
            0
        );

        _percentageReadTexture.Apply();

        RenderTexture.active = previous;

        Color32[] pixels = _percentageReadTexture.GetPixels32();

        int paintedPixels = 0;
        byte alphaLimit = (byte)(paintedAlphaThreshold * 255f);

        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i].a > alphaLimit)
                paintedPixels++;
        }

        PaintedPercentage = (paintedPixels / (float)pixels.Length) * 100f;

        Debug.Log($"Paint completed: {PaintedPercentage:0.0}%");

        CheckLevelComplete();
    }

    void CheckLevelComplete()
    {
        if (_levelCompleteShown)
            return;

        if (PaintedPercentage >= levelCompletePercent)
        {
            _levelCompleteShown = true;
            ShowLevelComplete();
        }
    }

    void ShowLevelComplete()
    {
        Debug.Log("Level Complete");

        if (levelCompleteUI != null)
            levelCompleteUI.SetActive(true);
    }

    void OnDestroy()
    {
        if (_paintRT != null)
        {
            _paintRT.Release();
            Destroy(_paintRT);
        }

        if (_tempRT != null)
        {
            _tempRT.Release();
            Destroy(_tempRT);
        }

        if (_brushMat != null)
            Destroy(_brushMat);

        if (_percentageReadTexture != null)
            Destroy(_percentageReadTexture);
    }

    static RenderTexture CreateRT(int size)
    {
        var rt = new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32);
        rt.filterMode = FilterMode.Bilinear;
        rt.Create();
        return rt;
    }

    static void ClearToTransparent(RenderTexture rt)
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = previous;
    }
    private bool CanShoot = true;

    public IEnumerator ActiveDeplay(float time = 0, bool _canShoot = false)
    {
        yield return new WaitForSeconds(time);
        CanShoot = _canShoot;
    }

    public void SetAction(bool canShoot) => StartCoroutine(ActiveDeplay(canShoot ? 0.5f : 0, canShoot));
}
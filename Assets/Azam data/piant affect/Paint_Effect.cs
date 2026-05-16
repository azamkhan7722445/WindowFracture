using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Attach to any GameObject with a Renderer + MeshFilter.
/// Assign a Material using Custom/PaperPaint shader in the Inspector.
/// The script creates a RenderTexture canvas, handles mouse/touch painting,
/// and drives the material's _PaintTexture in real time.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class Paint_Effect : MonoBehaviour
{
    [Header("Textures")]
    [Tooltip("Assign a white crumpled paper texture here.")]
    public Texture2D paperTexture;
    [Tooltip("Resolution of the paint RenderTexture (width = height).")]
    public int canvasResolution = 1024;

    [Header("Brush")]
    [Range(0.005f, 0.15f)] public float     brushSize     = 0.03f;
    public                        Color     brushColor     = Color.black;
    [Range(0f, 1f)]        public float     brushHardness  = 0.8f;
    [Tooltip("Optional custom brush shape. Drag any greyscale texture here — " +
             "white = full paint, black = no paint. Leave empty for a clean circle.")]
    public                        Texture2D brushTexture;

    [Header("Mode")]
    public bool eraseMode = false;

    [Header("UI (optional — auto-created if left empty)")]
    [Tooltip("Assign an existing Button to use as the Paint/Erase toggle.")]
    public Button toggleButton;

    [Header("Sounds (optional)")]
    [Tooltip("Played in a loop while the user is painting.")]
    public AudioClip paintSound;
    [Tooltip("Played in a loop while the user is erasing.")]
    public AudioClip eraseSound;
    [Range(0f, 1f)] public float soundVolume = 0.7f;

    // ── Internals ─────────────────────────────────────────────────────────────

    RenderTexture _paintRT;
    RenderTexture _tempRT;
    Material      _surfaceMat;
    Material      _brushMat;
    Camera        _cam;
    MeshCollider  _col;

    bool        _wasPainting;
    Vector2     _prevUV;
    Text        _btnLabel;
    AudioSource _audioSource;
    bool        _wasErasing;

    static readonly int ID_PaintTex    = Shader.PropertyToID("_PaintTexture");
    static readonly int ID_BrushUV    = Shader.PropertyToID("_BrushUV");
    static readonly int ID_BrushSize  = Shader.PropertyToID("_BrushSize");
    static readonly int ID_BrushCol   = Shader.PropertyToID("_BrushColor");
    static readonly int ID_BrushHard  = Shader.PropertyToID("_BrushHardness");
    static readonly int ID_EraseMode  = Shader.PropertyToID("_EraseMode");
    static readonly int ID_PaperTex   = Shader.PropertyToID("_PaperTexture");
    static readonly int ID_BrushTex   = Shader.PropertyToID("_BrushTexture");
    static readonly int ID_UseBrushTx = Shader.PropertyToID("_UseBrushTex");

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        _cam = Camera.main;

        // MeshCollider required for hit.textureCoord UV lookup
        _col = GetComponent<MeshCollider>();
        if (_col == null) _col = gameObject.AddComponent<MeshCollider>();
        var mf = GetComponent<MeshFilter>();
        if (mf != null && _col.sharedMesh == null)
            _col.sharedMesh = mf.sharedMesh;

        // Paint canvas RenderTextures (ARGB32: alpha encodes stroke coverage)
        _paintRT = CreateRT(canvasResolution);
        _tempRT  = CreateRT(canvasResolution);
        ClearToTransparent(_paintRT);

        // Surface material — must use Custom/PaperPaint shader
        _surfaceMat = GetComponent<Renderer>().material;
        if (paperTexture != null)
            _surfaceMat.SetTexture(ID_PaperTex, paperTexture);
        _surfaceMat.SetTexture(ID_PaintTex, _paintRT);

        // Brush blit material
        var brushShader = Shader.Find("Hidden/BrushStamp");
        if (brushShader == null)
        {
            Debug.LogError("[Paint_Effect] Hidden/BrushStamp shader not found. " +
                           "Make sure BrushStampShader.shader is in the project.");
            enabled = false;
            return;
        }
        _brushMat = new Material(brushShader) { hideFlags = HideFlags.HideAndDontSave };

        // AudioSource for paint/erase sounds
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.loop        = true;
        _audioSource.playOnAwake = false;
        _audioSource.volume      = soundVolume;
        _audioSource.spatialBlend = 0f;  // 2D sound

        BuildUI();
    }

    void Update()
    {
        if (_brushMat == null) return;

        bool leftBtn  = Input.GetMouseButton(0);
        bool rightBtn = Input.GetMouseButton(1);

        if (!leftBtn && !rightBtn)
        {
            StopBrushSound();
            _wasPainting = false;
            return;
        }

        bool erasing = eraseMode || rightBtn;

        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit) || hit.collider != _col)
        {
            StopBrushSound();
            _wasPainting = false;
            return;
        }

        PlayBrushSound(erasing);

        Vector2 uv = hit.textureCoord;

        // Interpolate stamps between frames to avoid gaps in fast strokes
        if (_wasPainting)
            StampAlongPath(_prevUV, uv, erasing);
        else
            Stamp(uv, erasing);

        _prevUV      = uv;
        _wasPainting = true;
        _wasErasing  = erasing;
    }

    // ── Sound ─────────────────────────────────────────────────────────────────

    void PlayBrushSound(bool erasing)
    {
        if (_audioSource == null) return;
        AudioClip wanted = erasing ? eraseSound : paintSound;
        if (wanted == null) { StopBrushSound(); return; }

        // Swap clip only when mode changes or sound stopped
        if (_audioSource.clip != wanted || !_audioSource.isPlaying)
        {
            _audioSource.clip   = wanted;
            _audioSource.volume = soundVolume;
            _audioSource.Play();
        }
    }

    void StopBrushSound()
    {
        if (_audioSource != null && _audioSource.isPlaying)
            _audioSource.Stop();
    }

    void OnDestroy()
    {
        if (_paintRT  != null) { _paintRT.Release();  Destroy(_paintRT);  }
        if (_tempRT   != null) { _tempRT.Release();   Destroy(_tempRT);   }
        if (_brushMat != null) Destroy(_brushMat);
    }

    // ── Painting ──────────────────────────────────────────────────────────────

    void StampAlongPath(Vector2 from, Vector2 to, bool erase)
    {
        // Tighter spacing with a texture brush so stamps fuse into a solid stroke
        float step  = brushSize * (brushTexture != null ? 0.15f : 0.35f);
        int   count = Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(from, to) / step), 1, 32);

        for (int i = 0; i <= count; i++)
            Stamp(Vector2.Lerp(from, to, (float)i / count), erase);
    }

    void Stamp(Vector2 uv, bool erase)
    {
        _brushMat.SetVector(ID_BrushUV,    new Vector4(uv.x, uv.y, 0f, 0f));
        _brushMat.SetFloat (ID_BrushSize,  brushSize);
        _brushMat.SetColor (ID_BrushCol,   brushColor);
        _brushMat.SetFloat (ID_BrushHard,  brushHardness);
        _brushMat.SetFloat (ID_EraseMode,  erase ? 1f : 0f);

        // Custom brush texture — swap at runtime without restarting
        bool hasTex = brushTexture != null;
        _brushMat.SetFloat  (ID_UseBrushTx, hasTex ? 1f : 0f);
        if (hasTex) _brushMat.SetTexture(ID_BrushTex, brushTexture);

        // Read _paintRT → stamp brush → write _tempRT → copy back
        Graphics.Blit(_paintRT, _tempRT, _brushMat);
        Graphics.Blit(_tempRT,  _paintRT);
    }

    // ── UI ────────────────────────────────────────────────────────────────────

    void BuildUI()
    {
        if (toggleButton != null)
        {
            _btnLabel = toggleButton.GetComponentInChildren<Text>();
            toggleButton.onClick.AddListener(ToggleMode);
            RefreshLabel();
            return;
        }

        // Auto-create a Canvas + button anchored to the bottom-center
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            var cgo = new GameObject("PaintCanvas");
            canvas  = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            cgo.AddComponent<CanvasScaler>();
            cgo.AddComponent<GraphicRaycaster>();
        }

        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var esgo = new GameObject("EventSystem");
            esgo.AddComponent<EventSystem>();
            esgo.AddComponent<StandaloneInputModule>();
        }

        // Button container
        var bgo = new GameObject("PaintEraseToggle");
        bgo.transform.SetParent(canvas.transform, false);

        var rt          = bgo.AddComponent<RectTransform>();
        rt.anchorMin    = new Vector2(0.5f, 0f);
        rt.anchorMax    = new Vector2(0.5f, 0f);
        rt.pivot        = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 24f);
        rt.sizeDelta    = new Vector2(220f, 58f);

        var img         = bgo.AddComponent<Image>();
        img.color       = new Color(0.08f, 0.08f, 0.08f, 0.9f);
        toggleButton    = bgo.AddComponent<Button>();
        toggleButton.targetGraphic = img;

        // Hover tint
        var colors          = toggleButton.colors;
        colors.highlightedColor = new Color(0.25f, 0.25f, 0.25f, 0.95f);
        colors.pressedColor     = new Color(0.4f,  0.4f,  0.4f,  1.0f);
        toggleButton.colors = colors;

        // Label
        var lgo   = new GameObject("Label");
        lgo.transform.SetParent(bgo.transform, false);
        var lrt   = lgo.AddComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.sizeDelta = Vector2.zero;

        _btnLabel           = lgo.AddComponent<Text>();
        _btnLabel.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _btnLabel.fontSize  = 22;
        _btnLabel.fontStyle = FontStyle.Bold;
        _btnLabel.alignment = TextAnchor.MiddleCenter;
        _btnLabel.color     = Color.white;

        toggleButton.onClick.AddListener(ToggleMode);
        RefreshLabel();
    }

    void ToggleMode()
    {
        eraseMode = !eraseMode;
        // Keep the surface material's debug flag in sync (optional)
        if (_surfaceMat != null)
            _surfaceMat.SetFloat(ID_EraseMode, eraseMode ? 1f : 0f);
        RefreshLabel();
    }

    // Button text shows the ACTION the user will switch TO
    void RefreshLabel()
    {
        if (_btnLabel == null) return;
        _btnLabel.text = eraseMode ? "Paint" : "Erase";
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static RenderTexture CreateRT(int size)
    {
        var rt = new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32);
        rt.filterMode = FilterMode.Bilinear;
        rt.Create();
        return rt;
    }

    static void ClearToTransparent(RenderTexture rt)
    {
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = prev;
    }
}

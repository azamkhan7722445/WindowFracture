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
    [Range(0.005f, 0.15f)] public float     brushSize     = 0.03f;
    public                        Color     brushColor     = Color.black;
    [Range(0f, 1f)]        public float     brushHardness  = 0.8f;
    [Tooltip("Optional custom brush shape. Drag any greyscale texture here — " +
             "white = full paint, black = no paint. Leave empty for a clean circle.")]
    public                        Texture2D brushTexture;

    [Header("Sounds (optional)")]
    [Tooltip("Played in a loop while the user is painting.")]
    public AudioClip paintSound;
    [Range(0f, 1f)] public float soundVolume = 0.7f;

    // ── Internals ─────────────────────────────────────────────────────────────

    RenderTexture _paintRT;
    RenderTexture _tempRT;
    Material      _surfaceMat;
    Material      _brushMat;
    Camera        _cam;
    MeshCollider  _col;

    bool    _wasPainting;
    Vector2 _prevUV;
    AudioSource _audioSource;

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

        // AudioSource for paint sound
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.loop        = true;
        _audioSource.playOnAwake = false;
        _audioSource.volume      = soundVolume;
        _audioSource.spatialBlend = 0f;  // 2D sound
    }

    void Update()
    {
        if (_brushMat == null) return;

        if (!Input.GetMouseButton(0))
        {
            StopBrushSound();
            _wasPainting = false;
            return;
        }

        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
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

        _prevUV      = uv;
        _wasPainting = true;
    }

    // ── Sound ─────────────────────────────────────────────────────────────────

    void PlayBrushSound()
    {
        if (_audioSource == null || paintSound == null) { StopBrushSound(); return; }

        if (_audioSource.clip != paintSound || !_audioSource.isPlaying)
        {
            _audioSource.clip   = paintSound;
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

    void StampAlongPath(Vector2 from, Vector2 to)
    {
        float step  = brushSize * (brushTexture != null ? 0.15f : 0.35f);
        int   count = Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(from, to) / step), 1, 32);

        for (int i = 0; i <= count; i++)
            Stamp(Vector2.Lerp(from, to, (float)i / count));
    }

    void Stamp(Vector2 uv)
    {
        _brushMat.SetVector(ID_BrushUV,   new Vector4(uv.x, uv.y, 0f, 0f));
        _brushMat.SetFloat (ID_BrushSize, brushSize);
        _brushMat.SetColor (ID_BrushCol,  brushColor);
        _brushMat.SetFloat (ID_BrushHard, brushHardness);
        _brushMat.SetFloat (ID_EraseMode, 0f);

        bool hasTex = brushTexture != null;
        _brushMat.SetFloat  (ID_UseBrushTx, hasTex ? 1f : 0f);
        if (hasTex) _brushMat.SetTexture(ID_BrushTex, brushTexture);

        Graphics.Blit(_paintRT, _tempRT, _brushMat);
        Graphics.Blit(_tempRT,  _paintRT);
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

using System.Collections;
using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Renderer))]
public class Paint_Effect : MonoBehaviour, IManagerInterface
{
    public event Action<PaintInputData> PaintInputChanged;

    public struct PaintInputData
    {
        public bool IsPainting { get; }
        public int FingerId { get; }
        public Vector2 ScreenPosition { get; }
        public Vector2 UV { get; }
        public Vector2 ScreenDelta { get; }
        public Vector2 UVDelta { get; }
        public float ScreenSpeed { get; }
        public float UVSpeed { get; }

        public PaintInputData(
            bool isPainting,
            int fingerId,
            Vector2 screenPosition,
            Vector2 uv,
            Vector2 screenDelta,
            Vector2 uvDelta,
            float screenSpeed,
            float uvSpeed)
        {
            IsPainting = isPainting;
            FingerId = fingerId;
            ScreenPosition = screenPosition;
            UV = uv;
            ScreenDelta = screenDelta;
            UVDelta = uvDelta;
            ScreenSpeed = screenSpeed;
            UVSpeed = uvSpeed;
        }
    }

    [Header("Textures")] [Tooltip("Assign a white crumpled paper texture here.")]
    public Texture2D paperTexture;

    [Tooltip("Resolution of the paint RenderTexture (width = height).")]
    public int canvasResolution = 1024;

    [Header("Brush")] [Range(0.005f, 0.15f)]
    public float brushSize = 0.03f;

    public Color brushColor = Color.black;
    [Range(0f, 1f)] public float brushHardness = 0.8f;

    [Tooltip("Optional custom brush shape. Drag any greyscale texture here - white = full paint, black = no paint.")]
    public Texture2D brushTexture;

    [Header("Brush Shader")] [Tooltip("Drag BrushStampShader here so it is guaranteed included in builds.")]
    public Shader brushStampShader;

    [Header("Camera Fit")] [SerializeField]
    private Camera targetCamera;

    [SerializeField] private Camera textCamera;

    [SerializeField] private float distanceFromCamera = 5f;
    [SerializeField] private bool scaleToDeviceHeightOnStart = true;
    [SerializeField, Range(0.1f, 1.5f)] private float deviceHeightFillPercent = 1f;
    [SerializeField] private bool limitCompletionToCameraView = true;
    [SerializeField, Range(-0.25f, 0.25f)] private float gameplayViewportMargin = 0.02f;

    [Header("Level Complete")] [SerializeField, Range(0f, 100f)]
    private float levelCompletePercent = 90f;

    [SerializeField] private GameObject levelCompleteUI;

    [Tooltip("How often paint percentage is checked while painting.")] [SerializeField]
    private float percentageCheckInterval = 0.25f;

    [Tooltip("Pixels with alpha above this value count as painted.")] [SerializeField, Range(0f, 1f)]
    private float paintedAlphaThreshold = 0.1f;

    public float PaintedPercentage { get; private set; }

    RenderTexture _paintRT;
    RenderTexture _tempRT;
    RenderTexture _textCameraRuntimeRT;
    Material _surfaceMat;
    Material _brushMat;
    Camera _cam;
    MeshCollider _col;

    bool _wasPainting;
    Vector2 _prevUV;
    Vector2 _prevScreenPosition;
    int _activeFingerId = -1;

    Texture2D _percentageReadTexture;
    float _nextPercentageCheckTime;
    bool _levelCompleteShown;
    int[] _visiblePixelIndices;
    int _visiblePixelCount;

    static readonly int ID_PaintTex = Shader.PropertyToID("_TextMap");
    static readonly int ID_PaintTexLegacy = Shader.PropertyToID("_PaintTexture");
    static readonly int ID_BrushUV = Shader.PropertyToID("_BrushUV");
    static readonly int ID_BrushSize = Shader.PropertyToID("_BrushSize");
    static readonly int ID_BrushCol = Shader.PropertyToID("_BrushColor");
    static readonly int ID_BrushHard = Shader.PropertyToID("_BrushHardness");
    static readonly int ID_EraseMode = Shader.PropertyToID("_EraseMode");
    static readonly int ID_PaperTex = Shader.PropertyToID("_PaperTexture");
    static readonly int ID_BrushTex = Shader.PropertyToID("_BrushTexture");
    static readonly int ID_UseBrushTx = Shader.PropertyToID("_UseBrushTex");

    public IEnumerator Initialize()
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

        if (scaleToDeviceHeightOnStart)
            ScaleToDeviceHeight();

        SetupTextCameraTargetTexture();

        BuildVisiblePixelCache();

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
        _surfaceMat.SetTexture(ID_PaintTexLegacy, _paintRT);

        // Use directly assigned shader first — guaranteed included in build.
        // Fall back to Shader.Find only in editor where stripping doesn't apply.
        var brushShader = brushStampShader != null ? brushStampShader : Shader.Find("Hidden/BrushStamp");
        if (brushShader == null)
        {
            Debug.LogError(
                "[Paint_Effect] BrushStamp shader not found. Assign it to the 'Brush Stamp Shader' field in the Inspector.");
            enabled = false;
            yield break;
        }

        _brushMat = new Material(brushShader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };

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

    void SetupTextCameraTargetTexture()
    {
        Camera camera = textCamera != null ? textCamera : GameObject.Find("TextCamera")?.GetComponent<Camera>();
        if (camera == null)
            return;

        if (_textCameraRuntimeRT == null)
        {
            RenderTextureDescriptor descriptor = new RenderTextureDescriptor(canvasResolution, canvasResolution, RenderTextureFormat.ARGB32, 0)
            {
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false,
                sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear,
                volumeDepth = 1,
                dimension = UnityEngine.Rendering.TextureDimension.Tex2D,
                enableRandomWrite = false,
                depthBufferBits = 0
            };

            _textCameraRuntimeRT = new RenderTexture(descriptor)
            {
                name = "TextCamera_RuntimeRT",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _textCameraRuntimeRT.Create();
        }

        camera.targetTexture = _textCameraRuntimeRT;
    }

    bool GetInputPosition(out Vector2 screenPos, out int fingerId)
    {
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null && touchscreen.primaryTouch.press.isPressed)
        {
            UnityEngine.InputSystem.TouchPhase phase = touchscreen.primaryTouch.phase.ReadValue();
            if (phase == UnityEngine.InputSystem.TouchPhase.Began ||
                phase == UnityEngine.InputSystem.TouchPhase.Moved ||
                phase == UnityEngine.InputSystem.TouchPhase.Stationary)
            {
                var touchId = touchscreen.primaryTouch.touchId.ReadValue();
                if (EventSystem.current && EventSystem.current.IsPointerOverGameObject(touchId))
                {
                    screenPos = Vector2.zero;
                    fingerId = -1;
                    return false;
                }

                screenPos = touchscreen.primaryTouch.position.ReadValue();
                fingerId = touchId;
                return true;
            }
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.isPressed)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                screenPos = Vector2.zero;
                fingerId = -1;
                return false;
            }

            screenPos = mouse.position.ReadValue();
            fingerId = -1;
            return true;
        }

        screenPos = Vector2.zero;
        fingerId = -1;
        return false;
    }

    void Update()
    {
        if (_brushMat == null || _cam == null || _levelCompleteShown)
            return;

        if (!GetInputPosition(out Vector2 inputPos, out int fingerId) || !CanShoot)
        {
            EndPaintInput();
            return;
        }

        Ray ray = _cam.ScreenPointToRay(inputPos);

        if (!Physics.Raycast(ray, out RaycastHit hit) || hit.collider != _col)
        {
            EndPaintInput();
            return;
        }

        Vector2 uv = hit.textureCoord;
        Vector2 screenDelta = _wasPainting ? inputPos - _prevScreenPosition : Vector2.zero;
        Vector2 uvDelta = _wasPainting ? uv - _prevUV : Vector2.zero;
        float deltaTime = Mathf.Max(Time.deltaTime, Mathf.Epsilon);

        PaintInputChanged?.Invoke(new PaintInputData(
            true,
            fingerId,
            inputPos,
            uv,
            screenDelta,
            uvDelta,
            screenDelta.magnitude / deltaTime,
            uvDelta.magnitude / deltaTime
        ));

        if (_wasPainting)
            StampAlongPath(_prevUV, uv);
        else
            Stamp(uv);

        _prevUV = uv;
        _prevScreenPosition = inputPos;
        _activeFingerId = fingerId;
        _wasPainting = true;

        CheckPaintPercentageTimer();
    }

    void EndPaintInput()
    {
        if (!_wasPainting)
            return;

        PaintInputChanged?.Invoke(new PaintInputData(
            false,
            _activeFingerId,
            _prevScreenPosition,
            _prevUV,
            Vector2.zero,
            Vector2.zero,
            0f,
            0f
        ));

        _wasPainting = false;
        _activeFingerId = -1;
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

        RenderTexture previousPaint = _paintRT;
        _paintRT = _tempRT;
        _tempRT = previousPaint;

        if (_surfaceMat != null)
        {
            _surfaceMat.SetTexture(ID_PaintTex, _paintRT);
            _surfaceMat.SetTexture(ID_PaintTexLegacy, _paintRT);
        }
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

        if (_visiblePixelIndices == null)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a > alphaLimit)
                    paintedPixels++;
            }

            PaintedPercentage = (paintedPixels / (float)pixels.Length) * 100f;
        }
        else
        {
            for (int i = 0; i < _visiblePixelCount; i++)
            {
                if (pixels[_visiblePixelIndices[i]].a > alphaLimit)
                    paintedPixels++;
            }

            PaintedPercentage = _visiblePixelCount > 0
                ? (paintedPixels / (float)_visiblePixelCount) * 100f
                : 0f;
        }

        Debug.Log($"Paint completed: {PaintedPercentage:0.0}%");

        CheckLevelComplete();
    }

    [Button]
    public void ScaleToDeviceHeight()
    {
        Camera camera = GetTargetCamera();
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();

        if (camera == null || meshRenderer == null)
            return;

        float currentWorldHeight = meshRenderer.bounds.size.y;
        if (currentWorldHeight <= 0f)
            return;

        Vector3 localScale = transform.localScale;
        float scaleMultiplier = GetGameplayWorldHeight(camera) / currentWorldHeight;
        float uniformLocalScale = localScale.y * scaleMultiplier;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.Undo.RecordObject(transform, "Scale Paint Plane To Device Height");
#endif

        transform.localScale = new Vector3(uniformLocalScale, uniformLocalScale, localScale.z);

#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.EditorUtility.SetDirty(transform);
#endif

        if (Application.isPlaying)
            BuildVisiblePixelCache();
    }

    void BuildVisiblePixelCache()
    {
        if (!limitCompletionToCameraView)
        {
            _visiblePixelIndices = null;
            _visiblePixelCount = canvasResolution * canvasResolution;
            return;
        }

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        int totalPixels = canvasResolution * canvasResolution;
        var indices = new int[totalPixels];
        int count = 0;

        for (int y = 0; y < canvasResolution; y++)
        {
            for (int x = 0; x < canvasResolution; x++)
            {
                Vector2 uv = new Vector2(
                    (x + 0.5f) / canvasResolution,
                    (y + 0.5f) / canvasResolution
                );

                if (!IsUvInGameplayView(uv, meshFilter))
                    continue;

                indices[count++] = y * canvasResolution + x;
            }
        }

        _visiblePixelIndices = new int[count];
        System.Array.Copy(indices, _visiblePixelIndices, count);
        _visiblePixelCount = count;
    }

    Camera GetTargetCamera()
    {
        return targetCamera != null ? targetCamera : Camera.main;
    }

    float GetGameplayWorldHeight(Camera camera)
    {
        float visibleHeight = GetVisibleWorldHeight(camera);
        float margin = Mathf.Clamp(gameplayViewportMargin, -0.45f, 0.45f);
        float usableHeight = visibleHeight * (1f - margin * 2f);

        return usableHeight * deviceHeightFillPercent;
    }

    float GetVisibleWorldHeight(Camera camera)
    {
        if (camera == null)
            return Mathf.Max(transform.lossyScale.y, 0f);

        float depth = Vector3.Dot(transform.position - camera.transform.position, camera.transform.forward);
        if (depth <= camera.nearClipPlane)
            depth = Mathf.Max(distanceFromCamera, camera.nearClipPlane + 0.1f);

        return camera.orthographic
            ? camera.orthographicSize * 2f
            : 2f * depth * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
    }

    Vector3 UvToWorld(Vector2 uv, MeshFilter meshFilter)
    {
        if (meshFilter == null || meshFilter.sharedMesh == null)
            return transform.position;

        Bounds localBounds = meshFilter.sharedMesh.bounds;
        Vector3 localPoint = new Vector3(
            Mathf.Lerp(localBounds.min.x, localBounds.max.x, uv.x),
            Mathf.Lerp(localBounds.min.y, localBounds.max.y, uv.y),
            localBounds.center.z
        );

        return transform.TransformPoint(localPoint);
    }

    bool IsUvInGameplayView(Vector2 uv, MeshFilter meshFilter)
    {
        if (!limitCompletionToCameraView)
            return true;

        Camera camera = GetTargetCamera();
        if (camera == null)
            return true;

        Vector3 viewportPoint = camera.WorldToViewportPoint(UvToWorld(uv, meshFilter));
        if (viewportPoint.z <= 0f)
            return false;

        float margin = Mathf.Clamp(gameplayViewportMargin, -0.45f, 0.45f);
        return viewportPoint.x >= margin
               && viewportPoint.x <= 1f - margin
               && viewportPoint.y >= margin
               && viewportPoint.y <= 1f - margin;
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

        StopPainting();

        SoundManager.Instance.PlayAudio(SoundManager.Instance.CompletedSfx);

        if (levelCompleteUI != null)
            levelCompleteUI.SetActive(true);
    }

    void StopPainting()
    {
        CanShoot = false;
        EndPaintInput();
    }

    void OnDestroy()
    {
        if (textCamera != null && textCamera.targetTexture == _textCameraRuntimeRT)
            textCamera.targetTexture = null;

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

        if (_textCameraRuntimeRT != null)
        {
            _textCameraRuntimeRT.Release();
            Destroy(_textCameraRuntimeRT);
        }
    }

    static RenderTexture CreateRT(int size)
    {
        var descriptor = new RenderTextureDescriptor(size, size)
        {
            depthBufferBits = 0,
            msaaSamples = 1,
            mipCount = 1,
            useMipMap = false,
            autoGenerateMips = false,
            sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear,
            volumeDepth = 1,
            dimension = UnityEngine.Rendering.TextureDimension.Tex2D,
            enableRandomWrite = false,
            colorFormat = RenderTextureFormat.ARGB32
        };

        var rt = new RenderTexture(descriptor);
        rt.name = "Paint_Effect_RT";
        rt.filterMode = FilterMode.Bilinear;
        rt.wrapMode = TextureWrapMode.Clamp;
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

    public IEnumerator ActiveDeplay(float time = 0, bool canShoot = false)
    {
        yield return new WaitForSeconds(time);
        if (canShoot && _levelCompleteShown)
            yield break;
        CanShoot = canShoot;
    }

    public void SetAction(bool canShoot)
    {
        if (canShoot && _levelCompleteShown)
            return;
        StartCoroutine(ActiveDeplay(canShoot ? 0.5f : 0, canShoot));
    }
}
using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class Responsive3DPanel : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private Camera targetCamera;

    [Header("Position In Camera View")]
    [Range(0f, 1f)]
    [SerializeField] private float viewportX = 0.5f;

    [Range(0f, 1f)]
    [SerializeField] private float viewportY = 0.5f;

    [SerializeField] private float distanceFromCamera = 5f;

    [Header("Responsive Size")]
    [Range(0.05f, 1f)]
    [SerializeField] private float screenHeightPercent = 0.5f;

    [Header("Behavior")]
    [SerializeField] private bool alwaysFaceCamera = true;
    [SerializeField] private bool updateEveryFrame = true;

    private Renderer panelRenderer;
    private Vector3 initialLocalScale;
    private float initialHeight;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        panelRenderer = GetComponent<Renderer>();

        initialLocalScale = transform.localScale;
        initialHeight = panelRenderer.bounds.size.y;
    }

    private void Start()
    {
        UpdatePanelTransform();
    }

    private void LateUpdate()
    {
        if (updateEveryFrame)
            UpdatePanelTransform();
    }

    private void UpdatePanelTransform()
    {
        if (targetCamera == null)
            return;

        SetPanelPosition();
        SetPanelRotation();
        SetPanelScale();
    }

    private void SetPanelPosition()
    {
        Vector3 viewportPoint = new Vector3(
            viewportX,
            viewportY,
            distanceFromCamera
        );

        transform.position = targetCamera.ViewportToWorldPoint(viewportPoint);
    }

    private void SetPanelRotation()
    {
        if (!alwaysFaceCamera)
            return;

        Vector3 directionToCamera = transform.position - targetCamera.transform.position;

        if (directionToCamera.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(directionToCamera);
        }
    }

    private void SetPanelScale()
    {
        if (initialHeight <= 0f)
            return;

        float visibleWorldHeight;

        if (targetCamera.orthographic)
        {
            visibleWorldHeight = targetCamera.orthographicSize * 2f;
        }
        else
        {
            visibleWorldHeight =
                2f *
                distanceFromCamera *
                Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        }

        float targetWorldHeight = visibleWorldHeight * screenHeightPercent;
        float scaleMultiplier = targetWorldHeight / initialHeight;

        transform.localScale = initialLocalScale * scaleMultiplier;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
            return;

        UpdatePanelTransform();
    }
#endif
}
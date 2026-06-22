using System.Collections;
using System.Collections.Generic;
using Arslan.Scripting;
using Azam_data.Bomb;
using GlassSystem.Scripts;
using Sirenix.OdinInspector;
using UnityEngine;

public class DeadlineSceneHandler : MonoBehaviour, IManagerInterface
{
    [TitleGroup("Refs"), SerializeField] private GlassPanel glassPanel;
    [TitleGroup("Refs"), SerializeField] private GamePanelHandling gamePanelHandling;
    [TitleGroup("Refs"), SerializeField] private Camera targetCamera;
    [TitleGroup("Refs"), SerializeField] private TimerControllerCanvas timerController;

    [TitleGroup("Level Complete"), SerializeField, Range(0f, 100f)]
    private float autoShatterHealthPercent = 30f;

    [TitleGroup("Level Complete"), SerializeField]
    private float levelCompleteShatterDelay = 0.25f;

    [TitleGroup("Level Complete"), SerializeField]
    private float levelCompletePanelDelay = 1.25f;

    [TitleGroup("Level Complete"), SerializeField, Min(1)]
    private int levelCompleteShardsPerFrame = 2;

    [TitleGroup("LevelFail"), SerializeField]
    private ParticleSystem blastParticlePrefab;

    [TitleGroup("LevelFail"), SerializeField]
    private int prespawnParticles = 10;

    [TitleGroup("LevelFail"), SerializeField, Min(0f)]
    private float levelFailDelay = 60f;

    [TitleGroup("LevelFail"), SerializeField, Min(1)]
    private int levelFailShardsPerFrame = 2;

    [TitleGroup("LevelFail"), SerializeField, Min(0f)]
    private float levelFailShardBlastForce = 10f;

    [TitleGroup("LevelFail"), SerializeField, Min(0f)]
    private float levelFailShardBlastTorque = 5f;

    [TitleGroup("LevelFail"), SerializeField, Min(1)]
    private int levelFailBlastCount = 10;

    [TitleGroup("LevelFail"), SerializeField]
    private Vector2 levelFailBlastDelayRange = new(0.05f, 0.25f);

    [TitleGroup("LevelFail"), SerializeField, Range(0f, 0.45f)]
    private float levelFailBlastScreenMargin = 0.1f;

    [TitleGroup("Debug"), ShowInInspector, ReadOnly]
    private List<GameObject> _preSpawnedObjects = new();

    [TitleGroup("Debug"), ShowInInspector, ReadOnly]
    private bool _onLevelFails = false;

    [TitleGroup("Debug"), ShowInInspector, ReadOnly]
    private bool _onLevelCompletes = false;

    [TitleGroup("Debug"), ShowInInspector, ReadOnly]
    private int _activeBlastParticles;

    [TitleGroup("Debug"), ShowInInspector, ReadOnly]
    private bool _levelFailPanelShown;

    [TitleGroup("Debug"), ShowInInspector, ReadOnly]
    private bool _levelFailShatterCompleted;

    private Coroutine _levelFailTimerRoutine;


    public IEnumerator Initialize()
    {
        if (!glassPanel)
        {
            Debug.LogError($"{nameof(DeadlineSceneHandler)} needs a {nameof(GlassPanel)} reference.", this);
            yield break;
        }

        if (!blastParticlePrefab)
        {
            Debug.LogError($"{nameof(DeadlineSceneHandler)} needs a blast particle prefab.", this);
            yield break;
        }

        for (var i = 0; i < prespawnParticles; i++)
        {
            var particle = Instantiate(blastParticlePrefab, transform);
            particle.gameObject.SetActive(false);
            _preSpawnedObjects.Add(particle.gameObject);
        }

        if (!gamePanelHandling)
            gamePanelHandling = GamePanelHandling.Instance;

        if (!timerController)
            timerController = FindFirstObjectByType<TimerControllerCanvas>();

        glassPanel.OnRemainingHealthUpdated += OnRemainingHealthUpdated;


        yield return null;
    }

    public IEnumerator PostInitialize()
    {
        yield return null;
    }

    public IEnumerator SetForGameplay()
    {
        if (timerController != null)
            timerController.SetTime(0f, levelFailDelay);

        _levelFailTimerRoutine = StartCoroutine(LevelFailTimer());
        
        yield return null;
    }

    private void OnDestroy()
    {
        if (_levelFailTimerRoutine != null)
            StopCoroutine(_levelFailTimerRoutine);

        if (glassPanel == null)
            return;

        glassPanel.OnRemainingHealthUpdated -= OnRemainingHealthUpdated;
    }

    private void OnRemainingHealthUpdated(float remainingHealth)
    {
        if (_onLevelCompletes || _onLevelFails)
            return;

        if (remainingHealth < autoShatterHealthPercent)
            StartCoroutine(LevelCompleteSequence());
    }

    private IEnumerator LevelFailTimer()
    {
        float duration = Mathf.Max(0f, levelFailDelay);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (_onLevelCompletes || _onLevelFails)
                yield break;

            elapsed += Time.deltaTime;

            if (timerController != null)
                timerController.SetTime(elapsed, duration);

            yield return null;
        }

        TriggerLevelFail();
    }

    private void TriggerLevelFail()
    {
        if (_onLevelFails || _onLevelCompletes)
            return;

        _onLevelFails = true;

        if (timerController != null)
        {
            timerController.StopTimerFeedback();
            timerController.PlayBlastFeedback();
        }

        StartCoroutine(LevelFailSequence());
    }

    private IEnumerator LevelFailSequence()
    {
        _levelFailShatterCompleted = false;

        if (glassPanel != null)
        {
            BreakGlassForLevelFailIfNeeded();
            glassPanel.SetCanBreak(false);
            StartCoroutine(LevelFailShatterSequence());
        }
        else
        {
            _levelFailShatterCompleted = true;
        }

        yield return PlayRandomScreenBlastSequence();
        yield return new WaitUntil(() => _levelFailShatterCompleted);
        yield return ShowLevelFailPanelAfterBlasts();
    }

    private IEnumerator LevelFailShatterSequence()
    {
        yield return glassPanel.BlastRemainingGlassTowards(
            GetShardBlastTargetPosition(),
            levelFailShardBlastForce,
            levelFailShardBlastTorque,
            levelFailShardsPerFrame
        );

        _levelFailShatterCompleted = true;
    }

    private Vector3 GetShardBlastTargetPosition()
    {
        Camera camera = targetCamera != null ? targetCamera : Camera.main;

        if (camera != null)
            return camera.transform.position;

        return glassPanel != null
            ? glassPanel.transform.position - glassPanel.transform.forward * 5f
            : transform.position;
    }

    private void BreakGlassForLevelFailIfNeeded()
    {
        if (glassPanel == null || glassPanel.IsBroken)
            return;

        glassPanel.SetCanBreak(true);

        MeshFilter meshFilter = glassPanel.GetComponent<MeshFilter>();
        float frontZ = meshFilter != null && meshFilter.sharedMesh != null
            ? meshFilter.sharedMesh.bounds.max.z
            : 0f;

        Vector3 breakPosition = glassPanel.transform.TransformPoint(new Vector3(0f, 0f, frontZ));
        glassPanel.Break(breakPosition, -glassPanel.transform.forward * 5000f);
    }

    private IEnumerator LevelCompleteSequence()
    {
        _onLevelCompletes = true;

        if (_levelFailTimerRoutine != null)
            StopCoroutine(_levelFailTimerRoutine);

        if (timerController != null)
            timerController.StopTimerFeedback();

        if (glassPanel != null)
            glassPanel.SetCanBreak(false);

        if (levelCompleteShatterDelay > 0f)
            yield return new WaitForSeconds(levelCompleteShatterDelay);

        VibrationsHandler.FinalShatter();

        if (glassPanel != null && glassPanel.BreakSound != null)
            AudioSource.PlayClipAtPoint(glassPanel.BreakSound, glassPanel.transform.position);

        if (glassPanel != null)
            yield return glassPanel.ShatterRemainingGlassSmoothly(levelCompleteShardsPerFrame);

        if (levelCompletePanelDelay > 0f)
            yield return new WaitForSeconds(levelCompletePanelDelay);

        ShowLevelCompletePanel();
    }

    private void ShowLevelCompletePanel()
    {
        if (gamePanelHandling == null)
            gamePanelHandling = GamePanelHandling.Instance;

        if (gamePanelHandling != null)
            gamePanelHandling.LevelComplete();
    }

    private void PlayBlastParticle(Vector3 position)
    {
        if (!_onLevelFails || _onLevelCompletes)
            return;

        GameObject blastObject = GetAvailableBlastObject();
        if (blastObject == null)
            blastObject = CreateBlastObject();

        if (blastObject == null)
            return;

        blastObject.transform.position = position;
        blastObject.SetActive(true);

        if (blastObject.TryGetComponent(out ParticleSystem particle))
        {
            particle.Clear(true);
            particle.Play(true);
            _activeBlastParticles++;
            StartCoroutine(DisableParticleAfterPlay(particle));
        }
    }

    private IEnumerator PlayRandomScreenBlastSequence()
    {
        int blastCount = Mathf.Max(1, levelFailBlastCount);

        for (int i = 0; i < blastCount; i++)
        {
            PlayBlastParticle(GetRandomScreenPosition());

            float delay = Random.Range(
                Mathf.Min(levelFailBlastDelayRange.x, levelFailBlastDelayRange.y),
                Mathf.Max(levelFailBlastDelayRange.x, levelFailBlastDelayRange.y)
            );

            if (delay > 0f)
                yield return new WaitForSeconds(delay);
        }
    }

    private Vector3 GetRandomScreenPosition()
    {
        Camera camera = targetCamera != null ? targetCamera : Camera.main;
        if (camera == null)
            return glassPanel != null ? glassPanel.transform.position : transform.position;

        float margin = Mathf.Clamp(levelFailBlastScreenMargin, 0f, 0.45f);
        float viewportX = Random.Range(margin, 1f - margin);
        float viewportY = Random.Range(margin, 1f - margin);
        float depth = GetBlastDepth(camera);

        return camera.ViewportToWorldPoint(new Vector3(viewportX, viewportY, depth));
    }

    private float GetBlastDepth(Camera camera)
    {
        if (glassPanel == null)
            return Mathf.Max(camera.nearClipPlane + 1f, 1f);

        float depth = Vector3.Dot(glassPanel.transform.position - camera.transform.position, camera.transform.forward);
        return Mathf.Max(depth, camera.nearClipPlane + 0.1f);
    }

    private GameObject GetAvailableBlastObject()
    {
        foreach (GameObject blastObject in _preSpawnedObjects)
        {
            if (blastObject != null && !blastObject.activeSelf)
                return blastObject;
        }

        return null;
    }

    private GameObject CreateBlastObject()
    {
        if (blastParticlePrefab == null)
            return null;

        ParticleSystem particle = Instantiate(blastParticlePrefab, transform);
        particle.gameObject.SetActive(false);
        _preSpawnedObjects.Add(particle.gameObject);
        return particle.gameObject;
    }

    private IEnumerator DisableParticleAfterPlay(ParticleSystem particle)
    {
        yield return new WaitWhile(() => particle != null && particle.IsAlive(true));

        if (particle != null)
            particle.gameObject.SetActive(false);

        _activeBlastParticles = Mathf.Max(0, _activeBlastParticles - 1);
    }

    private IEnumerator ShowLevelFailPanelAfterBlasts()
    {
        yield return null;
        yield return new WaitUntil(() => _activeBlastParticles == 0);

        if (_levelFailPanelShown || _onLevelCompletes)
            yield break;

        _levelFailPanelShown = true;

        if (gamePanelHandling == null)
            gamePanelHandling = GamePanelHandling.Instance;

        if (gamePanelHandling != null)
            gamePanelHandling.LevelFailed();
        else
            Debug.LogWarning(
                $"{nameof(DeadlineSceneHandler)} could not find a {nameof(GamePanelHandling)} to show the fail panel.",
                this);
    }
}
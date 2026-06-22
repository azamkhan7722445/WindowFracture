using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using DG.Tweening;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor.Drawers;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;


public static class SceneLookup
{
    private static Dictionary<string, int> sceneNameToIndex;

    [RuntimeInitializeOnLoadMethod]
    static void Init()
    {
        int sceneCount = SceneManager.sceneCountInBuildSettings;
        sceneNameToIndex = new Dictionary<string, int>();

        for (int i = 0; i < sceneCount; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);

            if (!sceneNameToIndex.ContainsKey(name))
                sceneNameToIndex.Add(name, i);
        }
    }

    public static bool HasScene(string sceneName)
    {
        return sceneNameToIndex.ContainsKey(sceneName);
    }

    public static int GetBuildIndex(string sceneName)
    {
        return sceneNameToIndex.GetValueOrDefault(sceneName, -1);
    }
}

public class SceneLoadManager : MonoBehaviour
{
    [SerializeField] CanvasGroup canvasGroup = null;
    [SerializeField] Image loadingFillIMG = null;
    [SerializeField] Text loadingPercentTxt;
    [SerializeField] private float preTransitionDelay = 0.35f;
    [SerializeField] private float panelFadeDuration = 0.25f;

    [TitleGroup("Runtim"), ShowInInspector, ReadOnly]
    private int _totalAssetsToLoad = 0;

    [TitleGroup("Runtime"), ShowInInspector, ReadOnly]
    private int _currentAssetLoaded = 0;

    [TitleGroup("Runtime"), ShowInInspector, ReadOnly]
    private bool _turnOffAfterAssetsAreLoaded = false;

    bool _isLoading;

    public static SceneLoadManager Instance = null;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            transform.parent = null;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        SetPanel(true);
    }

    public void CreateAssetsToLoad(int totalAssets, bool turnOffPanelAfterLoad)
    {
        _totalAssetsToLoad = totalAssets;
        _turnOffAfterAssetsAreLoaded = turnOffPanelAfterLoad;
    }

    public void AssetLoaded()
    {
        _currentAssetLoaded++;

        if (_currentAssetLoaded != _totalAssetsToLoad) return;
        _currentAssetLoaded = 0;
        if (_turnOffAfterAssetsAreLoaded)
        {
            _ = SetFakeLoadingPanel(false);
        }
    }

    public async Task SetFakeLoadingPanel(bool state, float initialValue = -1, float endValue = 0, float duration = 0,
        Action onComplete = null)
    {
        loadingFillIMG.DOKill();

        if (!state)
        {
            SetPanel(false);
            return;
        }

        if (Mathf.Approximately(initialValue, -1))
            initialValue = loadingFillIMG.fillAmount;

        initialValue = Mathf.Clamp01(initialValue);
        endValue = Mathf.Clamp01(endValue);

        if (duration <= 0f)
        {
            loadingFillIMG.fillAmount = endValue;
            loadingPercentTxt.text = $"{(int)(endValue * 100)}%";
            return;
        }

        DOTween.To(() => loadingFillIMG.fillAmount, value =>
            {
                loadingFillIMG.fillAmount = value;
                loadingPercentTxt.text = $"{(int)(value * 100)}%";
            }, endValue, duration)
            .From(initialValue).OnComplete(() => onComplete?.Invoke());

        await Task.Delay((int)(duration * 1000));
    }

    public void SetPanel(bool state)
    {
        if (state != (canvasGroup.alpha == 1))
        {
            canvasGroup.DOKill();
            canvasGroup.DOFade(state ? 1 : 0, panelFadeDuration).From(canvasGroup.alpha)
                .OnComplete(() => { canvasGroup.alpha = state ? 1 : 0; });
        }

        canvasGroup.blocksRaycasts = state;
        canvasGroup.interactable = state;
        AudioListener.pause = state;

        if (!state)
            _isLoading = false;
    }

    void SetInputBlocked(bool blocked)
    {
        canvasGroup.blocksRaycasts = blocked;
        canvasGroup.interactable = blocked;
    }

    Task FadePanelAlphaAsync(float targetAlpha)
    {
        var completionSource = new TaskCompletionSource<bool>();

        canvasGroup.DOKill();
        canvasGroup.DOFade(targetAlpha, panelFadeDuration)
            .From(canvasGroup.alpha)
            .OnComplete(() =>
            {
                canvasGroup.alpha = targetAlpha;
                completionSource.TrySetResult(true);
            });

        return completionSource.Task;
    }

    float ResolvePreTransitionDelay(float overrideDelay)
    {
        return overrideDelay > 0f ? overrideDelay : preTransitionDelay;
    }

    public bool HasScene(string sceneName)
    {
        return SceneLookup.HasScene(sceneName);
    }

    public async Task LoadSceneAsync(string sceneName, LoadSceneMode loadSceneMode = LoadSceneMode.Single,
        bool setTransitionPanel = true, float preTransitionDelayOverride = 0f)
    {
        if (!HasScene(sceneName))
        {
            Debug.Log("No Scene Found");
            return;
        }

        var sceneIndex = SceneLookup.GetBuildIndex(sceneName);

        await LoadSceneAsync(sceneIndex, loadSceneMode, setTransitionPanel, preTransitionDelayOverride);
    }

    public async Task LoadSceneAsync(int buildIndex, LoadSceneMode loadSceneMode = LoadSceneMode.Single,
        bool setTransitionPanel = true, float preTransitionDelayOverride = 0f)
    {
        if (_isLoading)
            return;

        _isLoading = true;
        canvasGroup.alpha = 0f;
        SetInputBlocked(true);

        float delay = ResolvePreTransitionDelay(preTransitionDelayOverride);
        if (delay > 0f)
            await Task.Delay((int)(delay * 1000f));

        loadingFillIMG.fillAmount = 0;
        loadingPercentTxt.text = "0%";

        if (setTransitionPanel)
        {
            await FadePanelAlphaAsync(1f);
            AudioListener.pause = true;

            await SetFakeLoadingPanel(true, 0, 0.5f, UnityEngine.Random.Range(2f, 4f));
        }

        AsyncOperation asyncOp = SceneManager.LoadSceneAsync(buildIndex, loadSceneMode);
        asyncOp.allowSceneActivation = false;

        while (asyncOp.progress < 0.9f)
        {
            float progress = Mathf.Clamp01(asyncOp.progress / 0.9f);
            loadingFillIMG.fillAmount = progress;
            loadingPercentTxt.text = $"{(int)(progress * 100)}%";
            await Task.Yield();
        }

        loadingFillIMG.fillAmount = 1f;
        loadingPercentTxt.text = "100%";

        asyncOp.allowSceneActivation = true;
    }

    public async Task UnloadScene(string sceneName /*, bool setTransitionPanel = true*/)
    {
        if (!HasScene(sceneName))
        {
            Debug.Log("No Scene Found");
            return;
        }

        AsyncOperation asyncOp = SceneManager.UnloadSceneAsync(SceneLookup.GetBuildIndex(sceneName));
        while (!asyncOp.isDone)
        {
            await Task.Yield();
        }

        await Task.Yield();
    }
}

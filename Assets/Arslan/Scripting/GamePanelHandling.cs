using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GamePanelHandling : MonoBehaviour
{
    public static GamePanelHandling Instance;
    [Header("Panels")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject completePanel;
    [SerializeField] private GameObject failedPanel;
    [SerializeField] private GameObject LoadingPanel;

    [Header("Different Buttons")]
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button retryButton;

    [Header("Same Buttons")]
    [SerializeField] private Button[] homeButtons;
    [SerializeField] private Button[] exitButtons;

    [Header("Scene Settings")]
    [SerializeField] private string homeSceneName = "MainMenu";
    [SerializeField] private int NextScene;
    [SerializeField] private string RestartSceneName = "MainMenu";
    [SerializeField] private bool pauseOnComplete = true;
    [SerializeField] private bool pauseOnFailed = true;

    [Header("Camera Script")]
    [SerializeField] private UnityEvent DisableWorking;
    [SerializeField] private UnityEvent EnableWorking;

    private bool isPaused;
    private bool isLevelEnded;


    private void Awake()
    {
        if (!Instance)
            Instance = this;
    }
    private void Start()
    {
        HideAllPanels();
        ResumeTime();
        AddButtonListeners();
    }

    private void OnDestroy()
    {
        RemoveButtonListeners();
    }

    private void AddButtonListeners()
    {
        if (pauseButton != null)
            pauseButton.onClick.AddListener(PauseLevel);

        if (resumeButton != null)
            resumeButton.onClick.AddListener(ResumeLevel);

        if (nextButton != null)
            nextButton.onClick.AddListener(NextLevel);

        if (retryButton != null)
            retryButton.onClick.AddListener(RetryLevel);

        foreach (Button button in homeButtons)
        {
            if (button != null)
                button.onClick.AddListener(GoHome);
        }

        foreach (Button button in exitButtons)
        {
            if (button != null)
                button.onClick.AddListener(ExitGame);
        }
    }

    private void RemoveButtonListeners()
    {
        if (pauseButton != null)
            pauseButton.onClick.RemoveListener(PauseLevel);

        if (resumeButton != null)
            resumeButton.onClick.RemoveListener(ResumeLevel);

        if (nextButton != null)
            nextButton.onClick.RemoveListener(NextLevel);

        if (retryButton != null)
            retryButton.onClick.RemoveListener(RetryLevel);

        foreach (Button button in homeButtons)
        {
            if (button != null)
                button.onClick.RemoveListener(GoHome);
        }

        foreach (Button button in exitButtons)
        {
            if (button != null)
                button.onClick.RemoveListener(ExitGame);
        }
    }

    public void PauseLevel()
    {
        if (isLevelEnded)
            return;

        isPaused = true;
        PauseTime();
        SetCameraScriptActive(false);
        ShowOnlyPanel(pausePanel);
    }

    public void ResumeLevel()
    {
        if (isLevelEnded)
            return;

        isPaused = false;
        ResumeTime();
        SetCameraScriptActive(true);
        HideAllPanels();
    }

    public void LevelComplete()
    {
        isLevelEnded = true;
        isPaused = false;

        if (pauseOnComplete)
            PauseTime();

        SetCameraScriptActive(false);
        ShowOnlyPanel(completePanel);
    }

    public void LevelFailed()
    {
        isLevelEnded = true;
        isPaused = false;

        if (pauseOnFailed)
            PauseTime();

        SetCameraScriptActive(false);
        ShowOnlyPanel(failedPanel);
    }

    public void NextLevel()
    {
        ResumeTime();


        StartCoroutine(LoadingScreen(NextScene));
    }

    IEnumerator LoadingScreen(int values)
    {
        LoadingPanel.SetActive(true);
        yield return new WaitForSeconds(2f);
            SceneManager.LoadScene(values);
    }

    public void RetryLevel()
    {
        ResumeTime();
        LoadingPanel.SetActive(true);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void GoHome()
    {
        ResumeTime();

        StartCoroutine(LoadingScreen(0));
    }

    public void ExitGame()
    {
        ResumeTime();
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void ShowOnlyPanel(GameObject panelToShow)
    {
        SetPanelActive(pausePanel, pausePanel == panelToShow);
        SetPanelActive(completePanel, completePanel == panelToShow);
        SetPanelActive(failedPanel, failedPanel == panelToShow);
    }

    private void HideAllPanels()
    {
        SetPanelActive(pausePanel, false);
        SetPanelActive(completePanel, false);
        SetPanelActive(failedPanel, false);
    }

    private void SetPanelActive(GameObject panel, bool isActive)
    {
        if (panel != null)
            panel.SetActive(isActive);
    }

    private void PauseTime()
    {
        Time.timeScale = 0f;
    }

    private void ResumeTime()
    {
        Time.timeScale = 1f;
    }

    private void SetCameraScriptActive(bool isActive)
    {
        if (!isActive)
            DisableWorking?.Invoke();
        else
            EnableWorking?.Invoke();
    }

   

    public bool IsPaused()
    {
        return isPaused;
    }

    public bool IsLevelEnded()
    {
        return isLevelEnded;
    }
}
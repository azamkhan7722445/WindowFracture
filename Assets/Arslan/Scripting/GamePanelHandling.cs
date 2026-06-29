using System.Collections;
using DG.Tweening;
using GlassSystem.Scripts;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Arslan.Scripting
{
    public class GamePanelHandling : MonoBehaviour, IManagerInterface
    {
        public static GamePanelHandling Instance;
        [Header("Panels")] [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject completePanel;
        [SerializeField] private GameObject failedPanel;

        [Header("Different Buttons")] [SerializeField]
        private Button pauseButton;

        [SerializeField] private Button resumeButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button retryButton;

        [Header("Same Buttons")] [SerializeField]
        private Button[] homeButtons;

        [SerializeField] private Button[] exitButtons;

        [Header("Scene Settings")] [SerializeField]
        private string homeSceneName = "MainMenu";

        [SerializeField] private int NextScene;
        [SerializeField] private string RestartSceneName = "MainMenu";
        [SerializeField] private bool pauseOnComplete = true;
        [SerializeField] private bool pauseOnFailed = true;

        [Header("Camera Script")] [SerializeField]
        private UnityEvent DisableWorking;

        [SerializeField] private UnityEvent EnableWorking;

        private bool isPaused;
        private bool isLevelEnded;


        private void Awake()
        {
            if (!Instance)
                Instance = this;
        }

        public IEnumerator Initialize()
        {
            HideAllPanels();
            ResumeTime();
            AddButtonListeners();

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

            SoundManager.Instance.PlayAudio(SoundManager.Instance.BtnClickSfx);

            isPaused = true;
            PauseTime();
            SetCameraScriptActive(false);
            ShowOnlyPanel(pausePanel);
        }

        public void ResumeLevel()
        {
            if (isLevelEnded)
                return;

            VibrationsHandler.SoftImpact();
            SoundManager.Instance.PlayAudio(SoundManager.Instance.BtnClickSfx);

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

            SoundManager.Instance.PlayAudio(SoundManager.Instance.CompletedSfx);

            SetCameraScriptActive(false);
            ShowOnlyPanel(completePanel);
        }

        public void LevelFailed()
        {
            isLevelEnded = true;
            isPaused = false;

            if (pauseOnFailed)
                PauseTime();

            SoundManager.Instance.PlayAudio(SoundManager.Instance.FailedSfx);

            SetCameraScriptActive(false);
            ShowOnlyPanel(failedPanel);
        }

        public void NextLevel()
        {
            VibrationsHandler.SoftImpact();
            ResumeTime();
            SoundManager.Instance.PlayAudio(SoundManager.Instance.BtnClickSfx);
            _ = SceneLoadManager.Instance.LoadSceneAsync(NextScene,LoadSceneMode.Single,true,0.5f);
        }


        public void RetryLevel()
        {
            VibrationsHandler.SoftImpact();
            ResumeTime();
            SoundManager.Instance.PlayAudio(SoundManager.Instance.BtnClickSfx);
            _ = SceneLoadManager.Instance.LoadSceneAsync(SceneManager.GetActiveScene().buildIndex,LoadSceneMode.Single,true,0.5f);
        }

        public void GoHome()
        {
            VibrationsHandler.SoftImpact();
            ResumeTime();
            SoundManager.Instance.PlayAudio(SoundManager.Instance.BackBtnSfx);
            _ = SceneLoadManager.Instance.LoadSceneAsync(0,LoadSceneMode.Single,true,0.5f);
        }

        public void ExitGame()
        {
            VibrationsHandler.SoftImpact();
            SoundManager.Instance.PlayAudio(SoundManager.Instance.BackBtnSfx);

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
}
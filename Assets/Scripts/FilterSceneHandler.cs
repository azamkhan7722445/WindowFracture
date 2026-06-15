using System;
using System.Collections;
using GlassSystem.Scripts;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefaultNamespace
{
    public class FilterSceneHandler : MonoBehaviour
    {
        [TitleGroup("Refs"), SerializeField] private GlassPanel glassPanel;
        [TitleGroup("Refs"), SerializeField] private GamePanelHandling gamePanelHandling;

        [TitleGroup("Level Complete"), SerializeField, Range(0f, 100f)]
        private float autoShatterHealthPercent = 30f;

        [TitleGroup("Level Complete"), SerializeField]
        private float levelCompleteShatterDelay = 0.25f;

        [TitleGroup("Level Complete"), SerializeField]
        private float levelCompletePanelDelay = 1.25f;

        [TitleGroup("Level Complete"), SerializeField, Min(1)]
        private int levelCompleteShardsPerFrame = 2;

        [TitleGroup("Input Gate"), SerializeField]
        private GameObject inputTxtPanelObj;

        [TitleGroup("Input Gate"), SerializeField]
        private TMP_Text inputTxt;

        [TitleGroup("Input Gate"), SerializeField]
        private Button inputAreaContinueBtn;

        private string _enteredName = string.Empty;
        private bool _levelCompleteShown;

        private void Awake()
        {
            if (glassPanel == null)
            {
                Debug.LogError($"{nameof(FilterSceneHandler)} needs a {nameof(GlassPanel)} reference.", this);
                return;
            }

            glassPanel.SetCanBreak(false);
            glassPanel.OnRemainingHealthUpdated += OnRemainingHealthUpdated;

            if (gamePanelHandling == null)
                gamePanelHandling = GamePanelHandling.Instance;
        }


        private void Start()
        {
            if (inputAreaContinueBtn != null)
                inputAreaContinueBtn.onClick.AddListener(InputAreaContinueBtn);

            if (inputTxtPanelObj != null)
                inputTxtPanelObj.SetActive(true);

            SetInputText(string.Empty);
            SetContinueButtonState(false);
        }

        private void OnDestroy()
        {
            if (inputAreaContinueBtn != null)
                inputAreaContinueBtn.onClick.RemoveListener(InputAreaContinueBtn);

            if (glassPanel != null)
                glassPanel.OnRemainingHealthUpdated -= OnRemainingHealthUpdated;
        }

        private void OnRemainingHealthUpdated(float remainingHealth)
        {
            if (_levelCompleteShown)
                return;

            if (remainingHealth < autoShatterHealthPercent)
                StartCoroutine(LevelCompleteSequence());
        }

        private IEnumerator LevelCompleteSequence()
        {
            _levelCompleteShown = true;

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

            if (gamePanelHandling == null)
                gamePanelHandling = GamePanelHandling.Instance;

            if (gamePanelHandling != null)
                gamePanelHandling.LevelComplete();
        }

        #region InputArea

        public void OnInputFieldUpdate(string input)
        {
            SetInputText(input);
            SetContinueButtonState(!string.IsNullOrWhiteSpace(_enteredName));
        }

        public void InputAreaContinueBtn()
        {
            if (string.IsNullOrWhiteSpace(_enteredName))
                return;

            if (glassPanel == null)
                return;

            glassPanel.SetCanBreak(true);

            if (inputTxtPanelObj != null)
                inputTxtPanelObj.SetActive(false);
        }

        private void SetInputText(string input)
        {
            _enteredName = input?.Trim() ?? string.Empty;

            if (inputTxt != null)
                inputTxt.text = input ?? string.Empty;
        }


        private void SetContinueButtonState(bool isInteractable)
        {
            if (inputAreaContinueBtn != null)
                inputAreaContinueBtn.interactable = isInteractable;
        }

        #endregion
    }
}
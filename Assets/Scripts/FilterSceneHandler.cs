using System;
using System.Collections;
using Arslan.Scripting;
using GlassSystem.Scripts;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefaultNamespace
{
    public class FilterSceneHandler : MonoBehaviour, IManagerInterface
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

        [TitleGroup("Wall Material"), SerializeField]
        private Renderer wallRenderer;

        [TitleGroup("Wall Material"), SerializeField, PreviewField]
        private Texture2D heightMap;

        [TitleGroup("Wall Material"), SerializeField, PreviewField]
        private Texture2D noiseMap;

        private string _enteredName = string.Empty;
        private bool _levelCompleteShown;


        public IEnumerator Initialize()
        {
            if (glassPanel == null)
            {
                Debug.LogError($"{nameof(FilterSceneHandler)} needs a {nameof(GlassPanel)} reference.", this);
                yield break;
            }

            ApplyWallMaterialSettings();

            glassPanel.SetCanBreak(false);
            glassPanel.OnRemainingHealthUpdated += OnRemainingHealthUpdated;

            if (gamePanelHandling == null)
                gamePanelHandling = GamePanelHandling.Instance;

            yield return null;
        }

        public IEnumerator PostInitialize()
        {
            if (inputAreaContinueBtn != null)
                inputAreaContinueBtn.onClick.AddListener(InputAreaContinueBtn);

            if (inputTxtPanelObj != null)
                inputTxtPanelObj.SetActive(true);

            SetInputText(string.Empty);
            SetContinueButtonState(false);

            yield return null;
        }

        public IEnumerator SetForGameplay()
        {
            yield return null;
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

            SoundManager.Instance.PlayAudio(SoundManager.Instance.BtnClickSfx);
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

        private void ApplyWallMaterialSettings()
        {
            if (heightMap == null && noiseMap == null)
                return;

            Renderer renderer = wallRenderer;
            if (renderer == null && glassPanel != null)
                renderer = glassPanel.GetComponent<Renderer>();

            if (renderer == null)
            {
                Debug.LogWarning($"{nameof(FilterSceneHandler)} could not find a wall renderer for wall material settings.", this);
                return;
            }

            Material material = renderer.material;

            if (heightMap != null)
                material.SetTexture("_HeightMap", heightMap);

            if (noiseMap != null)
            {
                material.SetTexture("_NoiseMap", noiseMap);
                material.SetFloat("_UseNoiseMap", 1f);
            }
        }
    }
}
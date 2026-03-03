using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using CivilSim.Core;

namespace CivilSim.UI
{
    /// <summary>
    /// Loading 씬에서 목표 씬을 비동기 로드하고 진행률을 표시한다.
    /// </summary>
    public class LoadingSceneUI : MonoBehaviour
    {
        [Header("Scene")]
        [SerializeField] private string _fallbackTargetSceneName = "Game Play";
        [SerializeField] private float _minimumLoadingSeconds = 1.8f;

        [Header("UI")]
        [SerializeField] private Slider _progressBar;
        [SerializeField] private Image _progressFillImage;
        [SerializeField] private TextMeshProUGUI _progressText;
        [SerializeField] private TextMeshProUGUI _statusText;
        [SerializeField] private string _loadingMessage = "Loading...";
        [SerializeField] private string _readyMessage = "Ready";

        private bool _isStarted;

        private void Awake()
        {
            AutoBindControls();
            SetProgress(0f);
            SetStatus(_loadingMessage);
        }

        private void Start()
        {
            if (_isStarted) return;
            _isStarted = true;
            StartCoroutine(LoadTargetSceneRoutine());
        }

        private IEnumerator LoadTargetSceneRoutine()
        {
            string targetScene = ResolveTargetSceneName();
            if (!CanLoadScene(targetScene))
            {
                SetStatus($"Scene not found: {targetScene}");
                Debug.LogError($"[LoadingSceneUI] Scene not found: {targetScene}");
                yield break;
            }

            AsyncOperation loadOp = SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Single);
            if (loadOp == null)
            {
                SetStatus("Failed to start scene loading");
                Debug.LogError("[LoadingSceneUI] SceneManager.LoadSceneAsync returned null.");
                yield break;
            }

            loadOp.allowSceneActivation = false;

            float minimumDuration = Mathf.Max(1f, _minimumLoadingSeconds);
            float elapsed = 0f;
            float visualProgress = 0f;
            while (!loadOp.isDone)
            {
                elapsed += Time.unscaledDeltaTime;

                float asyncProgress = Mathf.Clamp01(loadOp.progress / 0.9f);
                float timeProgress = Mathf.Clamp01(elapsed / minimumDuration);
                float targetProgress = Mathf.Min(asyncProgress, timeProgress);
                visualProgress = Mathf.MoveTowards(visualProgress, targetProgress, Time.unscaledDeltaTime * 2.5f);
                SetProgress(visualProgress);

                if (loadOp.progress >= 0.9f && elapsed >= minimumDuration)
                {
                    SetProgress(1f);
                    SetStatus(_readyMessage);
                    loadOp.allowSceneActivation = true;
                }

                yield return null;
            }
        }

        private string ResolveTargetSceneName()
        {
            if (LoadingSceneContext.ConsumeTargetScene(out string requestedScene) &&
                !string.IsNullOrWhiteSpace(requestedScene))
            {
                return requestedScene.Trim();
            }

            return string.IsNullOrWhiteSpace(_fallbackTargetSceneName)
                ? "Game Play"
                : _fallbackTargetSceneName.Trim();
        }

        private static bool CanLoadScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName)) return false;
            return Application.CanStreamedLevelBeLoaded(sceneName);
        }

        private void SetProgress(float value01)
        {
            float value = Mathf.Clamp01(value01);
            if (_progressBar != null)
                _progressBar.value = value;
            if (_progressFillImage != null)
                _progressFillImage.fillAmount = value;
            if (_progressText != null)
                _progressText.text = $"{Mathf.RoundToInt(value * 100f)}%";
        }

        private void SetStatus(string message)
        {
            if (_statusText != null)
                _statusText.text = message ?? string.Empty;
        }

        private void AutoBindControls()
        {
            if (_progressBar == null)
            {
                _progressBar = FindSlider("ProgressBar")
                    ?? FindSlider("LoadingProgressBar")
                    ?? FindSliderContains("progress");
            }

            if (_progressFillImage == null)
            {
                _progressFillImage = FindImage("ProgressFill")
                    ?? FindImage("LoadingProgressFill");
            }

            if (_progressText == null)
            {
                _progressText = FindText("ProgressText")
                    ?? FindText("LoadingPercentText")
                    ?? FindTextContains("percent")
                    ?? FindTextContains("progress");
            }

            if (_statusText == null)
            {
                _statusText = FindText("StatusText")
                    ?? FindText("LoadingStatusText")
                    ?? FindTextContains("status");
            }
        }

        private static Slider FindSlider(string objectName)
        {
            var go = GameObject.Find(objectName);
            return go != null ? go.GetComponent<Slider>() : null;
        }

        private static Slider FindSliderContains(string textLower)
        {
            var sliders = FindObjectsByType<Slider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var slider in sliders)
            {
                if (slider == null || slider.gameObject == null) continue;
                string nameLower = slider.gameObject.name.ToLowerInvariant();
                if (nameLower.Contains(textLower))
                    return slider;
            }
            return null;
        }

        private static Image FindImage(string objectName)
        {
            var go = GameObject.Find(objectName);
            return go != null ? go.GetComponent<Image>() : null;
        }

        private static TextMeshProUGUI FindText(string objectName)
        {
            var go = GameObject.Find(objectName);
            return go != null ? go.GetComponent<TextMeshProUGUI>() : null;
        }

        private static TextMeshProUGUI FindTextContains(string textLower)
        {
            var texts = FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var text in texts)
            {
                if (text == null || text.gameObject == null) continue;
                string nameLower = text.gameObject.name.ToLowerInvariant();
                if (nameLower.Contains(textLower))
                    return text;
            }
            return null;
        }
    }
}

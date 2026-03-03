using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using CivilSim.Core;

namespace CivilSim.UI
{
    /// <summary>
    /// Entry 씬 시작 메뉴 UI.
    /// 새 게임 시작 / 저장 목록 불러오기 진입을 담당한다.
    /// </summary>
    public class StartMenuUI : MonoBehaviour
    {
        [Header("씬 이름")]
        [SerializeField] private string _gamePlaySceneName = "Game Play";
        [SerializeField] private bool _useLoadingScene = true;
        [SerializeField] private string _loadingSceneName = "Loading";

        [Header("메인 버튼")]
        [SerializeField] private Button _newGameButton;
        [SerializeField] private Button _loadGameButton;

        [Header("저장 목록 패널")]
        [SerializeField] private GameObject _saveListPanel;
        [SerializeField] private Transform _saveListContainer;
        [SerializeField] private Button _saveItemButtonPrefab;
        [SerializeField] private Button _closeListButton;
        [SerializeField] private TextMeshProUGUI _emptyText;
        [SerializeField] private TextMeshProUGUI _statusText;

        private readonly List<GameObject> _spawnedItems = new();
        private List<SaveLoadManager.SaveSlotInfo> _slots = new();

        private void Awake()
        {
            AutoBindControls();
            BindListeners();
            SetSaveListVisible(false);
            UpdateStatus(string.Empty);
        }

        private void Start()
        {
            RefreshSaveList();
        }

        private void OnDestroy()
        {
            if (_newGameButton != null) _newGameButton.onClick.RemoveListener(OnClickNewGame);
            if (_loadGameButton != null) _loadGameButton.onClick.RemoveListener(OnClickLoadMenu);
            if (_closeListButton != null) _closeListButton.onClick.RemoveListener(CloseSaveList);
        }

        private void OnClickNewGame()
        {
            GameStartContext.RequestNewGame();
            BeginSceneTransition();
        }

        private void OnClickLoadMenu()
        {
            RefreshSaveList();
            if (_slots.Count == 0)
            {
                SetSaveListVisible(true);
                UpdateStatus("저장된 게임이 없습니다.");
                return;
            }

            SetSaveListVisible(true);
            UpdateStatus("불러올 저장을 선택하세요.");
        }

        private void CloseSaveList()
        {
            SetSaveListVisible(false);
            UpdateStatus(string.Empty);
        }

        private void RefreshSaveList()
        {
            _slots = SaveLoadManager.GetSaveSlotInfos();
            RebuildSaveListItems();
            if (_loadGameButton != null)
                _loadGameButton.interactable = _slots.Count > 0;
        }

        private void RebuildSaveListItems()
        {
            ClearSpawnedItems();

            bool hasSave = _slots.Count > 0;
            if (_emptyText != null)
                _emptyText.gameObject.SetActive(!hasSave);

            if (!hasSave || _saveListContainer == null) return;

            foreach (var slot in _slots)
            {
                Button button = CreateSaveItemButton();
                if (button == null) continue;

                button.onClick.RemoveAllListeners();
                string slotName = slot.SlotName;
                button.onClick.AddListener(() => OnClickLoadSlot(slotName));

                var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                    label.text = BuildSlotLabel(slot);

                _spawnedItems.Add(button.gameObject);
            }
        }

        private void OnClickLoadSlot(string slotName)
        {
            GameStartContext.RequestLoad(slotName);
            BeginSceneTransition();
        }

        private void BeginSceneTransition()
        {
            if (_useLoadingScene && CanLoadScene(_loadingSceneName))
            {
                LoadingSceneContext.RequestTargetScene(_gamePlaySceneName);
                SceneManager.LoadScene(_loadingSceneName);
                return;
            }

            if (!CanLoadScene(_gamePlaySceneName))
            {
                string message = $"진입 실패: 씬을 찾을 수 없습니다 ({_gamePlaySceneName})";
                UpdateStatus(message);
                Debug.LogError($"[StartMenuUI] {message}");
                return;
            }

            SceneManager.LoadScene(_gamePlaySceneName);
        }

        private static bool CanLoadScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName)) return false;
            return Application.CanStreamedLevelBeLoaded(sceneName.Trim());
        }

        private Button CreateSaveItemButton()
        {
            if (_saveItemButtonPrefab != null)
                return Instantiate(_saveItemButtonPrefab, _saveListContainer);

            var go = new GameObject("SaveItemButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_saveListContainer, false);

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.GetComponent<TextMeshProUGUI>();
            text.fontSize = 28;
            text.alignment = TextAlignmentOptions.Midline;
            text.color = Color.white;
            text.text = "Save Slot";

            var rt = textGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(18, 8);
            rt.offsetMax = new Vector2(-18, -8);

            return go.GetComponent<Button>();
        }

        private static string BuildSlotLabel(SaveLoadManager.SaveSlotInfo slot)
        {
            string timestamp = slot.SavedAtUtcTicks > 0
                ? new DateTime(slot.SavedAtUtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                : "시간 정보 없음";

            return $"{slot.SlotName} | {timestamp} | {slot.Year}년 {slot.Month:D2}월 {slot.Day:D2}일 | 잔액 {slot.Money:N0}";
        }

        private void SetSaveListVisible(bool visible)
        {
            if (_saveListPanel != null)
                _saveListPanel.SetActive(visible);
        }

        private void UpdateStatus(string text)
        {
            if (_statusText == null) return;
            _statusText.text = text;
        }

        private void ClearSpawnedItems()
        {
            foreach (var item in _spawnedItems)
            {
                if (item != null)
                    Destroy(item);
            }
            _spawnedItems.Clear();
        }

        private void AutoBindControls()
        {
            if (_newGameButton == null)
                _newGameButton = FindButtonByName("NewGameButton") ?? FindButtonByContains("new");
            if (_loadGameButton == null)
                _loadGameButton = FindButtonByName("LoadGameButton") ?? FindButtonByContains("load");

            if (_saveListPanel == null)
            {
                var panelGo = GameObject.Find("SaveListPanel") ?? GameObject.Find("LoadListPanel");
                if (panelGo != null) _saveListPanel = panelGo;
            }

            if (_saveListContainer == null && _saveListPanel != null)
            {
                var tr = _saveListPanel.transform.Find("Content");
                if (tr != null) _saveListContainer = tr;
            }

            if (_closeListButton == null && _saveListPanel != null)
                _closeListButton = FindButtonInChildrenByName(_saveListPanel.transform, "Close")
                    ?? FindButtonInChildrenByName(_saveListPanel.transform, "Exit");

            if (_emptyText == null && _saveListPanel != null)
                _emptyText = FindTextInChildrenByName(_saveListPanel.transform, "EmptyText");
            if (_statusText == null)
                _statusText = FindTextByName("StatusText");
        }

        private void BindListeners()
        {
            if (_newGameButton != null)
            {
                _newGameButton.onClick.RemoveListener(OnClickNewGame);
                _newGameButton.onClick.AddListener(OnClickNewGame);
            }

            if (_loadGameButton != null)
            {
                _loadGameButton.onClick.RemoveListener(OnClickLoadMenu);
                _loadGameButton.onClick.AddListener(OnClickLoadMenu);
            }

            if (_closeListButton != null)
            {
                _closeListButton.onClick.RemoveListener(CloseSaveList);
                _closeListButton.onClick.AddListener(CloseSaveList);
            }
        }

        private static Button FindButtonByName(string objectName)
        {
            var go = GameObject.Find(objectName);
            return go != null ? go.GetComponent<Button>() : null;
        }

        private static Button FindButtonByContains(string textLower)
        {
            var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var button in buttons)
            {
                if (button == null || button.gameObject == null) continue;
                string nameLower = button.gameObject.name.ToLowerInvariant();
                if (nameLower.Contains(textLower))
                    return button;
            }
            return null;
        }

        private static Button FindButtonInChildrenByName(Transform parent, string objectName)
        {
            if (parent == null) return null;
            var transforms = parent.GetComponentsInChildren<Transform>(true);
            foreach (var tr in transforms)
            {
                if (tr == null || tr.name != objectName) continue;
                var button = tr.GetComponent<Button>();
                if (button != null) return button;
            }
            return null;
        }

        private static TextMeshProUGUI FindTextByName(string objectName)
        {
            var go = GameObject.Find(objectName);
            return go != null ? go.GetComponent<TextMeshProUGUI>() : null;
        }

        private static TextMeshProUGUI FindTextInChildrenByName(Transform parent, string objectName)
        {
            if (parent == null) return null;
            var texts = parent.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in texts)
            {
                if (t != null && t.gameObject.name == objectName)
                    return t;
            }
            return null;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using CivilSim.CameraSystem;
using CivilSim.Core;

namespace CivilSim.UI
{
    /// <summary>
    /// 게임 환경 설정 패널 (카메라 이동 속도 등) 런타임 조정.
    /// 정책(세율/수요계수) 설정은 PolicyPanelUI로 분리한다.
    /// </summary>
    public class SettingsPanelUI : MonoBehaviour
    {
        [Header("패널 루트 (자식 오브젝트)")]
        [SerializeField] private GameObject _panel;

        [Header("외부 열기 버튼 (미할당 시 자동 탐색)")]
        [SerializeField] private Button _openButton;

        [Header("게임 설정 패널 나가기 버튼 (루트로 복귀)")]
        [SerializeField] private Button _closeButton;

        [Header("저장 버튼 (게임 설정 패널, 선택)")]
        [SerializeField] private Button _saveGameButton;

        [Header("서브 패널 (선택)")]
        [SerializeField] private GameObject _rootSettingsMenuRoot;
        [SerializeField] private GameObject _gameSettingsContentRoot;
        [SerializeField] private GameObject _hotkeySettingsPanelRoot;

        [Header("루트 메뉴 버튼/닫기 (선택)")]
        [SerializeField] private Button _openGameSettingsButton;
        [SerializeField] private Button _openHotkeySettingsButton;
        [SerializeField] private Button _closeHotkeySettingsButton;
        [SerializeField] private Button _closeRootButton;

        [Header("카메라 이동 속도")]
        [SerializeField] private Slider _cameraPanSlider;
        [SerializeField] private TextMeshProUGUI _cameraPanLabel;

        [Header("단축키 설정")]
        [SerializeField] private TextMeshProUGUI _hotkeyStatusLabel;
        [SerializeField] private Button _resetHotkeysButton;
        [SerializeField] private Button _bindBuildingPanelKeyButton;
        [SerializeField] private Button _bindRoadModeKeyButton;
        [SerializeField] private Button _bindFoundationModeKeyButton;
        [SerializeField] private Button _bindZoneModeKeyButton;
        [SerializeField] private Button _bindZoneResidentialKeyButton;
        [SerializeField] private Button _bindZoneCommercialKeyButton;
        [SerializeField] private Button _bindZoneIndustrialKeyButton;
        [SerializeField] private Button _bindZoneClearKeyButton;
        [SerializeField] private Button _bindRotateBuildingKeyButton;

        [SerializeField] private TextMeshProUGUI _bindBuildingPanelKeyLabel;
        [SerializeField] private TextMeshProUGUI _bindRoadModeKeyLabel;
        [SerializeField] private TextMeshProUGUI _bindFoundationModeKeyLabel;
        [SerializeField] private TextMeshProUGUI _bindZoneModeKeyLabel;
        [SerializeField] private TextMeshProUGUI _bindZoneResidentialKeyLabel;
        [SerializeField] private TextMeshProUGUI _bindZoneCommercialKeyLabel;
        [SerializeField] private TextMeshProUGUI _bindZoneIndustrialKeyLabel;
        [SerializeField] private TextMeshProUGUI _bindZoneClearKeyLabel;
        [SerializeField] private TextMeshProUGUI _bindRotateBuildingKeyLabel;

        [Header("카메라 속도 범위")]
        [SerializeField, Range(0.05f, 0.5f)] private float _minPanSpeed = 0.05f;
        [SerializeField, Range(0.5f, 3f)] private float _maxPanSpeed = 2f;
        [SerializeField, Range(0.05f, 3f)] private float _defaultPanSpeed = 0.4f;

        private enum SettingsView
        {
            RootMenu,
            GameSettings,
            HotkeySettings
        }

        private const string PrefLastViewKey = "CivilSim.SettingsPanel.LastView";
        private const string PrefCameraPanSpeedKey = "CivilSim.SettingsPanel.CameraPanSpeed";

        private RTSCameraController _camCtrl;
        private readonly List<GameObject> _autoRootMenuObjects = new();
        private bool _isOpen;
        private SettingsView _currentView = SettingsView.RootMenu;
        private SettingsView _preferredOpenView = SettingsView.RootMenu;
        private bool _isRebinding;
        private GameHotkeyAction _rebindingAction;

        private void Awake()
        {
            LoadPreferences();
            _camCtrl = FindFirstObjectByType<RTSCameraController>();
            AutoBindButtons();
            AutoBindHotkeyControls();
            BindButtonListeners();
            BindHotkeyListeners();
            SetVisible(false);
        }

        private void Start()
        {
            float initSpeed = (_camCtrl != null) ? _camCtrl.KeyPanSpeed : _defaultPanSpeed;
            if (PlayerPrefs.HasKey(PrefCameraPanSpeedKey))
                initSpeed = PlayerPrefs.GetFloat(PrefCameraPanSpeedKey, initSpeed);
            initSpeed = Mathf.Clamp(initSpeed, _minPanSpeed, _maxPanSpeed);

            if (_camCtrl != null)
                _camCtrl.KeyPanSpeed = initSpeed;

            if (_cameraPanSlider != null)
            {
                _cameraPanSlider.minValue = _minPanSpeed;
                _cameraPanSlider.maxValue = _maxPanSpeed;
                _cameraPanSlider.value = initSpeed;
                _cameraPanSlider.onValueChanged.AddListener(OnCameraSpeedChanged);
            }

            UpdateSpeedLabel(initSpeed);
            RefreshHotkeyLabels();
            SetHotkeyStatus(string.Empty);
        }

        private void OnEnable()
        {
            PanelOpenCoordinator.PanelOpened += OnOtherPanelOpened;
            GameHotkeySettings.Changed += OnHotkeysChanged;
        }

        private void OnDisable()
        {
            PanelOpenCoordinator.PanelOpened -= OnOtherPanelOpened;
            GameHotkeySettings.Changed -= OnHotkeysChanged;
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (_isOpen && _isRebinding && _currentView == SettingsView.HotkeySettings)
            {
                HandleRebindingInput(kb);
                return;
            }

            if (kb.escapeKey.wasPressedThisFrame && _isOpen)
            {
                if (_currentView == SettingsView.RootMenu)
                    Hide();
                else
                    ReturnToRootMenu();
            }
        }

        private void OnDestroy()
        {
            if (_cameraPanSlider != null)
                _cameraPanSlider.onValueChanged.RemoveListener(OnCameraSpeedChanged);

            if (_openButton != null)
                _openButton.onClick.RemoveListener(Toggle);

            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(ReturnToRootMenu);

            if (_saveGameButton != null)
                _saveGameButton.onClick.RemoveListener(OnClickSaveGame);

            if (_openGameSettingsButton != null)
                _openGameSettingsButton.onClick.RemoveListener(ShowGameSettingsPanel);

            if (_openHotkeySettingsButton != null)
                _openHotkeySettingsButton.onClick.RemoveListener(ShowHotkeyPanel);

            if (_closeHotkeySettingsButton != null)
                _closeHotkeySettingsButton.onClick.RemoveListener(ReturnToRootMenu);

            if (_closeRootButton != null)
                _closeRootButton.onClick.RemoveListener(Hide);

            UnbindHotkeyListeners();
            PlayerPrefs.Save();
        }

        public void Toggle() => SetVisible(!_isOpen);
        public void Show() => SetVisible(true);
        public void Hide() => SetVisible(false);
        public bool IsOpen => _isOpen;

        private void OnCameraSpeedChanged(float value)
        {
            if (_camCtrl != null)
                _camCtrl.KeyPanSpeed = value;
            UpdateSpeedLabel(value);
            PlayerPrefs.SetFloat(PrefCameraPanSpeedKey, value);
        }

        private void UpdateSpeedLabel(float value)
        {
            if (_cameraPanLabel != null)
                _cameraPanLabel.text = $"{value:F2}x";
        }

        private void SetVisible(bool visible)
        {
            bool changed = _isOpen != visible;
            _isOpen = visible;
            if (_panel != null)
                _panel.SetActive(visible);

            if (_isRebinding && !visible)
                _isRebinding = false;

            if (visible)
                SetView(_preferredOpenView, false);

            if (visible && changed)
                PanelOpenCoordinator.NotifyOpened(this);

            if (visible)
                GameManager.Instance?.CancelAllModes();

            if (_camCtrl != null)
                _camCtrl.InputLocked = visible;

            if (!visible)
                PlayerPrefs.Save();
        }

        private void ShowGameSettingsPanel()
        {
            SetView(SettingsView.GameSettings);
        }

        private void ShowHotkeyPanel()
        {
            SetView(SettingsView.HotkeySettings);
            SetHotkeyStatus(string.Empty);
        }

        private void ReturnToRootMenu()
        {
            if (_isRebinding)
                CancelRebinding("단축키 변경 취소");

            SetView(SettingsView.RootMenu);
        }

        private void SetView(SettingsView view, bool persist = true)
        {
            _currentView = view;
            if (persist)
            {
                _preferredOpenView = view;
                PlayerPrefs.SetInt(PrefLastViewKey, (int)view);
            }

            SetRootMenuVisible(view == SettingsView.RootMenu);

            if (_gameSettingsContentRoot != null)
                _gameSettingsContentRoot.SetActive(view == SettingsView.GameSettings);

            if (_hotkeySettingsPanelRoot != null)
                _hotkeySettingsPanelRoot.SetActive(view == SettingsView.HotkeySettings);
        }

        private void LoadPreferences()
        {
            int savedView = PlayerPrefs.GetInt(PrefLastViewKey, (int)SettingsView.RootMenu);
            if (savedView < (int)SettingsView.RootMenu || savedView > (int)SettingsView.HotkeySettings)
                savedView = (int)SettingsView.RootMenu;
            _preferredOpenView = (SettingsView)savedView;
        }

        private void SetRootMenuVisible(bool visible)
        {
            if (_rootSettingsMenuRoot != null)
            {
                _rootSettingsMenuRoot.SetActive(visible);
                return;
            }

            if (_autoRootMenuObjects.Count == 0)
                CacheRootMenuObjects();

            foreach (var go in _autoRootMenuObjects)
            {
                if (go != null)
                    go.SetActive(visible);
            }
        }

        private void OnClickSaveGame()
        {
            var saveLoad = GameManager.Instance?.SaveLoad;
            if (saveLoad == null)
            {
                GameEventBus.Publish(new NotificationEvent
                {
                    Message = "저장 시스템을 찾을 수 없습니다.",
                    Type = NotificationType.Warning
                });
                return;
            }

            saveLoad.Save();
        }

        private void OnHotkeysChanged()
        {
            RefreshHotkeyLabels();
        }

        private void HandleRebindingInput(Keyboard kb)
        {
            if (kb == null) return;

            if (kb.escapeKey.wasPressedThisFrame)
            {
                CancelRebinding("단축키 변경 취소");
                return;
            }

            if (!GameHotkeySettings.TryGetPressedKey(kb, out Key pressedKey))
                return;

            if (pressedKey == Key.Escape)
            {
                CancelRebinding("단축키 변경 취소");
                return;
            }

            GameHotkeySettings.SetKey(_rebindingAction, pressedKey);
            _isRebinding = false;

            string actionLabel = GetActionLabel(_rebindingAction);
            string keyLabel = GameHotkeySettings.ToDisplayString(pressedKey);
            SetHotkeyStatus($"{actionLabel} 키가 {keyLabel}(으)로 변경됨");
        }

        private void StartRebinding(GameHotkeyAction action)
        {
            _rebindingAction = action;
            _isRebinding = true;
            SetHotkeyStatus($"[{GetActionLabel(action)}] 새 키 입력 (ESC 취소)");
        }

        private void CancelRebinding(string message)
        {
            _isRebinding = false;
            SetHotkeyStatus(message);
        }

        private void OnClickResetHotkeys()
        {
            GameHotkeySettings.ResetToDefaults();
            _isRebinding = false;
            SetHotkeyStatus("단축키를 기본값으로 복원");
        }

        private void OnClickBindBuildingPanelKey() => StartRebinding(GameHotkeyAction.ToggleBuildingPanel);
        private void OnClickBindRoadModeKey() => StartRebinding(GameHotkeyAction.ToggleRoadMode);
        private void OnClickBindFoundationModeKey() => StartRebinding(GameHotkeyAction.ToggleFoundationMode);
        private void OnClickBindZoneModeKey() => StartRebinding(GameHotkeyAction.ToggleZoneMode);
        private void OnClickBindZoneResidentialKey() => StartRebinding(GameHotkeyAction.ZoneResidential);
        private void OnClickBindZoneCommercialKey() => StartRebinding(GameHotkeyAction.ZoneCommercial);
        private void OnClickBindZoneIndustrialKey() => StartRebinding(GameHotkeyAction.ZoneIndustrial);
        private void OnClickBindZoneClearKey() => StartRebinding(GameHotkeyAction.ZoneClear);
        private void OnClickBindRotateBuildingKey() => StartRebinding(GameHotkeyAction.RotateBuilding);

        private void RefreshHotkeyLabels()
        {
            SetHotkeyLabel(_bindBuildingPanelKeyLabel, _bindBuildingPanelKeyButton, GameHotkeyAction.ToggleBuildingPanel);
            SetHotkeyLabel(_bindRoadModeKeyLabel, _bindRoadModeKeyButton, GameHotkeyAction.ToggleRoadMode);
            SetHotkeyLabel(_bindFoundationModeKeyLabel, _bindFoundationModeKeyButton, GameHotkeyAction.ToggleFoundationMode);
            SetHotkeyLabel(_bindZoneModeKeyLabel, _bindZoneModeKeyButton, GameHotkeyAction.ToggleZoneMode);
            SetHotkeyLabel(_bindZoneResidentialKeyLabel, _bindZoneResidentialKeyButton, GameHotkeyAction.ZoneResidential);
            SetHotkeyLabel(_bindZoneCommercialKeyLabel, _bindZoneCommercialKeyButton, GameHotkeyAction.ZoneCommercial);
            SetHotkeyLabel(_bindZoneIndustrialKeyLabel, _bindZoneIndustrialKeyButton, GameHotkeyAction.ZoneIndustrial);
            SetHotkeyLabel(_bindZoneClearKeyLabel, _bindZoneClearKeyButton, GameHotkeyAction.ZoneClear);
            SetHotkeyLabel(_bindRotateBuildingKeyLabel, _bindRotateBuildingKeyButton, GameHotkeyAction.RotateBuilding);
        }

        private void SetHotkeyLabel(TextMeshProUGUI label, Button button, GameHotkeyAction action)
        {
            string text = $"{GetActionLabel(action)}: {GameHotkeySettings.ToDisplayString(GameHotkeySettings.GetKey(action))}";
            if (label != null)
                label.text = text;
            else if (button != null)
            {
                var btnText = button.GetComponentInChildren<TextMeshProUGUI>(true);
                if (btnText != null) btnText.text = text;
            }
        }

        private void SetHotkeyStatus(string message)
        {
            if (_hotkeyStatusLabel != null)
                _hotkeyStatusLabel.text = message ?? string.Empty;
        }

        private static string GetActionLabel(GameHotkeyAction action)
        {
            switch (action)
            {
                case GameHotkeyAction.ToggleBuildingPanel: return "건물 패널";
                case GameHotkeyAction.ToggleRoadMode: return "도로 모드";
                case GameHotkeyAction.ToggleFoundationMode: return "지반 모드";
                case GameHotkeyAction.ToggleZoneMode: return "구역 모드";
                case GameHotkeyAction.ZoneResidential: return "구역 주거";
                case GameHotkeyAction.ZoneCommercial: return "구역 상업";
                case GameHotkeyAction.ZoneIndustrial: return "구역 공업";
                case GameHotkeyAction.ZoneClear: return "구역 해제";
                case GameHotkeyAction.RotateBuilding: return "건물 회전";
                default: return action.ToString();
            }
        }

        private void OnOtherPanelOpened(object panelOwner)
        {
            if (ReferenceEquals(panelOwner, this)) return;
            if (_isOpen) Hide();
        }

        private void AutoBindButtons()
        {
            if (_openButton == null)
            {
                _openButton = FindButtonByName("SettingBuuton")
                    ?? FindButtonByName("SettingButton")
                    ?? FindButtonByName("SettingsButton")
                    ?? FindButtonByContains("setting");
            }

            if (_gameSettingsContentRoot == null && _panel != null)
            {
                _gameSettingsContentRoot = FindGameObjectInChildrenByName(_panel.transform, "GameSettingsContent")
                    ?? FindGameObjectInChildrenByName(_panel.transform, "SettingPanelUI")
                    ?? FindGameObjectInChildrenByName(_panel.transform, "GameSettingsPanel")
                    ?? FindGameObjectInChildrenByName(_panel.transform, "SettingsContent");
            }

            if (_hotkeySettingsPanelRoot == null && _panel != null)
            {
                _hotkeySettingsPanelRoot = FindGameObjectInChildrenByName(_panel.transform, "HotkeySettingsPanel")
                    ?? FindGameObjectInChildrenByName(_panel.transform, "HotkeyPanel")
                    ?? FindGameObjectInChildrenByName(_panel.transform, "KeybindPanel");
            }

            if (_closeButton == null && _panel != null)
            {
                Transform gameRoot = _gameSettingsContentRoot != null ? _gameSettingsContentRoot.transform : _panel.transform;
                _closeButton = FindButtonInChildrenByName(gameRoot, "SettingsCloseButton")
                    ?? FindButtonInChildrenByName(gameRoot, "SettingCloseButton")
                    ?? FindButtonInChildrenByName(gameRoot, "Exit")
                    ?? FindButtonInChildrenByName(gameRoot, "Close")
                    ?? FindButtonInChildrenByContains(gameRoot, "exit")
                    ?? FindButtonInChildrenByContains(gameRoot, "close");
            }

            if (_saveGameButton == null && _panel != null)
            {
                _saveGameButton = FindButtonInChildrenByName(_panel.transform, "SaveGameButton")
                    ?? FindButtonInChildrenByName(_panel.transform, "SaveButton")
                    ?? FindButtonInChildrenByName(_panel.transform, "QuickSaveButton")
                    ?? FindButtonInChildrenByContains(_panel.transform, "save");
            }

            if (_openGameSettingsButton == null && _panel != null)
            {
                _openGameSettingsButton = FindButtonInChildrenByName(_panel.transform, "OpenGameSettingsButton")
                    ?? FindButtonInChildrenByName(_panel.transform, "GameSettingsButton")
                    ?? FindButtonInChildrenByName(_panel.transform, "GameSettingsPanel")
                    ?? FindButtonInChildrenByContains(_panel.transform, "opengamesetting");
            }

            if (_openHotkeySettingsButton == null && _panel != null)
            {
                _openHotkeySettingsButton = FindButtonInChildrenByName(_panel.transform, "OpenHotkeySettingsButton")
                    ?? FindButtonInChildrenByName(_panel.transform, "HotkeySettingButton")
                    ?? FindButtonInChildrenByName(_panel.transform, "HotkeySettingsButton")
                    ?? FindButtonInChildrenByName(_panel.transform, "HotkeyButton")
                    ?? FindButtonInChildrenByContains(_panel.transform, "hotkeysetting")
                    ?? FindButtonInChildrenByContains(_panel.transform, "keysetting")
                    ?? FindButtonInChildrenByContains(_panel.transform, "openhotkey");
            }

            if (_closeHotkeySettingsButton == null && _panel != null)
            {
                Transform hotkeyRoot = _hotkeySettingsPanelRoot != null ? _hotkeySettingsPanelRoot.transform : _panel.transform;
                _closeHotkeySettingsButton = FindButtonInChildrenByName(hotkeyRoot, "CloseHotkeySettingsButton")
                    ?? FindButtonInChildrenByName(_panel.transform, "BackFromHotkeyButton")
                    ?? FindButtonInChildrenByName(_panel.transform, "HotkeyBackButton")
                    ?? FindButtonInChildrenByName(hotkeyRoot, "Exit")
                    ?? FindButtonInChildrenByName(hotkeyRoot, "Close")
                    ?? FindButtonInChildrenByContains(_panel.transform, "gamesetting")
                    ?? FindButtonInChildrenByContains(_panel.transform, "hotkeyback")
                    ?? FindButtonInChildrenByContains(hotkeyRoot, "exit")
                    ?? FindButtonInChildrenByContains(_panel.transform, "closehotkey");
            }

            if (_closeRootButton == null && _panel != null)
            {
                _closeRootButton = FindDirectChildButtonByName(_panel.transform, "SettingsRootCloseButton")
                    ?? FindDirectChildButtonByName(_panel.transform, "RootSettingsCloseButton")
                    ?? FindDirectChildButtonByName(_panel.transform, "Exit")
                    ?? FindDirectChildButtonByName(_panel.transform, "Exit ")
                    ?? FindDirectChildButtonByContains(_panel.transform, "rootclose");
            }

            CacheRootMenuObjects();
        }

        private void AutoBindHotkeyControls()
        {
            if (_panel == null) return;

            Transform root = _hotkeySettingsPanelRoot != null ? _hotkeySettingsPanelRoot.transform : _panel.transform;

            if (_resetHotkeysButton == null)
                _resetHotkeysButton = FindButtonInChildrenByName(root, "ResetHotkeysButton")
                    ?? FindButtonInChildrenByName(root, "HotkeyResetButton")
                    ?? FindButtonInChildrenByContains(root, "resethotkey");

            _bindBuildingPanelKeyButton ??= FindButtonInChildrenByName(root, "BindBuildingPanelKeyButton");
            _bindRoadModeKeyButton ??= FindButtonInChildrenByName(root, "BindRoadModeKeyButton");
            _bindFoundationModeKeyButton ??= FindButtonInChildrenByName(root, "BindFoundationModeKeyButton");
            _bindZoneModeKeyButton ??= FindButtonInChildrenByName(root, "BindZoneModeKeyButton");
            _bindZoneResidentialKeyButton ??= FindButtonInChildrenByName(root, "BindZoneResidentialKeyButton");
            _bindZoneCommercialKeyButton ??= FindButtonInChildrenByName(root, "BindZoneCommercialKeyButton");
            _bindZoneIndustrialKeyButton ??= FindButtonInChildrenByName(root, "BindZoneIndustrialKeyButton");
            _bindZoneClearKeyButton ??= FindButtonInChildrenByName(root, "BindZoneClearKeyButton");
            _bindRotateBuildingKeyButton ??= FindButtonInChildrenByName(root, "BindRotateBuildingKeyButton");

            _bindBuildingPanelKeyLabel ??= FindTextInChildrenByName(root, "BindBuildingPanelKeyLabel");
            _bindRoadModeKeyLabel ??= FindTextInChildrenByName(root, "BindRoadModeKeyLabel");
            _bindFoundationModeKeyLabel ??= FindTextInChildrenByName(root, "BindFoundationModeKeyLabel");
            _bindZoneModeKeyLabel ??= FindTextInChildrenByName(root, "BindZoneModeKeyLabel");
            _bindZoneResidentialKeyLabel ??= FindTextInChildrenByName(root, "BindZoneResidentialKeyLabel");
            _bindZoneCommercialKeyLabel ??= FindTextInChildrenByName(root, "BindZoneCommercialKeyLabel");
            _bindZoneIndustrialKeyLabel ??= FindTextInChildrenByName(root, "BindZoneIndustrialKeyLabel");
            _bindZoneClearKeyLabel ??= FindTextInChildrenByName(root, "BindZoneClearKeyLabel");
            _bindRotateBuildingKeyLabel ??= FindTextInChildrenByName(root, "BindRotateBuildingKeyLabel");

            if (_hotkeyStatusLabel == null)
                _hotkeyStatusLabel = FindTextInChildrenByName(root, "HotkeyStatusLabel")
                    ?? FindTextInChildrenByContains(root, "hotkeystatus")
                    ?? FindTextInChildrenByContains(root, "keybindstatus");
        }

        private void BindButtonListeners()
        {
            if (_openButton != null)
            {
                _openButton.onClick.RemoveListener(Toggle);
                _openButton.onClick.AddListener(Toggle);
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(ReturnToRootMenu);
                _closeButton.onClick.AddListener(ReturnToRootMenu);
            }

            if (_saveGameButton != null)
            {
                _saveGameButton.onClick.RemoveListener(OnClickSaveGame);
                _saveGameButton.onClick.AddListener(OnClickSaveGame);
            }

            if (_openGameSettingsButton != null)
            {
                _openGameSettingsButton.onClick.RemoveListener(ShowGameSettingsPanel);
                _openGameSettingsButton.onClick.AddListener(ShowGameSettingsPanel);
            }

            if (_openHotkeySettingsButton != null)
            {
                _openHotkeySettingsButton.onClick.RemoveListener(ShowHotkeyPanel);
                _openHotkeySettingsButton.onClick.AddListener(ShowHotkeyPanel);
            }

            if (_closeHotkeySettingsButton != null)
            {
                _closeHotkeySettingsButton.onClick.RemoveListener(ReturnToRootMenu);
                _closeHotkeySettingsButton.onClick.AddListener(ReturnToRootMenu);
            }

            if (_closeRootButton != null)
            {
                _closeRootButton.onClick.RemoveListener(Hide);
                _closeRootButton.onClick.AddListener(Hide);
            }
        }

        private void BindHotkeyListeners()
        {
            _resetHotkeysButton?.onClick.AddListener(OnClickResetHotkeys);
            _bindBuildingPanelKeyButton?.onClick.AddListener(OnClickBindBuildingPanelKey);
            _bindRoadModeKeyButton?.onClick.AddListener(OnClickBindRoadModeKey);
            _bindFoundationModeKeyButton?.onClick.AddListener(OnClickBindFoundationModeKey);
            _bindZoneModeKeyButton?.onClick.AddListener(OnClickBindZoneModeKey);
            _bindZoneResidentialKeyButton?.onClick.AddListener(OnClickBindZoneResidentialKey);
            _bindZoneCommercialKeyButton?.onClick.AddListener(OnClickBindZoneCommercialKey);
            _bindZoneIndustrialKeyButton?.onClick.AddListener(OnClickBindZoneIndustrialKey);
            _bindZoneClearKeyButton?.onClick.AddListener(OnClickBindZoneClearKey);
            _bindRotateBuildingKeyButton?.onClick.AddListener(OnClickBindRotateBuildingKey);
        }

        private void UnbindHotkeyListeners()
        {
            _resetHotkeysButton?.onClick.RemoveListener(OnClickResetHotkeys);
            _bindBuildingPanelKeyButton?.onClick.RemoveListener(OnClickBindBuildingPanelKey);
            _bindRoadModeKeyButton?.onClick.RemoveListener(OnClickBindRoadModeKey);
            _bindFoundationModeKeyButton?.onClick.RemoveListener(OnClickBindFoundationModeKey);
            _bindZoneModeKeyButton?.onClick.RemoveListener(OnClickBindZoneModeKey);
            _bindZoneResidentialKeyButton?.onClick.RemoveListener(OnClickBindZoneResidentialKey);
            _bindZoneCommercialKeyButton?.onClick.RemoveListener(OnClickBindZoneCommercialKey);
            _bindZoneIndustrialKeyButton?.onClick.RemoveListener(OnClickBindZoneIndustrialKey);
            _bindZoneClearKeyButton?.onClick.RemoveListener(OnClickBindZoneClearKey);
            _bindRotateBuildingKeyButton?.onClick.RemoveListener(OnClickBindRotateBuildingKey);
        }

        private static Button FindButtonByName(string objName)
        {
            var go = GameObject.Find(objName);
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

        private static Button FindButtonInChildrenByName(Transform parent, string objName)
        {
            if (parent == null) return null;
            var transforms = parent.GetComponentsInChildren<Transform>(true);
            foreach (var tr in transforms)
            {
                if (tr == null) continue;
                if (tr.name != objName) continue;
                var btn = tr.GetComponent<Button>();
                if (btn != null) return btn;
            }
            return null;
        }

        private static Button FindButtonInChildrenByContains(Transform parent, string textLower)
        {
            if (parent == null) return null;
            var buttons = parent.GetComponentsInChildren<Button>(true);
            foreach (var button in buttons)
            {
                if (button == null || button.gameObject == null) continue;
                string nameLower = button.gameObject.name.ToLowerInvariant();
                if (nameLower.Contains(textLower))
                    return button;
            }
            return null;
        }

        private static TextMeshProUGUI FindTextInChildrenByName(Transform parent, string objName)
        {
            if (parent == null) return null;
            var texts = parent.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var text in texts)
            {
                if (text == null || text.gameObject == null) continue;
                if (text.gameObject.name == objName) return text;
            }
            return null;
        }

        private static TextMeshProUGUI FindTextInChildrenByContains(Transform parent, string textLower)
        {
            if (parent == null) return null;
            var texts = parent.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var text in texts)
            {
                if (text == null || text.gameObject == null) continue;
                string nameLower = text.gameObject.name.ToLowerInvariant();
                if (nameLower.Contains(textLower))
                    return text;
            }
            return null;
        }

        private void CacheRootMenuObjects()
        {
            _autoRootMenuObjects.Clear();

            if (_panel == null || _rootSettingsMenuRoot != null)
                return;

            foreach (Transform child in _panel.transform)
            {
                if (child == null) continue;
                GameObject childObject = child.gameObject;

                if (_gameSettingsContentRoot != null && childObject == _gameSettingsContentRoot)
                    continue;
                if (_hotkeySettingsPanelRoot != null && childObject == _hotkeySettingsPanelRoot)
                    continue;

                _autoRootMenuObjects.Add(childObject);
            }
        }

        private static GameObject FindGameObjectInChildrenByName(Transform parent, string objName)
        {
            if (parent == null) return null;
            var transforms = parent.GetComponentsInChildren<Transform>(true);
            foreach (var tr in transforms)
            {
                if (tr == null) continue;
                if (tr.name == objName) return tr.gameObject;
            }
            return null;
        }

        private static Button FindDirectChildButtonByName(Transform parent, string objName)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child == null || child.name != objName) continue;
                Button btn = child.GetComponent<Button>();
                if (btn != null) return btn;
            }
            return null;
        }

        private static Button FindDirectChildButtonByContains(Transform parent, string textLower)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child == null || child.gameObject == null) continue;
                string nameLower = child.gameObject.name.ToLowerInvariant();
                if (!nameLower.Contains(textLower)) continue;
                Button btn = child.GetComponent<Button>();
                if (btn != null) return btn;
            }
            return null;
        }
    }
}

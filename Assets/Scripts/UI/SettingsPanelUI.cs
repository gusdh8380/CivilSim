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

        [Header("열기/닫기 버튼 (미할당 시 자동 탐색)")]
        [SerializeField] private Button _openButton;
        [SerializeField] private Button _closeButton;

        [Header("저장 버튼 (선택)")]
        [SerializeField] private Button _saveGameButton;

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

        private RTSCameraController _camCtrl;
        private bool _isOpen;
        private bool _isRebinding;
        private GameHotkeyAction _rebindingAction;

        private void Awake()
        {
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

            if (_isOpen && _isRebinding)
            {
                HandleRebindingInput(kb);
                return;
            }

            if (kb.escapeKey.wasPressedThisFrame && _isOpen)
                Hide();
        }

        private void OnDestroy()
        {
            if (_cameraPanSlider != null)
                _cameraPanSlider.onValueChanged.RemoveListener(OnCameraSpeedChanged);

            if (_openButton != null)
                _openButton.onClick.RemoveListener(Toggle);

            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(Hide);

            if (_saveGameButton != null)
                _saveGameButton.onClick.RemoveListener(OnClickSaveGame);

            UnbindHotkeyListeners();
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

            if (!visible && _isRebinding)
                _isRebinding = false;

            if (visible && changed)
                PanelOpenCoordinator.NotifyOpened(this);

            if (visible)
                GameManager.Instance?.CancelAllModes();

            if (_camCtrl != null)
                _camCtrl.InputLocked = visible;
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

            if (_closeButton == null && _panel != null)
            {
                _closeButton = FindButtonInChildrenByName(_panel.transform, "Exit")
                    ?? FindButtonInChildrenByName(_panel.transform, "Close")
                    ?? FindButtonInChildrenByContains(_panel.transform, "exit")
                    ?? FindButtonInChildrenByContains(_panel.transform, "close");
            }

            if (_saveGameButton == null && _panel != null)
            {
                _saveGameButton = FindButtonInChildrenByName(_panel.transform, "SaveGameButton")
                    ?? FindButtonInChildrenByName(_panel.transform, "SaveButton")
                    ?? FindButtonInChildrenByName(_panel.transform, "QuickSaveButton")
                    ?? FindButtonInChildrenByContains(_panel.transform, "save");
            }
        }

        private void AutoBindHotkeyControls()
        {
            if (_panel == null) return;

            Transform root = _panel.transform;

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
                _closeButton.onClick.RemoveListener(Hide);
                _closeButton.onClick.AddListener(Hide);
            }

            if (_saveGameButton != null)
            {
                _saveGameButton.onClick.RemoveListener(OnClickSaveGame);
                _saveGameButton.onClick.AddListener(OnClickSaveGame);
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
    }
}

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

        [Header("카메라 이동 속도")]
        [SerializeField] private Slider _cameraPanSlider;
        [SerializeField] private TextMeshProUGUI _cameraPanLabel;

        [Header("카메라 속도 범위")]
        [SerializeField, Range(0.05f, 0.5f)] private float _minPanSpeed = 0.05f;
        [SerializeField, Range(0.5f, 3f)] private float _maxPanSpeed = 2f;
        [SerializeField, Range(0.05f, 3f)] private float _defaultPanSpeed = 0.4f;

        private RTSCameraController _camCtrl;
        private bool _isOpen;

        private void Awake()
        {
            _camCtrl = FindFirstObjectByType<RTSCameraController>();
            AutoBindButtons();
            BindButtonListeners();
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
        }

        private void OnEnable()
        {
            PanelOpenCoordinator.PanelOpened += OnOtherPanelOpened;
        }

        private void OnDisable()
        {
            PanelOpenCoordinator.PanelOpened -= OnOtherPanelOpened;
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

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

            if (visible && changed)
                PanelOpenCoordinator.NotifyOpened(this);

            if (visible)
                GameManager.Instance?.CancelAllModes();

            if (_camCtrl != null)
                _camCtrl.InputLocked = visible;
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
    }
}

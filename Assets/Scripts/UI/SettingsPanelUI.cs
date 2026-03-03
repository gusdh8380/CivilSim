using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using CivilSim.CameraSystem;
using CivilSim.Core;
using CivilSim.Economy;
using CivilSim.Population;

namespace CivilSim.UI
{
    /// <summary>
    /// 게임 설정 패널 (카메라 이동 속도 등 런타임 조정).
    ///
    /// 권장 씬 구성:
    ///   Canvas
    ///   -- SettingsPanelRoot (이 스크립트 — 항상 Active)
    ///       -- SettingsPanel (Image/Panel) ← _panel
    ///           ├-- Title (TMP_Text)  "Settings 설정"
    ///           ├-- CamSpeedRow
    ///              ├-- Label (TMP_Text) "카메라 이동 속도"
    ///              ├-- CamSpeedSlider (Slider) ← _cameraPanSlider
    ///              -- CamSpeedValue (TMP_Text) ← _cameraPanLabel
    ///           -- CloseButton (Button) -> onClick: SettingsPanelUI.Toggle()
    ///
    /// 열기/닫기:
    ///   - HUD의 Settings 버튼 -> Toggle() 연결
    ///   - 코드: GameManager.Instance?.Settings?.Toggle()
    /// </summary>
    public class SettingsPanelUI : MonoBehaviour
    {
        // -- 인스펙터 --
        [Header("패널 루트 (자식 오브젝트를 할당)")]
        [SerializeField] private GameObject _panel;

        [Header("버튼 (미할당 시 자동 탐색)")]
        [SerializeField] private Button _openButton;
        [SerializeField] private Button _closeButton;

        [Header("카메라 이동 속도 슬라이더")]
        [SerializeField] private Slider          _cameraPanSlider;
        [SerializeField] private TextMeshProUGUI _cameraPanLabel;

        [Header("슬라이더 범위")]
        [SerializeField, Range(0.05f, 0.5f)]  private float _minPanSpeed = 0.05f;
        [SerializeField, Range(0.5f,  3f)]    private float _maxPanSpeed = 2f;
        [Tooltip("RTSCameraController._keyPanSpeed 와 동일한 기본값")]
        [SerializeField, Range(0.05f, 3f)]    private float _defaultPanSpeed = 0.4f;

        [Header("정책 슬라이더 (선택)")]
        [SerializeField] private Slider _residentTaxSlider;
        [SerializeField] private TextMeshProUGUI _residentTaxLabel;
        [SerializeField] private Slider _jobTaxSlider;
        [SerializeField] private TextMeshProUGUI _jobTaxLabel;
        [SerializeField] private Slider _commercialDemandFactorSlider;
        [SerializeField] private TextMeshProUGUI _commercialDemandFactorLabel;
        [SerializeField] private Slider _industrialDemandFactorSlider;
        [SerializeField] private TextMeshProUGUI _industrialDemandFactorLabel;

        [Header("정책 범위")]
        [SerializeField, Range(0, 500)] private int _minResidentTax = 0;
        [SerializeField, Range(50, 1000)] private int _maxResidentTax = 300;
        [SerializeField, Range(0, 500)] private int _minJobTax = 0;
        [SerializeField, Range(50, 1000)] private int _maxJobTax = 300;
        [SerializeField, Range(0.05f, 1.0f)] private float _minDemandFactor = 0.05f;
        [SerializeField, Range(0.05f, 1.0f)] private float _maxDemandFactor = 1.0f;

        // -- 내부 상태 --
        private RTSCameraController _camCtrl;
        private EconomyManager _economy;
        private CityDemandSystem _demand;
        private bool                _isOpen;

        // -- Unity --

        private void Awake()
        {
            _camCtrl = FindObjectOfType<RTSCameraController>();
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
                _cameraPanSlider.value    = initSpeed;
                _cameraPanSlider.onValueChanged.AddListener(OnCameraSpeedChanged);
            }

            UpdateSpeedLabel(initSpeed);
            InitializePolicyControls();
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

            if (_residentTaxSlider != null)
                _residentTaxSlider.onValueChanged.RemoveListener(OnResidentTaxChanged);
            if (_jobTaxSlider != null)
                _jobTaxSlider.onValueChanged.RemoveListener(OnJobTaxChanged);
            if (_commercialDemandFactorSlider != null)
                _commercialDemandFactorSlider.onValueChanged.RemoveListener(OnCommercialDemandFactorChanged);
            if (_industrialDemandFactorSlider != null)
                _industrialDemandFactorSlider.onValueChanged.RemoveListener(OnIndustrialDemandFactorChanged);

            if (_openButton != null)
                _openButton.onClick.RemoveListener(Show);

            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(Hide);
        }

        // -- 공개 API --

        public void Toggle()  => SetVisible(!_isOpen);
        public void Show()    => SetVisible(true);
        public void Hide()    => SetVisible(false);
        public bool IsOpen    => _isOpen;

        // -- 슬라이더 콜백 --

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

        private void InitializePolicyControls()
        {
            _economy = GameManager.Instance?.Economy;
            _demand = GameManager.Instance?.Demand;

            if (_residentTaxSlider != null)
            {
                _residentTaxSlider.minValue = _minResidentTax;
                _residentTaxSlider.maxValue = _maxResidentTax;
                _residentTaxSlider.value = _economy != null ? _economy.ResidentTaxPerMonth : _minResidentTax;
                _residentTaxSlider.onValueChanged.AddListener(OnResidentTaxChanged);
                UpdateResidentTaxLabel(Mathf.RoundToInt(_residentTaxSlider.value));
            }

            if (_jobTaxSlider != null)
            {
                _jobTaxSlider.minValue = _minJobTax;
                _jobTaxSlider.maxValue = _maxJobTax;
                _jobTaxSlider.value = _economy != null ? _economy.JobTaxPerMonth : _minJobTax;
                _jobTaxSlider.onValueChanged.AddListener(OnJobTaxChanged);
                UpdateJobTaxLabel(Mathf.RoundToInt(_jobTaxSlider.value));
            }

            if (_commercialDemandFactorSlider != null)
            {
                _commercialDemandFactorSlider.minValue = _minDemandFactor;
                _commercialDemandFactorSlider.maxValue = _maxDemandFactor;
                _commercialDemandFactorSlider.value = _demand != null ? _demand.CommercialDemandFactor : _minDemandFactor;
                _commercialDemandFactorSlider.onValueChanged.AddListener(OnCommercialDemandFactorChanged);
                UpdateCommercialDemandFactorLabel(_commercialDemandFactorSlider.value);
            }

            if (_industrialDemandFactorSlider != null)
            {
                _industrialDemandFactorSlider.minValue = _minDemandFactor;
                _industrialDemandFactorSlider.maxValue = _maxDemandFactor;
                _industrialDemandFactorSlider.value = _demand != null ? _demand.IndustrialDemandFactor : _minDemandFactor;
                _industrialDemandFactorSlider.onValueChanged.AddListener(OnIndustrialDemandFactorChanged);
                UpdateIndustrialDemandFactorLabel(_industrialDemandFactorSlider.value);
            }
        }

        private void OnResidentTaxChanged(float value)
        {
            int tax = Mathf.RoundToInt(value);
            if (_economy == null) _economy = GameManager.Instance?.Economy;
            _economy?.SetResidentTaxPerMonth(tax);
            UpdateResidentTaxLabel(tax);
        }

        private void OnJobTaxChanged(float value)
        {
            int tax = Mathf.RoundToInt(value);
            if (_economy == null) _economy = GameManager.Instance?.Economy;
            _economy?.SetJobTaxPerMonth(tax);
            UpdateJobTaxLabel(tax);
        }

        private void OnCommercialDemandFactorChanged(float value)
        {
            if (_demand == null) _demand = GameManager.Instance?.Demand;
            _demand?.SetCommercialDemandFactor(value);
            UpdateCommercialDemandFactorLabel(value);
        }

        private void OnIndustrialDemandFactorChanged(float value)
        {
            if (_demand == null) _demand = GameManager.Instance?.Demand;
            _demand?.SetIndustrialDemandFactor(value);
            UpdateIndustrialDemandFactorLabel(value);
        }

        private void UpdateResidentTaxLabel(int value)
        {
            if (_residentTaxLabel != null)
                _residentTaxLabel.text = $"거주세 {value}";
        }

        private void UpdateJobTaxLabel(int value)
        {
            if (_jobTaxLabel != null)
                _jobTaxLabel.text = $"고용세 {value}";
        }

        private void UpdateCommercialDemandFactorLabel(float value)
        {
            if (_commercialDemandFactorLabel != null)
                _commercialDemandFactorLabel.text = $"상업계수 {value:F2}";
        }

        private void UpdateIndustrialDemandFactorLabel(float value)
        {
            if (_industrialDemandFactorLabel != null)
                _industrialDemandFactorLabel.text = $"공업계수 {value:F2}";
        }

        // -- 내부 --

        private void SetVisible(bool visible)
        {
            _isOpen = visible;
            if (_panel != null)
                _panel.SetActive(visible);

            // 설정창이 열리면 배치 모드/카메라 입력 잠금
            if (visible)
                GameManager.Instance?.CancelAllModes();

            if (_camCtrl != null)
                _camCtrl.InputLocked = visible;
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
                _openButton.onClick.RemoveListener(Show);
                _openButton.onClick.AddListener(Show);
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
            var buttons = FindObjectsOfType<Button>(true);
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

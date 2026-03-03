using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using CivilSim.Core;
using CivilSim.Economy;
using CivilSim.Population;

namespace CivilSim.UI
{
    /// <summary>
    /// 정책 설정 패널 (세율 / 수요 계수) 런타임 조정.
    /// 환경 설정(SettingsPanelUI)과 분리된 운영 정책 전용 UI.
    /// </summary>
    public class PolicyPanelUI : MonoBehaviour
    {
        [System.Serializable]
        private struct PolicyPreset
        {
            public int ResidentTax;
            public int JobTax;
            public float CommercialDemandFactor;
            public float IndustrialDemandFactor;
        }

        [Header("패널 루트 (자식 오브젝트)")]
        [SerializeField] private GameObject _panel;

        [Header("열기/닫기 버튼 (미할당 시 자동 탐색)")]
        [SerializeField] private Button _openButton;
        [SerializeField] private Button _closeButton;

        [Header("정책 슬라이더")]
        [SerializeField] private Slider _residentTaxSlider;
        [SerializeField] private TextMeshProUGUI _residentTaxLabel;
        [SerializeField] private Slider _jobTaxSlider;
        [SerializeField] private TextMeshProUGUI _jobTaxLabel;
        [SerializeField] private Slider _commercialDemandFactorSlider;
        [SerializeField] private TextMeshProUGUI _commercialDemandFactorLabel;
        [SerializeField] private Slider _industrialDemandFactorSlider;
        [SerializeField] private TextMeshProUGUI _industrialDemandFactorLabel;

        [Header("정책 프리셋 버튼 (선택)")]
        [SerializeField] private Button _balancedPresetButton;
        [SerializeField] private Button _growthPresetButton;
        [SerializeField] private Button _austerityPresetButton;
        [SerializeField] private TextMeshProUGUI _presetStateLabel;

        [Header("정책 범위")]
        [SerializeField, Range(0, 500)] private int _minResidentTax = 0;
        [SerializeField, Range(50, 1000)] private int _maxResidentTax = 300;
        [SerializeField, Range(0, 500)] private int _minJobTax = 0;
        [SerializeField, Range(50, 1000)] private int _maxJobTax = 300;
        [SerializeField, Range(0.05f, 1.0f)] private float _minDemandFactor = 0.05f;
        [SerializeField, Range(0.05f, 1.0f)] private float _maxDemandFactor = 1.0f;

        [Header("프리셋 값")]
        [SerializeField] private PolicyPreset _balancedPreset = new PolicyPreset
        {
            ResidentTax = 100,
            JobTax = 80,
            CommercialDemandFactor = 0.25f,
            IndustrialDemandFactor = 0.20f
        };
        [SerializeField] private PolicyPreset _growthPreset = new PolicyPreset
        {
            ResidentTax = 70,
            JobTax = 60,
            CommercialDemandFactor = 0.40f,
            IndustrialDemandFactor = 0.30f
        };
        [SerializeField] private PolicyPreset _austerityPreset = new PolicyPreset
        {
            ResidentTax = 150,
            JobTax = 120,
            CommercialDemandFactor = 0.18f,
            IndustrialDemandFactor = 0.12f
        };

        private EconomyManager _economy;
        private CityDemandSystem _demand;
        private bool _isOpen;
        private bool _isApplyingPreset;

        private void Awake()
        {
            AutoBindButtons();
            AutoBindPresetButtons();
            BindButtonListeners();
            BindPresetButtonListeners();
            SetVisible(false);
        }

        private void Start()
        {
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
            if (_residentTaxSlider != null)
                _residentTaxSlider.onValueChanged.RemoveListener(OnResidentTaxChanged);
            if (_jobTaxSlider != null)
                _jobTaxSlider.onValueChanged.RemoveListener(OnJobTaxChanged);
            if (_commercialDemandFactorSlider != null)
                _commercialDemandFactorSlider.onValueChanged.RemoveListener(OnCommercialDemandFactorChanged);
            if (_industrialDemandFactorSlider != null)
                _industrialDemandFactorSlider.onValueChanged.RemoveListener(OnIndustrialDemandFactorChanged);

            if (_openButton != null)
                _openButton.onClick.RemoveListener(Toggle);
            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(Hide);
            if (_balancedPresetButton != null)
                _balancedPresetButton.onClick.RemoveListener(ApplyBalancedPreset);
            if (_growthPresetButton != null)
                _growthPresetButton.onClick.RemoveListener(ApplyGrowthPreset);
            if (_austerityPresetButton != null)
                _austerityPresetButton.onClick.RemoveListener(ApplyAusterityPreset);
        }

        public void Toggle() => SetVisible(!_isOpen);
        public void Show() => SetVisible(true);
        public void Hide() => SetVisible(false);
        public bool IsOpen => _isOpen;

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

            UpdatePresetStateLabel("사용자 정의");
        }

        private void OnResidentTaxChanged(float value)
        {
            int tax = Mathf.RoundToInt(value);
            if (_economy == null) _economy = GameManager.Instance?.Economy;
            _economy?.SetResidentTaxPerMonth(tax);
            UpdateResidentTaxLabel(tax);
            if (!_isApplyingPreset) UpdatePresetStateLabel("사용자 정의");
        }

        private void OnJobTaxChanged(float value)
        {
            int tax = Mathf.RoundToInt(value);
            if (_economy == null) _economy = GameManager.Instance?.Economy;
            _economy?.SetJobTaxPerMonth(tax);
            UpdateJobTaxLabel(tax);
            if (!_isApplyingPreset) UpdatePresetStateLabel("사용자 정의");
        }

        private void OnCommercialDemandFactorChanged(float value)
        {
            if (_demand == null) _demand = GameManager.Instance?.Demand;
            _demand?.SetCommercialDemandFactor(value);
            UpdateCommercialDemandFactorLabel(value);
            if (!_isApplyingPreset) UpdatePresetStateLabel("사용자 정의");
        }

        private void OnIndustrialDemandFactorChanged(float value)
        {
            if (_demand == null) _demand = GameManager.Instance?.Demand;
            _demand?.SetIndustrialDemandFactor(value);
            UpdateIndustrialDemandFactorLabel(value);
            if (!_isApplyingPreset) UpdatePresetStateLabel("사용자 정의");
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

        private void SetVisible(bool visible)
        {
            _isOpen = visible;
            if (_panel != null)
                _panel.SetActive(visible);

            if (visible)
                GameManager.Instance?.CancelAllModes();
        }

        private void ApplyBalancedPreset()
        {
            ApplyPreset("균형", _balancedPreset);
        }

        private void ApplyGrowthPreset()
        {
            ApplyPreset("성장", _growthPreset);
        }

        private void ApplyAusterityPreset()
        {
            ApplyPreset("긴축", _austerityPreset);
        }

        private void ApplyPreset(string presetName, PolicyPreset preset)
        {
            _isApplyingPreset = true;

            int residentTax = Mathf.Clamp(preset.ResidentTax, _minResidentTax, _maxResidentTax);
            int jobTax = Mathf.Clamp(preset.JobTax, _minJobTax, _maxJobTax);
            float commercialFactor = Mathf.Clamp(preset.CommercialDemandFactor, _minDemandFactor, _maxDemandFactor);
            float industrialFactor = Mathf.Clamp(preset.IndustrialDemandFactor, _minDemandFactor, _maxDemandFactor);

            if (_residentTaxSlider != null)
                _residentTaxSlider.SetValueWithoutNotify(residentTax);
            if (_jobTaxSlider != null)
                _jobTaxSlider.SetValueWithoutNotify(jobTax);
            if (_commercialDemandFactorSlider != null)
                _commercialDemandFactorSlider.SetValueWithoutNotify(commercialFactor);
            if (_industrialDemandFactorSlider != null)
                _industrialDemandFactorSlider.SetValueWithoutNotify(industrialFactor);

            OnResidentTaxChanged(residentTax);
            OnJobTaxChanged(jobTax);
            OnCommercialDemandFactorChanged(commercialFactor);
            OnIndustrialDemandFactorChanged(industrialFactor);

            _isApplyingPreset = false;
            UpdatePresetStateLabel(presetName);
            GameEventBus.Publish(new NotificationEvent
            {
                Message = $"정책 프리셋 적용: {presetName}",
                Type = NotificationType.Info
            });
        }

        private void AutoBindButtons()
        {
            if (_openButton == null)
            {
                _openButton = FindButtonByName("PolicyButton")
                    ?? FindButtonByName("PolicyOpenButton")
                    ?? FindButtonByContains("policy");
            }

            if (_closeButton == null && _panel != null)
            {
                _closeButton = FindButtonInChildrenByName(_panel.transform, "Exit")
                    ?? FindButtonInChildrenByName(_panel.transform, "Close")
                    ?? FindButtonInChildrenByContains(_panel.transform, "exit")
                    ?? FindButtonInChildrenByContains(_panel.transform, "close");
            }
        }

        private void AutoBindPresetButtons()
        {
            if (_panel == null) return;

            if (_balancedPresetButton == null)
            {
                _balancedPresetButton = FindButtonInChildrenByName(_panel.transform, "BalancedPresetButton")
                    ?? FindButtonInChildrenByName(_panel.transform, "BalancedButton")
                    ?? FindButtonInChildrenByContains(_panel.transform, "balanced");
            }

            if (_growthPresetButton == null)
            {
                _growthPresetButton = FindButtonInChildrenByName(_panel.transform, "GrowthPresetButton")
                    ?? FindButtonInChildrenByName(_panel.transform, "GrowthButton")
                    ?? FindButtonInChildrenByContains(_panel.transform, "growth");
            }

            if (_austerityPresetButton == null)
            {
                _austerityPresetButton = FindButtonInChildrenByName(_panel.transform, "AusterityPresetButton")
                    ?? FindButtonInChildrenByName(_panel.transform, "AusterityButton")
                    ?? FindButtonInChildrenByContains(_panel.transform, "austerity");
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

        private void BindPresetButtonListeners()
        {
            if (_balancedPresetButton != null)
            {
                _balancedPresetButton.onClick.RemoveListener(ApplyBalancedPreset);
                _balancedPresetButton.onClick.AddListener(ApplyBalancedPreset);
            }

            if (_growthPresetButton != null)
            {
                _growthPresetButton.onClick.RemoveListener(ApplyGrowthPreset);
                _growthPresetButton.onClick.AddListener(ApplyGrowthPreset);
            }

            if (_austerityPresetButton != null)
            {
                _austerityPresetButton.onClick.RemoveListener(ApplyAusterityPreset);
                _austerityPresetButton.onClick.AddListener(ApplyAusterityPreset);
            }
        }

        private void UpdatePresetStateLabel(string presetName)
        {
            if (_presetStateLabel != null)
                _presetStateLabel.text = presetName;
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

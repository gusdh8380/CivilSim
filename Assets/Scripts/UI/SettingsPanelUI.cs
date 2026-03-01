using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CivilSim.CameraSystem;

namespace CivilSim.UI
{
    /// <summary>
    /// 게임 설정 패널 (카메라 이동 속도 등 런타임 조정).
    ///
    /// 권장 씬 구성:
    ///   Canvas
    ///   └── SettingsPanelRoot (이 스크립트 — 항상 Active)
    ///       └── SettingsPanel (Image/Panel) ← _panel
    ///           ├── Title (TMP_Text)  "⚙ 설정"
    ///           ├── CamSpeedRow
    ///           │   ├── Label (TMP_Text) "카메라 이동 속도"
    ///           │   ├── CamSpeedSlider (Slider) ← _cameraPanSlider
    ///           │   └── CamSpeedValue (TMP_Text) ← _cameraPanLabel
    ///           └── CloseButton (Button) → onClick: SettingsPanelUI.Toggle()
    ///
    /// 열기/닫기:
    ///   - HUD의 ⚙ 버튼 → Toggle() 연결
    ///   - 코드: GameManager.Instance?.Settings?.Toggle()
    /// </summary>
    public class SettingsPanelUI : MonoBehaviour
    {
        // ── 인스펙터 ──────────────────────────────────────────
        [Header("패널 루트 (자식 오브젝트를 할당)")]
        [SerializeField] private GameObject _panel;

        [Header("카메라 이동 속도 슬라이더")]
        [SerializeField] private Slider          _cameraPanSlider;
        [SerializeField] private TextMeshProUGUI _cameraPanLabel;

        [Header("슬라이더 범위")]
        [SerializeField, Range(0.05f, 0.5f)]  private float _minPanSpeed = 0.05f;
        [SerializeField, Range(0.5f,  3f)]    private float _maxPanSpeed = 2f;
        [Tooltip("RTSCameraController._keyPanSpeed 와 동일한 기본값")]
        [SerializeField, Range(0.05f, 3f)]    private float _defaultPanSpeed = 0.4f;

        // ── 내부 상태 ─────────────────────────────────────────
        private RTSCameraController _camCtrl;
        private bool                _isOpen;

        // ── Unity ────────────────────────────────────────────

        private void Awake()
        {
            _camCtrl = FindObjectOfType<RTSCameraController>();
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
        }

        // ── 공개 API ──────────────────────────────────────────

        public void Toggle()  => SetVisible(!_isOpen);
        public void Show()    => SetVisible(true);
        public void Hide()    => SetVisible(false);
        public bool IsOpen    => _isOpen;

        // ── 슬라이더 콜백 ─────────────────────────────────────

        private void OnCameraSpeedChanged(float value)
        {
            if (_camCtrl != null)
                _camCtrl.KeyPanSpeed = value;
            UpdateSpeedLabel(value);
        }

        private void UpdateSpeedLabel(float value)
        {
            if (_cameraPanLabel != null)
                _cameraPanLabel.text = $"{value:F2}×";
        }

        // ── 내부 ─────────────────────────────────────────────

        private void SetVisible(bool visible)
        {
            _isOpen = visible;
            if (_panel != null)
                _panel.SetActive(visible);
        }
    }
}

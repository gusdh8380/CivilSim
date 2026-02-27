using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using CivilSim.Buildings;

namespace CivilSim.UI
{
    /// <summary>
    /// 건물 버튼 호버 시 표시되는 툴팁 패널.
    /// 싱글턴 패턴으로 BuildingButtonUI에서 정적 메서드로 호출한다.
    ///
    /// 씬 구성:
    ///   Canvas
    ///   └── Tooltip (이 컴포넌트, RectTransform)
    ///       ├── Name         (TMP_Text)
    ///       ├── Category     (TMP_Text)
    ///       ├── Size         (TMP_Text)
    ///       ├── Cost         (TMP_Text)
    ///       ├── Maintenance  (TMP_Text)
    ///       ├── Population   (TMP_Text)
    ///       └── Description  (TMP_Text)
    /// </summary>
    public class BuildingTooltipUI : MonoBehaviour
    {
        // ── 인스펙터 ──────────────────────────────────────────
        [Header("UI 요소")]
        [SerializeField] private GameObject         _panel;
        [SerializeField] private TextMeshProUGUI    _nameText;
        [SerializeField] private TextMeshProUGUI    _categoryText;
        [SerializeField] private TextMeshProUGUI    _sizeText;
        [SerializeField] private TextMeshProUGUI    _costText;
        [SerializeField] private TextMeshProUGUI    _maintenanceText;
        [SerializeField] private TextMeshProUGUI    _populationText;
        [SerializeField] private TextMeshProUGUI    _descriptionText;

        [Header("마우스 오프셋")]
        [SerializeField] private Vector2 _offset = new Vector2(12f, -12f);

        // ── 내부 ─────────────────────────────────────────────
        private static BuildingTooltipUI _instance;
        private RectTransform _rect;
        private RectTransform _canvasRect;

        // ── Unity ────────────────────────────────────────────

        private void Awake()
        {
            _instance  = this;
            _rect      = GetComponent<RectTransform>();
            _canvasRect = GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
            _panel?.SetActive(false);
        }

        private void Update()
        {
            if (_panel != null && _panel.activeSelf)
                FollowMouse();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        // ── 정적 API (BuildingButtonUI에서 호출) ──────────────

        public static void Show(BuildingData data)
        {
            if (_instance == null || data == null) return;
            _instance.Populate(data);
            _instance._panel?.SetActive(true);
        }

        public static void Hide()
        {
            if (_instance == null) return;
            _instance._panel?.SetActive(false);
        }

        // ── 내부 ─────────────────────────────────────────────

        private void Populate(BuildingData data)
        {
            Set(_nameText,        data.BuildingName);
            Set(_categoryText,    CategoryLabel(data.Category));
            Set(_sizeText,        $"{data.SizeX}×{data.SizeZ} 타일");
            Set(_costText,        $"건설: ₩{data.BuildCost:N0}");
            Set(_maintenanceText, $"유지: ₩{data.MaintenanceCostPerMonth:N0}/월");
            Set(_descriptionText, data.Description);

            // 인구 / 고용
            string pop = "";
            if (data.ResidentCapacity > 0) pop += $"거주 {data.ResidentCapacity}명";
            if (data.JobCapacity > 0)
            {
                if (pop.Length > 0) pop += "  |  ";
                pop += $"고용 {data.JobCapacity}명";
            }
            Set(_populationText, pop);
        }

        private void FollowMouse()
        {
            if (_rect == null || _canvasRect == null) return;
            if (Mouse.current == null) return;

            Vector2 screen = Mouse.current.position.ReadValue();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect, screen, null, out Vector2 local);

            // 화면 밖 삐져나오지 않도록 피벗 자동 조정
            float halfW = _canvasRect.rect.width  * 0.5f;
            float halfH = _canvasRect.rect.height * 0.5f;
            float px    = (local.x + _offset.x + _rect.rect.width  > halfW)  ? 1f : 0f;
            float py    = (local.y + _offset.y - _rect.rect.height < -halfH) ? 0f : 1f;

            _rect.pivot            = new Vector2(px, py);
            _rect.anchoredPosition = local + _offset;
        }

        private static void Set(TextMeshProUGUI label, string text)
        {
            if (label == null) return;
            label.text = text;
            label.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        private static string CategoryLabel(BuildingCategory cat) => cat switch
        {
            BuildingCategory.Residential    => "🏠 주거",
            BuildingCategory.Commercial     => "🏪 상업",
            BuildingCategory.Industrial     => "🏭 공업",
            BuildingCategory.Public         => "🏥 공공시설",
            BuildingCategory.Utility        => "⚡ 유틸리티",
            BuildingCategory.Infrastructure => "🛣️ 인프라",
            _                               => ""
        };
    }
}

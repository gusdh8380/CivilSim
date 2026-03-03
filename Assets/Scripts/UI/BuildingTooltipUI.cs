using UnityEngine;
using TMPro;
using CivilSim.Buildings;

namespace CivilSim.UI
{
    /// <summary>
    /// 건물 버튼 호버 시 표시되는 툴팁 패널.
    /// 싱글턴 패턴으로 BuildingButtonUI에서 정적 메서드로 호출한다.
    ///
    /// 씬 구성 (왼쪽 고정 레이아웃):
    ///   Canvas
    ///   -- Tooltip (이 컴포넌트)
    ///       -- Panel (Image) ← _panel
    ///           ├-- Name         (TMP_Text) ← _nameText
    ///           ├-- Category     (TMP_Text) ← _categoryText
    ///           ├-- Size         (TMP_Text) ← _sizeText
    ///           ├-- Cost         (TMP_Text) ← _costText
    ///           ├-- Maintenance  (TMP_Text) ← _maintenanceText
    ///           ├-- Population   (TMP_Text) ← _populationText
    ///           -- Description  (TMP_Text) ← _descriptionText
    ///
    /// RectTransform 설정 (화면 왼쪽 중앙 고정):
    ///   - Anchor : Left-Middle  (anchorMin: 0,0.5 / anchorMax: 0,0.5)
    ///   - Pivot  : 0, 0.5
    ///   - Pos X  : 10  (화면 왼쪽 끝에서 10px)
    ///   - Pos Y  : 0   (수직 중앙)
    ///   - Width  : 220 / Height : Auto (Content Size Fitter 권장)
    /// </summary>
    public class BuildingTooltipUI : MonoBehaviour
    {
        // -- 인스펙터 --
        [Header("UI 요소")]
        [SerializeField] private GameObject         _panel;
        [SerializeField] private TextMeshProUGUI    _nameText;
        [SerializeField] private TextMeshProUGUI    _categoryText;
        [SerializeField] private TextMeshProUGUI    _sizeText;
        [SerializeField] private TextMeshProUGUI    _costText;
        [SerializeField] private TextMeshProUGUI    _maintenanceText;
        [SerializeField] private TextMeshProUGUI    _populationText;
        [SerializeField] private TextMeshProUGUI    _descriptionText;

        // -- 내부 --
        private static BuildingTooltipUI _instance;

        // -- Unity --

        private void Awake()
        {
            _instance = this;
            _panel?.SetActive(false); // 시작 시 숨김
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        // -- 정적 API (BuildingButtonUI에서 호출) --

        /// <summary>건물 버튼 호버 시 호출 — 왼쪽 고정 위치에 정보 표시</summary>
        public static void Show(BuildingData data)
        {
            if (_instance == null || data == null) return;
            _instance.Populate(data);
            _instance._panel?.SetActive(true);
        }

        /// <summary>호버 해제 시 호출 — 패널 숨김</summary>
        public static void Hide()
        {
            if (_instance == null) return;
            _instance._panel?.SetActive(false);
        }

        // -- 내부 --

        private void Populate(BuildingData data)
        {
            Set(_nameText,        data.BuildingName);
            Set(_categoryText,    CategoryLabel(data.Category));
            Set(_sizeText,        $"{data.SizeX}x{data.SizeZ} 타일");
            Set(_costText,        $"건설: ₩{data.BuildCost:N0}");
            Set(_maintenanceText, $"유지: ₩{data.MaintenanceCostPerMonth:N0}/월");
            Set(_descriptionText, BuildDescriptionText(data));

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

        private static string BuildDescriptionText(BuildingData data)
        {
            if (data == null) return string.Empty;

            string utilityText = BuildUtilityText(data);
            string serviceText = BuildServiceText(data);

            if (string.IsNullOrEmpty(utilityText) && string.IsNullOrEmpty(serviceText))
                return data.Description;

            if (string.IsNullOrEmpty(data.Description))
                return $"{utilityText}\n{serviceText}".Trim();

            string extra = $"{utilityText}\n{serviceText}".Trim();
            return string.IsNullOrEmpty(extra) ? data.Description : $"{data.Description}\n{extra}";
        }

        private static string BuildUtilityText(BuildingData data)
        {
            if (data == null) return string.Empty;

            string supply = "";
            if (data.PowerSupply > 0) supply += $"전력공급 {data.PowerSupply}";
            if (data.WaterSupply > 0)
            {
                if (!string.IsNullOrEmpty(supply)) supply += " | ";
                supply += $"수도공급 {data.WaterSupply}";
            }

            string demand = "";
            if (data.RequiresPower && data.PowerConsumption > 0) demand += $"전력소비 {data.PowerConsumption}";
            if (data.RequiresWater && data.WaterConsumption > 0)
            {
                if (!string.IsNullOrEmpty(demand)) demand += " | ";
                demand += $"수도소비 {data.WaterConsumption}";
            }

            if (string.IsNullOrEmpty(supply) && string.IsNullOrEmpty(demand))
                return string.Empty;
            if (string.IsNullOrEmpty(supply)) return demand;
            if (string.IsNullOrEmpty(demand)) return supply;
            return $"{supply} / {demand}";
        }

        private static string BuildServiceText(BuildingData data)
        {
            if (data == null || data.ServiceValue <= 0) return string.Empty;
            return $"서비스 {data.ServiceKind} +{data.ServiceValue}";
        }

        private static void Set(TextMeshProUGUI label, string text)
        {
            if (label == null) return;
            label.text = text;
            label.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        private static string CategoryLabel(BuildingCategory cat) => cat switch
        {
            BuildingCategory.Residential    => "RES 주거",
            BuildingCategory.Commercial     => "COM 상업",
            BuildingCategory.Industrial     => "IND 공업",
            BuildingCategory.Public         => "PUB 공공시설",
            BuildingCategory.Utility        => "UTL 유틸리티",
            BuildingCategory.Infrastructure => "INFRA 인프라",
            _                               => ""
        };
    }
}

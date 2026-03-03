using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CivilSim.Buildings;
using CivilSim.Core;
using CivilSim.Economy;
using CivilSim.Grid;
using CivilSim.Infrastructure;
using CivilSim.Zones;

namespace CivilSim.UI
{
    /// <summary>
    /// 화면 상단 HUD — 자금 / 인구 / 날짜 / 시간 배속 / 현재 모드 표시.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [Header("자금")]
        [SerializeField] private TextMeshProUGUI _moneyText;
        [SerializeField] private Color _moneyPositiveColor = new Color(0.2f, 0.9f, 0.3f);
        [SerializeField] private Color _moneyNegativeColor = new Color(1.0f, 0.3f, 0.3f);

        [Header("인구")]
        [SerializeField] private TextMeshProUGUI _populationText;

        [Header("날짜")]
        [SerializeField] private TextMeshProUGUI _dateText;

        [Header("모드 표시 (선택)")]
        [SerializeField] private TextMeshProUGUI _modeText;
        [SerializeField] private TextMeshProUGUI _zoneTypeText;

        [Header("수요 표시 (선택)")]
        [SerializeField] private TextMeshProUGUI _resDemandText;
        [SerializeField] private TextMeshProUGUI _comDemandText;
        [SerializeField] private TextMeshProUGUI _indDemandText;

        [Header("시간 배속 버튼")]
        [SerializeField] private Button _pauseButton;
        [SerializeField] private Button _speed1Button;
        [SerializeField] private Button _speed2Button;
        [SerializeField] private Button _speed4Button;

        [Header("배속 버튼 색상")]
        [SerializeField] private Color _activeSpeedColor = new Color(0.3f, 0.8f, 0.4f);
        [SerializeField] private Color _inactiveSpeedColor = new Color(0.25f, 0.25f, 0.25f);

        private int _population;
        private int _currentDay = 1;
        private int _currentMonth = 1;
        private int _currentYear = 1;

        private void Start()
        {
            AutoBindOptionalTexts();

            _pauseButton?.onClick.AddListener(() => SetSpeed(TimeSpeed.Paused));
            _speed1Button?.onClick.AddListener(() => SetSpeed(TimeSpeed.Normal));
            _speed2Button?.onClick.AddListener(() => SetSpeed(TimeSpeed.Fast));
            _speed4Button?.onClick.AddListener(() => SetSpeed(TimeSpeed.VeryFast));

            GameEventBus.Subscribe<MoneyChangedEvent>(OnMoneyChanged);
            GameEventBus.Subscribe<BuildingPlacedEvent>(OnBuildingPlaced);
            GameEventBus.Subscribe<BuildingRemovedEvent>(OnBuildingRemoved);
            GameEventBus.Subscribe<DailyTickEvent>(OnDailyTick);
            GameEventBus.Subscribe<TimeSpeedChangedEvent>(OnTimeSpeedChanged);
            GameEventBus.Subscribe<PopulationChangedEvent>(OnPopulationChanged);
            GameEventBus.Subscribe<DemandChangedEvent>(OnDemandChanged);

            RefreshAll();
        }

        private void Update()
        {
            UpdateModeUI();
        }

        private void OnDestroy()
        {
            GameEventBus.Unsubscribe<MoneyChangedEvent>(OnMoneyChanged);
            GameEventBus.Unsubscribe<BuildingPlacedEvent>(OnBuildingPlaced);
            GameEventBus.Unsubscribe<BuildingRemovedEvent>(OnBuildingRemoved);
            GameEventBus.Unsubscribe<DailyTickEvent>(OnDailyTick);
            GameEventBus.Unsubscribe<TimeSpeedChangedEvent>(OnTimeSpeedChanged);
            GameEventBus.Unsubscribe<PopulationChangedEvent>(OnPopulationChanged);
            GameEventBus.Unsubscribe<DemandChangedEvent>(OnDemandChanged);
        }

        private void OnMoneyChanged(MoneyChangedEvent e) => UpdateMoneyUI(e.NewAmount);

        private void OnDailyTick(DailyTickEvent e)
        {
            _currentDay = e.Day;
            _currentMonth = e.Month;
            _currentYear = e.Year;
            UpdateDateUI();
        }

        private void OnBuildingPlaced(BuildingPlacedEvent e)
        {
            var inst = GameManager.Instance.Buildings.GetBuilding(e.BuildingDataId);
            if (inst?.Data != null)
                _population += inst.Data.ResidentCapacity;
            UpdatePopulationUI();
        }

        private void OnBuildingRemoved(BuildingRemovedEvent e)
        {
            RecalculatePopulation();
            UpdatePopulationUI();
        }

        private void OnTimeSpeedChanged(TimeSpeedChangedEvent e)
            => UpdateSpeedButtonColors(e.Speed);

        private void OnPopulationChanged(PopulationChangedEvent e)
        {
            _population = e.NewPopulation;
            UpdatePopulationUI();
        }

        private void OnDemandChanged(DemandChangedEvent e)
            => UpdateDemandUI(e.ResidentialDemand, e.CommercialDemand, e.IndustrialDemand);

        private void UpdateMoneyUI(int amount)
        {
            if (_moneyText == null) return;
            _moneyText.text = $"{amount:N0}";
            _moneyText.color = amount >= 0 ? _moneyPositiveColor : _moneyNegativeColor;
        }

        private void UpdatePopulationUI()
        {
            if (_populationText == null) return;
            _populationText.text = $"{_population:N0}명";
        }

        private void UpdateDateUI()
        {
            if (_dateText == null) return;
            _dateText.text = $"{_currentYear}년 {_currentMonth}월 {_currentDay}일";
        }

        private void UpdateSpeedButtonColors(TimeSpeed speed)
        {
            SetButtonColor(_pauseButton, speed == TimeSpeed.Paused);
            SetButtonColor(_speed1Button, speed == TimeSpeed.Normal);
            SetButtonColor(_speed2Button, speed == TimeSpeed.Fast);
            SetButtonColor(_speed4Button, speed == TimeSpeed.VeryFast);
        }

        private void SetButtonColor(Button btn, bool isActive)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null) img.color = isActive ? _activeSpeedColor : _inactiveSpeedColor;
        }

        private void SetSpeed(TimeSpeed speed)
        {
            GameManager.Instance.SetTimeSpeed(speed);
            UpdateSpeedButtonColors(speed);
        }

        private void RefreshAll()
        {
            var economy = GameManager.Instance.Economy;
            if (economy != null) UpdateMoneyUI(economy.Money);
            else UpdateMoneyUI(0);

            RecalculatePopulation();
            UpdatePopulationUI();
            UpdateDateUI();
            UpdateSpeedButtonColors(TimeSpeed.Normal);
            UpdateModeUI();
            UpdateDemandUI(0, 0, 0);
        }

        private void RecalculatePopulation()
        {
            _population = 0;
            var all = GameManager.Instance.Buildings?.GetAll();
            if (all == null) return;
            foreach (var kv in all)
                if (kv.Value?.Data != null)
                    _population += kv.Value.Data.ResidentCapacity;
        }

        private void UpdateModeUI()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            string modeLabel = "기본";
            string zoneLabel = "-";

            BuildingPlacer placer = gm.Placer;
            if (placer != null && placer.Mode != PlacerMode.None)
            {
                modeLabel = placer.Mode == PlacerMode.Placing ? "건물 배치" : "건물 철거";
            }
            else
            {
                RoadBuilder roadBuilder = gm.RoadBuild;
                if (roadBuilder != null && roadBuilder.Mode != RoadBuilderMode.None)
                {
                    modeLabel = roadBuilder.Mode == RoadBuilderMode.Building ? "도로 배치" : "도로 철거";
                }
                else
                {
                    FoundationBuilder foundationBuilder = gm.FoundationBuild;
                    if (foundationBuilder != null && foundationBuilder.IsActive)
                    {
                        modeLabel = "지반 정비";
                    }
                    else
                    {
                        ZoneBuilder zoneBuilder = gm.ZoneBuild;
                        if (zoneBuilder != null && zoneBuilder.IsActive)
                        {
                            modeLabel = "구역 지정";
                            zoneLabel = ZoneTypeToLabel(zoneBuilder.CurrentZoneType);
                        }
                    }
                }
            }

            if (_modeText != null) _modeText.text = $"모드: {modeLabel}";
            if (_zoneTypeText != null) _zoneTypeText.text = $"구역: {zoneLabel}";
        }

        private static string ZoneTypeToLabel(ZoneType zoneType)
        {
            return zoneType switch
            {
                ZoneType.Residential => "주거 (R)",
                ZoneType.Commercial => "상업 (C)",
                ZoneType.Industrial => "공업 (I)",
                _ => "해제 (X)"
            };
        }

        private void AutoBindOptionalTexts()
        {
            if (_modeText == null)
                _modeText = FindTextByName("ModeText");

            if (_zoneTypeText == null)
                _zoneTypeText = FindTextByName("ZoneTypeText");

            if (_resDemandText == null)
                _resDemandText = FindTextByName("ResDemandText");
            if (_comDemandText == null)
                _comDemandText = FindTextByName("ComDemandText");
            if (_indDemandText == null)
                _indDemandText = FindTextByName("IndDemandText");
        }

        private void UpdateDemandUI(int res, int com, int ind)
        {
            if (_resDemandText != null) _resDemandText.text = $"R 수요: {FormatSigned(res)}";
            if (_comDemandText != null) _comDemandText.text = $"C 수요: {FormatSigned(com)}";
            if (_indDemandText != null) _indDemandText.text = $"I 수요: {FormatSigned(ind)}";
        }

        private static string FormatSigned(int value)
            => value >= 0 ? $"+{value}" : value.ToString();

        private TextMeshProUGUI FindTextByName(string objectName)
        {
            foreach (var t in GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (t != null && t.gameObject.name == objectName)
                    return t;
            }
            return null;
        }
    }
}

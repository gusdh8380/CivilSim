using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CivilSim.Buildings;
using CivilSim.Core;
using CivilSim.Economy;
using CivilSim.Grid;
using CivilSim.Infrastructure;
using CivilSim.Population;
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

        [Header("월말 리포트/목표 (선택)")]
        [SerializeField] private TextMeshProUGUI _budgetReportText;
        [SerializeField] private TextMeshProUGUI _goalText;
        [SerializeField] private TextMeshProUGUI _resultText;
        [SerializeField] private TextMeshProUGUI _notificationText;

        [Header("알림 색상")]
        [SerializeField] private Color _infoColor = new Color(0.8f, 0.9f, 1f);
        [SerializeField] private Color _warningColor = new Color(1.0f, 0.85f, 0.3f);
        [SerializeField] private Color _alertColor = new Color(1.0f, 0.45f, 0.45f);

        [Header("행복도 (선택)")]
        [SerializeField] private TextMeshProUGUI _happinessText;

        [Header("알림 설정")]
        [Tooltip("각 알림 메시지를 화면에 유지하는 시간(초)")]
        [SerializeField] private float _notificationDisplayDuration = 3f;

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
        private int _targetPopulation;
        private int _targetBalance;
        private int _currentBalance;
        private bool _useBalanceGoal;

        // 알림 큐 — 여러 알림이 동시에 들어와도 순서대로 표시
        private readonly Queue<(string message, NotificationType type)> _notificationQueue = new();
        private bool _isShowingNotification;

        // 모드 UI 캐싱 — 변경 시에만 UI 갱신 (매 프레임 string 생성 방지)
        private string _lastModeLabel = "";
        private string _lastZoneLabel = "";

        private void Start()
        {
            AutoBindOptionalTexts();

            _pauseButton?.onClick.AddListener(() => SetSpeed(TimeSpeed.Paused));
            _speed1Button?.onClick.AddListener(() => SetSpeed(TimeSpeed.Normal));
            _speed2Button?.onClick.AddListener(() => SetSpeed(TimeSpeed.Fast));
            _speed4Button?.onClick.AddListener(() => SetSpeed(TimeSpeed.VeryFast));

            GameEventBus.Subscribe<MoneyChangedEvent>(OnMoneyChanged);
            GameEventBus.Subscribe<DailyTickEvent>(OnDailyTick);
            GameEventBus.Subscribe<TimeSpeedChangedEvent>(OnTimeSpeedChanged);
            GameEventBus.Subscribe<PopulationChangedEvent>(OnPopulationChanged);
            GameEventBus.Subscribe<DemandChangedEvent>(OnDemandChanged);
            GameEventBus.Subscribe<BudgetReportEvent>(OnBudgetReport);
            GameEventBus.Subscribe<GoalProgressEvent>(OnGoalProgress);
            GameEventBus.Subscribe<GameWonEvent>(OnGameWon);
            GameEventBus.Subscribe<GameLostEvent>(OnGameLost);
            GameEventBus.Subscribe<NotificationEvent>(OnNotification);
            GameEventBus.Subscribe<HappinessChangedEvent>(OnHappinessChanged);
            GameEventBus.Subscribe<GameStartedEvent>(OnGameStarted);

            RefreshAll();
        }

        private void Update()
        {
            UpdateModeUI();
        }

        private void OnDestroy()
        {
            GameEventBus.Unsubscribe<MoneyChangedEvent>(OnMoneyChanged);
            GameEventBus.Unsubscribe<DailyTickEvent>(OnDailyTick);
            GameEventBus.Unsubscribe<TimeSpeedChangedEvent>(OnTimeSpeedChanged);
            GameEventBus.Unsubscribe<PopulationChangedEvent>(OnPopulationChanged);
            GameEventBus.Unsubscribe<DemandChangedEvent>(OnDemandChanged);
            GameEventBus.Unsubscribe<BudgetReportEvent>(OnBudgetReport);
            GameEventBus.Unsubscribe<GoalProgressEvent>(OnGoalProgress);
            GameEventBus.Unsubscribe<GameWonEvent>(OnGameWon);
            GameEventBus.Unsubscribe<GameLostEvent>(OnGameLost);
            GameEventBus.Unsubscribe<NotificationEvent>(OnNotification);
            GameEventBus.Unsubscribe<HappinessChangedEvent>(OnHappinessChanged);
            GameEventBus.Unsubscribe<GameStartedEvent>(OnGameStarted);
        }

        private void OnMoneyChanged(MoneyChangedEvent e) => UpdateMoneyUI(e.NewAmount);

        private void OnDailyTick(DailyTickEvent e)
        {
            _currentDay = e.Day;
            _currentMonth = e.Month;
            _currentYear = e.Year;
            UpdateDateUI();
        }

        private void OnHappinessChanged(HappinessChangedEvent e)
        {
            if (_happinessText == null) return;
            _happinessText.text = $"행복도: {Mathf.RoundToInt(e.NewHappiness)}";
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

        private void OnBudgetReport(BudgetReportEvent e)
        {
            _currentBalance = e.Balance;
            UpdateBudgetReportUI(e);
            UpdateGoalUI();
        }

        private void OnGoalProgress(GoalProgressEvent e)
        {
            _targetPopulation = e.TargetPopulation;
            _targetBalance = e.TargetBalance;
            _population = e.CurrentPopulation;
            _currentBalance = e.CurrentBalance;
            _useBalanceGoal = e.UseBalanceGoal;
            UpdateGoalUI();
        }

        private void OnGameWon(GameWonEvent e)
            => UpdateResultUI($"승리 - {e.Year}년 {e.Month}월 - {e.Reason}", _moneyPositiveColor);

        private void OnGameLost(GameLostEvent e)
            => UpdateResultUI($"패배 - {e.Year}년 {e.Month}월 - {e.Reason}", _moneyNegativeColor);

        private void OnGameStarted(GameStartedEvent e)
        {
            _notificationQueue.Enqueue(("도시 건설을 시작합니다!", NotificationType.Info));
            if (!_isShowingNotification)
                StartCoroutine(ProcessNotificationQueue());
        }

        private void OnNotification(NotificationEvent e)
        {
            _notificationQueue.Enqueue((e.Message, e.Type));
            if (!_isShowingNotification)
                StartCoroutine(ProcessNotificationQueue());
        }

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
            _currentBalance = economy != null ? economy.Money : 0;
            UpdateMoneyUI(_currentBalance);

            RecalculatePopulation();

            var progression = GameManager.Instance.Progression;
            if (progression != null)
            {
                _targetPopulation = progression.TargetPopulation;
                _targetBalance = progression.TargetBalance;
                _useBalanceGoal = progression.UseBalanceGoal;
                _population = progression.CurrentPopulation;
                _currentBalance = progression.CurrentBalance;
            }

            UpdatePopulationUI();
            UpdateDateUI();
            UpdateSpeedButtonColors(TimeSpeed.Normal);
            UpdateModeUI();
            UpdateDemandUI(0, 0, 0);
            UpdateGoalUI();
        }

        private void RecalculatePopulation()
        {
            // CityDemandSystem이 OperationRate를 반영한 인구를 갖고 있으면 그것을 우선 사용
            var demand = GameManager.Instance?.Demand;
            if (demand != null)
            {
                _population = demand.Residents;
                return;
            }

            // fallback: OperationRate 미반영 원시 합산
            _population = 0;
            var all = GameManager.Instance?.Buildings?.GetAll();
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

            // 이전 값과 동일하면 UI 갱신 생략 — 매 프레임 string 할당 및 UI 재계산 방지
            if (modeLabel == _lastModeLabel && zoneLabel == _lastZoneLabel) return;
            _lastModeLabel = modeLabel;
            _lastZoneLabel = zoneLabel;

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

            if (_budgetReportText == null)
                _budgetReportText = FindTextByName("BudgetReportText") ?? FindTextByName("MonthlyReportText");

            if (_goalText == null)
                _goalText = FindTextByName("GoalText") ?? FindTextByName("ObjectiveText") ?? FindTextByName("GoalProgressText");

            if (_resultText == null)
                _resultText = FindTextByName("ResultText") ?? FindTextByName("OutcomeText");

            if (_notificationText == null)
                _notificationText = FindTextByName("NotificationText");
        }

        private void UpdateDemandUI(int res, int com, int ind)
        {
            if (_resDemandText != null) _resDemandText.text = $"R 수요: {FormatSigned(res)}";
            if (_comDemandText != null) _comDemandText.text = $"C 수요: {FormatSigned(com)}";
            if (_indDemandText != null) _indDemandText.text = $"I 수요: {FormatSigned(ind)}";
        }

        private static string FormatSigned(int value)
            => value >= 0 ? $"+{value}" : value.ToString();

        private void UpdateBudgetReportUI(BudgetReportEvent e)
        {
            if (_budgetReportText == null) return;
            _budgetReportText.text = BudgetReportTextFormatter.BuildBudgetLine(e);
        }

        private void UpdateGoalUI()
        {
            if (_goalText == null) return;
            if (_targetPopulation <= 0)
            {
                _goalText.text = "목표: 진행 데이터 없음";
                return;
            }

            string populationPart = $"인구 {_population:N0}/{_targetPopulation:N0}";
            if (_useBalanceGoal)
                _goalText.text = $"목표: {populationPart} | 자금 {_currentBalance:N0}/{_targetBalance:N0}";
            else
                _goalText.text = $"목표: {populationPart}";
        }

        private void UpdateResultUI(string text, Color color)
        {
            if (_resultText == null) return;
            _resultText.text = text;
            _resultText.color = color;
        }

        /// <summary>
        /// 알림 큐를 순서대로 표시하고 각 메시지를 <see cref="_notificationDisplayDuration"/>초 동안 유지한다.
        /// 이전 메시지가 표시되는 동안 새 알림이 들어오면 큐에 쌓였다가 순차적으로 출력된다.
        /// </summary>
        private System.Collections.IEnumerator ProcessNotificationQueue()
        {
            _isShowingNotification = true;
            while (_notificationQueue.Count > 0)
            {
                var (msg, type) = _notificationQueue.Dequeue();
                if (_notificationText != null)
                {
                    _notificationText.text = msg;
                    _notificationText.color = type switch
                    {
                        NotificationType.Info    => _infoColor,
                        NotificationType.Warning => _warningColor,
                        _                        => _alertColor
                    };
                }
                yield return new WaitForSecondsRealtime(_notificationDisplayDuration);
            }
            if (_notificationText != null) _notificationText.text = "";
            _isShowingNotification = false;
        }

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

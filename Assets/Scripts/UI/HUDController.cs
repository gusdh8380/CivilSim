using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CivilSim.Core;
using CivilSim.Economy;

namespace CivilSim.UI
{
    /// <summary>
    /// 화면 상단 HUD — 자금 / 인구 / 날짜 / 시간 배속 표시.
    ///
    /// 씬 구성:
    ///   Canvas
    ///   └── HUD (이 컴포넌트)
    ///       ├── MoneyText     (TMP) ← _moneyText
    ///       ├── PopulationText(TMP) ← _populationText
    ///       ├── DateText      (TMP) ← _dateText
    ///       └── TimeControls
    ///           ├── PauseButton   ← _pauseButton
    ///           ├── Speed1Button  ← _speed1Button
    ///           ├── Speed2Button  ← _speed2Button
    ///           └── Speed4Button  ← _speed4Button
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        // ── 인스펙터 ──────────────────────────────────────────
        [Header("자금")]
        [SerializeField] private TextMeshProUGUI _moneyText;
        [SerializeField] private Color _moneyPositiveColor = new Color(0.2f, 0.9f, 0.3f);
        [SerializeField] private Color _moneyNegativeColor = new Color(1.0f, 0.3f, 0.3f);

        [Header("인구")]
        [SerializeField] private TextMeshProUGUI _populationText;

        [Header("날짜")]
        [SerializeField] private TextMeshProUGUI _dateText;

        [Header("시간 배속 버튼")]
        [SerializeField] private Button _pauseButton;
        [SerializeField] private Button _speed1Button;
        [SerializeField] private Button _speed2Button;
        [SerializeField] private Button _speed4Button;

        [Header("배속 버튼 색상")]
        [SerializeField] private Color _activeSpeedColor   = new Color(0.3f, 0.8f, 0.4f);
        [SerializeField] private Color _inactiveSpeedColor = new Color(0.25f, 0.25f, 0.25f);

        // ── 내부 상태 ─────────────────────────────────────────
        private int _population;
        private int _currentDay = 1, _currentMonth = 1, _currentYear = 1;

        // ── Unity ────────────────────────────────────────────

        private void Start()
        {
            // 버튼 이벤트 연결
            _pauseButton? .onClick.AddListener(() => SetSpeed(TimeSpeed.Paused));
            _speed1Button?.onClick.AddListener(() => SetSpeed(TimeSpeed.Normal));
            _speed2Button?.onClick.AddListener(() => SetSpeed(TimeSpeed.Fast));
            _speed4Button?.onClick.AddListener(() => SetSpeed(TimeSpeed.VeryFast));

            // 이벤트 구독
            GameEventBus.Subscribe<MoneyChangedEvent>    (OnMoneyChanged);
            GameEventBus.Subscribe<BuildingPlacedEvent>  (OnBuildingPlaced);
            GameEventBus.Subscribe<BuildingRemovedEvent> (OnBuildingRemoved);
            GameEventBus.Subscribe<DailyTickEvent>       (OnDailyTick);
            GameEventBus.Subscribe<TimeSpeedChangedEvent>(OnTimeSpeedChanged);

            // 초기값 반영
            RefreshAll();
        }

        private void OnDestroy()
        {
            GameEventBus.Unsubscribe<MoneyChangedEvent>    (OnMoneyChanged);
            GameEventBus.Unsubscribe<BuildingPlacedEvent>  (OnBuildingPlaced);
            GameEventBus.Unsubscribe<BuildingRemovedEvent> (OnBuildingRemoved);
            GameEventBus.Unsubscribe<DailyTickEvent>       (OnDailyTick);
            GameEventBus.Unsubscribe<TimeSpeedChangedEvent>(OnTimeSpeedChanged);
        }

        // ── 이벤트 핸들러 ─────────────────────────────────────

        private void OnMoneyChanged(MoneyChangedEvent e)    => UpdateMoneyUI(e.NewAmount);
        private void OnDailyTick(DailyTickEvent e)
        {
            _currentDay   = e.Day;
            _currentMonth = e.Month;
            _currentYear  = e.Year;
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
            // 이미 파괴됐을 수 있으므로 전체 재계산
            RecalculatePopulation();
            UpdatePopulationUI();
        }

        private void OnTimeSpeedChanged(TimeSpeedChangedEvent e)
            => UpdateSpeedButtonColors(e.Speed);

        // ── UI 갱신 ───────────────────────────────────────────

        private void UpdateMoneyUI(int amount)
        {
            if (_moneyText == null) return;
            _moneyText.text  = $"{amount:N0}";
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
            SetButtonColor(_pauseButton,  speed == TimeSpeed.Paused);
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

        // ── 버튼 액션 ─────────────────────────────────────────

        private void SetSpeed(TimeSpeed speed)
        {
            GameManager.Instance.SetTimeSpeed(speed);
            UpdateSpeedButtonColors(speed);
        }

        // ── 초기화 ────────────────────────────────────────────

        private void RefreshAll()
        {
            // 자금
            var economy = GameManager.Instance.Economy;
            if (economy != null) UpdateMoneyUI(economy.Money);
            else UpdateMoneyUI(0);

            // 인구
            RecalculatePopulation();
            UpdatePopulationUI();

            // 날짜
            UpdateDateUI();

            // 배속 초기 상태
            UpdateSpeedButtonColors(TimeSpeed.Normal);
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
    }
}

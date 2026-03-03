using UnityEngine;
using CivilSim.Economy;
using CivilSim.Population;

namespace CivilSim.Core
{
    /// <summary>
    /// 플레이 루프(승리/패배) 판정을 담당한다.
    /// </summary>
    public class CityProgressionManager : MonoBehaviour
    {
        [Header("승리 조건")]
        [SerializeField, Min(1)] private int _targetPopulation = 300;
        [SerializeField] private bool _useBalanceGoal = true;
        [SerializeField, Min(0)] private int _targetBalance = 600000;

        [Header("패배 조건")]
        [SerializeField, Min(1)] private int _maxDeficitStreakMonths = 6;
        [SerializeField, Min(1)] private int _maxNegativeBalanceStreakMonths = 3;

        [Header("동작 옵션")]
        [SerializeField] private bool _pauseOnOutcome = true;
        [SerializeField] private bool _notifyMonthlyGoalProgress = true;

        private EconomyManager _economy;
        private CityDemandSystem _demand;

        private int _currentPopulation;
        private int _currentBalance;
        private int _currentMonth;
        private int _currentYear;
        private int _deficitStreakMonths;
        private int _negativeBalanceStreakMonths;
        private bool _isEnded;

        public int TargetPopulation => _targetPopulation;
        public int TargetBalance => _targetBalance;
        public bool UseBalanceGoal => _useBalanceGoal;
        public int CurrentPopulation => _currentPopulation;
        public int CurrentBalance => _currentBalance;
        public bool IsEnded => _isEnded;

        private void Start()
        {
            ResolveReferences();
            InitializeState();
            PublishGoalProgress();
        }

        private void OnEnable()
        {
            GameEventBus.Subscribe<PopulationChangedEvent>(OnPopulationChanged);
            GameEventBus.Subscribe<MoneyChangedEvent>(OnMoneyChanged);
            GameEventBus.Subscribe<BudgetReportEvent>(OnBudgetReport);
            GameEventBus.Subscribe<MonthlyTickEvent>(OnMonthlyTick);
        }

        private void OnDisable()
        {
            GameEventBus.Unsubscribe<PopulationChangedEvent>(OnPopulationChanged);
            GameEventBus.Unsubscribe<MoneyChangedEvent>(OnMoneyChanged);
            GameEventBus.Unsubscribe<BudgetReportEvent>(OnBudgetReport);
            GameEventBus.Unsubscribe<MonthlyTickEvent>(OnMonthlyTick);
        }

        private void ResolveReferences()
        {
            _economy = GameManager.Instance?.Economy;
            _demand = GameManager.Instance?.Demand;
        }

        private void InitializeState()
        {
            if (_economy != null) _currentBalance = _economy.Money;
            if (_demand != null) _currentPopulation = _demand.Residents;

            var clock = GameManager.Instance?.Clock;
            _currentMonth = clock != null ? clock.Month : 1;
            _currentYear = clock != null ? clock.Year : 1;
        }

        private void OnPopulationChanged(PopulationChangedEvent e)
        {
            _currentPopulation = e.NewPopulation;
            PublishGoalProgress();
            TryResolveVictory();
        }

        private void OnMoneyChanged(MoneyChangedEvent e)
        {
            _currentBalance = e.NewAmount;
            PublishGoalProgress();

            if (_isEnded) return;
            if (_economy == null) _economy = GameManager.Instance?.Economy;
            if (_economy != null && _economy.IsBankrupt)
                TriggerLoss("도시가 파산했습니다.");
        }

        private void OnMonthlyTick(MonthlyTickEvent e)
        {
            _currentMonth = e.Month;
            _currentYear = e.Year;

            if (_notifyMonthlyGoalProgress && !_isEnded)
            {
                string balancePart = _useBalanceGoal
                    ? $" | 자금 {_currentBalance:N0}/{_targetBalance:N0}"
                    : string.Empty;

                GameEventBus.Publish(new NotificationEvent
                {
                    Message = $"목표 진행: 인구 {_currentPopulation:N0}/{_targetPopulation:N0}{balancePart}",
                    Type = NotificationType.Info
                });
            }

            PublishGoalProgress();
            TryResolveVictory();
        }

        private void OnBudgetReport(BudgetReportEvent e)
        {
            _currentMonth = e.Month;
            _currentYear = e.Year;
            _currentBalance = e.Balance;

            int net = e.Income - e.Expenditure;
            _deficitStreakMonths = net < 0 ? _deficitStreakMonths + 1 : 0;
            _negativeBalanceStreakMonths = e.Balance < 0 ? _negativeBalanceStreakMonths + 1 : 0;

            if (_isEnded) return;

            if (_deficitStreakMonths >= Mathf.Max(1, _maxDeficitStreakMonths))
            {
                TriggerLoss($"월간 순손실이 {_deficitStreakMonths}개월 연속 발생했습니다.");
                return;
            }

            if (_negativeBalanceStreakMonths >= Mathf.Max(1, _maxNegativeBalanceStreakMonths))
            {
                TriggerLoss($"자금이 {_negativeBalanceStreakMonths}개월 연속 음수입니다.");
                return;
            }

            TryResolveVictory();
        }

        private void TryResolveVictory()
        {
            if (_isEnded) return;

            bool populationOk = _currentPopulation >= _targetPopulation;
            bool balanceOk = !_useBalanceGoal || _currentBalance >= _targetBalance;
            if (!populationOk || !balanceOk) return;

            _isEnded = true;
            string reason = _useBalanceGoal
                ? "인구와 자금 목표를 달성했습니다."
                : "인구 목표를 달성했습니다.";

            GameEventBus.Publish(new GameWonEvent
            {
                Reason = reason,
                Month = _currentMonth,
                Year = _currentYear
            });

            GameEventBus.Publish(new NotificationEvent
            {
                Message = $"승리: {reason}",
                Type = NotificationType.Alert
            });

            if (_pauseOnOutcome)
                GameManager.Instance?.SetTimeSpeed(TimeSpeed.Paused);
        }

        private void TriggerLoss(string reason)
        {
            if (_isEnded) return;
            _isEnded = true;

            GameEventBus.Publish(new GameLostEvent
            {
                Reason = reason,
                Month = _currentMonth,
                Year = _currentYear
            });

            GameEventBus.Publish(new NotificationEvent
            {
                Message = $"패배: {reason}",
                Type = NotificationType.Alert
            });

            if (_pauseOnOutcome)
                GameManager.Instance?.SetTimeSpeed(TimeSpeed.Paused);
        }

        private void PublishGoalProgress()
        {
            GameEventBus.Publish(new GoalProgressEvent
            {
                TargetPopulation = _targetPopulation,
                CurrentPopulation = _currentPopulation,
                TargetBalance = _targetBalance,
                CurrentBalance = _currentBalance,
                UseBalanceGoal = _useBalanceGoal
            });
        }
    }
}

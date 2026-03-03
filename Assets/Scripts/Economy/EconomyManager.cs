using UnityEngine;
using CivilSim.Core;
using CivilSim.Buildings;

namespace CivilSim.Economy
{
    /// <summary>
    /// 도시 자금을 관리한다.
    ///
    /// 수입/지출 구조:
    ///   - 수입: 매월 (거주자 x 세율) + (고용자 x 세율)
    ///   - 지출: 매월 모든 건물의 MaintenanceCostPerMonth 합산
    ///
    /// GameManager.Instance.Economy 로 접근.
    /// </summary>
    public class EconomyManager : MonoBehaviour
    {
        // -- 인스펙터 --
        [Header("설정")]
        [SerializeField] private EconomyConfig _config;

        // -- 공개 상태 --
        public int  Money        { get; private set; }
        public bool IsBankrupt   { get; private set; }
        public int ResidentTaxPerMonth => _residentTaxPerMonth;
        public int JobTaxPerMonth => _jobTaxPerMonth;

        // -- 내부 참조 --
        private BuildingManager _buildings;
        private int _resDemand;
        private int _comDemand;
        private int _indDemand;
        private int _deficitStreakMonths;
        private int _residentTaxPerMonth;
        private int _jobTaxPerMonth;

        // -- Unity --

        private void Awake()
        {
            if (_config != null)
            {
                _residentTaxPerMonth = Mathf.Max(0, _config.TaxPerResidentPerMonth);
                _jobTaxPerMonth = Mathf.Max(0, _config.TaxPerJobPerMonth);
            }
        }

        private void Start()
        {
            _buildings = GameManager.Instance.Buildings;

            if (_config == null)
            {
                Debug.LogError("[EconomyManager] EconomyConfig이 할당되지 않았습니다!");
                return;
            }

            _residentTaxPerMonth = Mathf.Max(0, _config.TaxPerResidentPerMonth);
            _jobTaxPerMonth = Mathf.Max(0, _config.TaxPerJobPerMonth);

            Money = _config.InitialBudget;
            PublishMoneyChanged(0);

            GameEventBus.Subscribe<MonthlyTickEvent>(OnMonthlyTick);
            GameEventBus.Subscribe<DemandChangedEvent>(OnDemandChanged);
        }

        private void OnDestroy()
        {
            GameEventBus.Unsubscribe<MonthlyTickEvent>(OnMonthlyTick);
            GameEventBus.Unsubscribe<DemandChangedEvent>(OnDemandChanged);
        }

        // -- 공개 API --

        /// <summary>
        /// 자금을 차감한다. 잔액이 부족하면 false 반환.
        /// </summary>
        public bool TrySpend(int amount)
        {
            if (amount <= 0) return true;
            if (Money < amount) return false;

            Money -= amount;
            PublishMoneyChanged(-amount);
            return true;
        }

        /// <summary>자금을 추가한다 (보조금, 이벤트 등).</summary>
        public void AddMoney(int amount)
        {
            if (amount <= 0) return;
            Money += amount;
            PublishMoneyChanged(amount);
        }

        public void SetResidentTaxPerMonth(int value)
        {
            _residentTaxPerMonth = Mathf.Max(0, value);
        }

        public void SetJobTaxPerMonth(int value)
        {
            _jobTaxPerMonth = Mathf.Max(0, value);
        }

        // -- 월별 정산 --

        private void OnMonthlyTick(MonthlyTickEvent e)
        {
            if (_config == null || _buildings == null) return;

            int baseIncome  = 0;
            int expenditure = 0;

            foreach (var kv in _buildings.GetAll())
            {
                var inst = kv.Value;
                if (inst == null || inst.Data == null) continue;
                var data = inst.Data;

                // 수입: 거주자 세금 + 고용자 세금
                baseIncome += data.ResidentCapacity * _residentTaxPerMonth;
                baseIncome += data.JobCapacity      * _jobTaxPerMonth;

                // 지출: 유지비
                expenditure += data.MaintenanceCostPerMonth;
            }

            float demandScore = (_resDemand + _comDemand + _indDemand) / 3f;
            float incomeMultiplier = 1f + demandScore * _config.IncomeMultiplierPerDemandPoint;
            incomeMultiplier = Mathf.Clamp(
                incomeMultiplier,
                _config.MinIncomeMultiplier,
                _config.MaxIncomeMultiplier);

            int income = Mathf.RoundToInt(baseIncome * incomeMultiplier);
            int net = income - expenditure;
            Money  += net;

            // 예산 보고 이벤트
            GameEventBus.Publish(new BudgetReportEvent
            {
                BaseIncome  = baseIncome,
                Income      = income,
                Expenditure = expenditure,
                Balance     = Money,
                Month       = e.Month,
                Year        = e.Year,
                IncomeMultiplier = incomeMultiplier,
            });

            PublishMoneyChanged(net);
            HandleOperationalAlerts(net);

            // 파산 체크
            if (!IsBankrupt && _config != null && Money < _config.BankruptcyThreshold)
            {
                IsBankrupt = true;
                GameEventBus.Publish(new NotificationEvent
                {
                    Message = "WARN 도시 파산! 자금이 한계를 초과했습니다.",
                    Type    = NotificationType.Alert
                });
                Debug.LogWarning("[EconomyManager] 파산!");
            }

            Debug.Log($"[Economy] {e.Year}/{e.Month} 기본수입:{baseIncome:N0} 배율:{incomeMultiplier:F2} 수입:{income:N0} 지출:{expenditure:N0} 잔액:{Money:N0}");
        }

        // -- 내부 --

        private void OnDemandChanged(DemandChangedEvent e)
        {
            _resDemand = e.ResidentialDemand;
            _comDemand = e.CommercialDemand;
            _indDemand = e.IndustrialDemand;
        }

        private void PublishMoneyChanged(int delta)
        {
            GameEventBus.Publish(new MoneyChangedEvent
            {
                NewAmount = Money,
                Delta     = delta,
            });
        }

        private void HandleOperationalAlerts(int monthlyNet)
        {
            if (monthlyNet < 0) _deficitStreakMonths++;
            else _deficitStreakMonths = 0;

            if (_deficitStreakMonths >= Mathf.Max(1, _config.DeficitAlertAfterMonths))
            {
                GameEventBus.Publish(new NotificationEvent
                {
                    Message = $"재정 경고: 순손실 {_deficitStreakMonths}개월 연속",
                    Type = NotificationType.Warning
                });
            }

            int threshold = Mathf.Max(1, _config.DemandOverheatThreshold);
            bool overheat =
                Mathf.Abs(_resDemand) >= threshold ||
                Mathf.Abs(_comDemand) >= threshold ||
                Mathf.Abs(_indDemand) >= threshold;

            if (overheat)
            {
                GameEventBus.Publish(new NotificationEvent
                {
                    Message = $"수요 과열 경고: R{_resDemand:+#;-#;0} C{_comDemand:+#;-#;0} I{_indDemand:+#;-#;0}",
                    Type = NotificationType.Warning
                });
            }
        }
    }
}

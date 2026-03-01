using UnityEngine;
using CivilSim.Core;
using CivilSim.Buildings;

namespace CivilSim.Economy
{
    /// <summary>
    /// 도시 자금을 관리한다.
    ///
    /// 수입/지출 구조:
    ///   - 수입: 매월 (거주자 × 세율) + (고용자 × 세율)
    ///   - 지출: 매월 모든 건물의 MaintenanceCostPerMonth 합산
    ///
    /// GameManager.Instance.Economy 로 접근.
    /// </summary>
    public class EconomyManager : MonoBehaviour
    {
        // ── 인스펙터 ──────────────────────────────────────────
        [Header("설정")]
        [SerializeField] private EconomyConfig _config;

        // ── 공개 상태 ─────────────────────────────────────────
        public int  Money        { get; private set; }
        public bool IsBankrupt   { get; private set; }

        // ── 내부 참조 ─────────────────────────────────────────
        private BuildingManager _buildings;

        // ── Unity ────────────────────────────────────────────

        private void Start()
        {
            _buildings = GameManager.Instance.Buildings;

            if (_config == null)
            {
                Debug.LogError("[EconomyManager] EconomyConfig이 할당되지 않았습니다!");
                return;
            }

            Money = _config.InitialBudget;
            PublishMoneyChanged(0);

            GameEventBus.Subscribe<MonthlyTickEvent>(OnMonthlyTick);
        }

        private void OnDestroy()
        {
            GameEventBus.Unsubscribe<MonthlyTickEvent>(OnMonthlyTick);
        }

        // ── 공개 API ──────────────────────────────────────────

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

        // ── 월별 정산 ──────────────────────────────────────────

        private void OnMonthlyTick(MonthlyTickEvent e)
        {
            if (_config == null || _buildings == null) return;

            int income      = 0;
            int expenditure = 0;

            foreach (var kv in _buildings.GetAll())
            {
                var inst = kv.Value;
                if (inst == null || inst.Data == null) continue;
                var data = inst.Data;

                // 수입: 거주자 세금 + 고용자 세금
                income += data.ResidentCapacity * _config.TaxPerResidentPerMonth;
                income += data.JobCapacity      * _config.TaxPerJobPerMonth;

                // 지출: 유지비
                expenditure += data.MaintenanceCostPerMonth;
            }

            int net = income - expenditure;
            Money  += net;

            // 예산 보고 이벤트
            GameEventBus.Publish(new BudgetReportEvent
            {
                Income      = income,
                Expenditure = expenditure,
                Balance     = Money,
                Month       = e.Month,
                Year        = e.Year,
            });

            PublishMoneyChanged(net);

            // 파산 체크
            if (!IsBankrupt && _config != null && Money < _config.BankruptcyThreshold)
            {
                IsBankrupt = true;
                GameEventBus.Publish(new NotificationEvent
                {
                    Message = "⚠️ 도시 파산! 자금이 한계를 초과했습니다.",
                    Type    = NotificationType.Alert
                });
                Debug.LogWarning("[EconomyManager] 파산!");
            }

            Debug.Log($"[Economy] {e.Year}/{e.Month} 수입:{income:N0} 지출:{expenditure:N0} 잔액:{Money:N0}");
        }

        // ── 내부 ─────────────────────────────────────────────

        private void PublishMoneyChanged(int delta)
        {
            GameEventBus.Publish(new MoneyChangedEvent
            {
                NewAmount = Money,
                Delta     = delta,
            });
        }
    }
}

using NUnit.Framework;
using CivilSim.Economy;

namespace CivilSim.Tests
{
    /// <summary>
    /// EconomyFormula 단위 테스트.
    /// MonoBehaviour 없이 순수 계산 공식만 검증한다.
    /// </summary>
    public class EconomyFormulaTests
    {
        // ── CalcIncomeMultiplier ──────────────────────────────────────

        [Test]
        public void IncomeMultiplier_ZeroDemand_ReturnsMinimumClamped()
        {
            // 수요가 0이면 1 + 0 = 1.0, min이 0.5라면 1.0
            float result = EconomyFormula.CalcIncomeMultiplier(
                demandScore: 0f, perDemandPoint: 0.01f, min: 0.5f, max: 2.0f);

            Assert.AreEqual(1.0f, result, delta: 0.001f);
        }

        [Test]
        public void IncomeMultiplier_HighDemand_ClampsToMax()
        {
            // 수요 200 × 0.01 = 2.0 → 1 + 2.0 = 3.0 → max 2.0으로 클램프
            float result = EconomyFormula.CalcIncomeMultiplier(
                demandScore: 200f, perDemandPoint: 0.01f, min: 0.5f, max: 2.0f);

            Assert.AreEqual(2.0f, result, delta: 0.001f);
        }

        [Test]
        public void IncomeMultiplier_NegativeDemand_ClampsToMin()
        {
            // 수요가 음수인 경우 min으로 클램프
            float result = EconomyFormula.CalcIncomeMultiplier(
                demandScore: -100f, perDemandPoint: 0.01f, min: 0.5f, max: 2.0f);

            Assert.AreEqual(0.5f, result, delta: 0.001f);
        }

        // ── CalcResidentIncome ────────────────────────────────────────

        [Test]
        public void ResidentIncome_ZeroOperationRate_ReturnsZero()
        {
            // 전기/수도 공급 없음(operationRate=0) → 수입 0
            int result = EconomyFormula.CalcResidentIncome(
                residentBase: 10_000, incomeMultiplier: 1.5f,
                residentMultiplier: 1.0f, operationRate: 0f);

            Assert.AreEqual(0, result);
        }

        [Test]
        public void ResidentIncome_FullOperation_ReturnsScaledValue()
        {
            // 1000 × 1.5 × 1.0 × 1.0 = 1500
            int result = EconomyFormula.CalcResidentIncome(
                residentBase: 1_000, incomeMultiplier: 1.5f,
                residentMultiplier: 1.0f, operationRate: 1.0f);

            Assert.AreEqual(1_500, result);
        }

        [Test]
        public void ResidentIncome_HalfOperation_ReturnsHalfValue()
        {
            // 2000 × 1.0 × 1.0 × 0.5 = 1000
            int result = EconomyFormula.CalcResidentIncome(
                residentBase: 2_000, incomeMultiplier: 1.0f,
                residentMultiplier: 1.0f, operationRate: 0.5f);

            Assert.AreEqual(1_000, result);
        }

        // ── CalcExpenditure ───────────────────────────────────────────

        [Test]
        public void Expenditure_NoBuildings_ReturnsZero()
        {
            int result = EconomyFormula.CalcExpenditure(
                baseExpenditure: 0, maintenanceMultiplier: 1.0f);

            Assert.AreEqual(0, result);
        }

        [Test]
        public void Expenditure_ScalesWithMultiplier()
        {
            // 유지비 5000 × 1.2 = 6000
            int result = EconomyFormula.CalcExpenditure(
                baseExpenditure: 5_000, maintenanceMultiplier: 1.2f);

            Assert.AreEqual(6_000, result);
        }

        // ── CalcNet ───────────────────────────────────────────────────

        [Test]
        public void Net_IncomeExceedsExpenditure_ReturnsPositive()
        {
            int result = EconomyFormula.CalcNet(income: 5_000, expenditure: 3_000);
            Assert.AreEqual(2_000, result);
        }

        [Test]
        public void Net_ExpenditureExceedsIncome_ReturnsNegative()
        {
            int result = EconomyFormula.CalcNet(income: 1_000, expenditure: 4_000);
            Assert.AreEqual(-3_000, result);
        }
    }
}

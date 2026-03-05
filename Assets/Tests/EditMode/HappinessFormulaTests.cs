using NUnit.Framework;
using CivilSim.Population;

namespace CivilSim.Tests
{
    /// <summary>
    /// HappinessFormula 단위 테스트.
    /// MonoBehaviour 없이 순수 계산 공식만 검증한다.
    /// </summary>
    public class HappinessFormulaTests
    {
        private const float DefaultServiceWeight   = 0.6f;
        private const float DefaultOperationWeight = 0.4f;
        private const float Delta = 0.01f;

        // ── 정상 범위 ────────────────────────────────────────────────

        [Test]
        public void Happiness_FullServiceAndOperation_Returns100()
        {
            // 서비스 100점 + 운영률 1.0 → 0.6×100 + 0.4×100 = 100
            float result = HappinessFormula.Calculate(
                serviceScore: 100f, operationRate: 1.0f,
                serviceWeight: DefaultServiceWeight, operationWeight: DefaultOperationWeight);

            Assert.AreEqual(100f, result, Delta);
        }

        [Test]
        public void Happiness_NoServiceAndOperation_ReturnsZero()
        {
            // 서비스 0점 + 운영률 0 → 0
            float result = HappinessFormula.Calculate(
                serviceScore: 0f, operationRate: 0f,
                serviceWeight: DefaultServiceWeight, operationWeight: DefaultOperationWeight);

            Assert.AreEqual(0f, result, Delta);
        }

        [Test]
        public void Happiness_MidValues_ReturnsWeightedAverage()
        {
            // 서비스 50점 + 운영률 0.5 → 0.6×50 + 0.4×50 = 30 + 20 = 50
            float result = HappinessFormula.Calculate(
                serviceScore: 50f, operationRate: 0.5f,
                serviceWeight: DefaultServiceWeight, operationWeight: DefaultOperationWeight);

            Assert.AreEqual(50f, result, Delta);
        }

        [Test]
        public void Happiness_FullServiceNoOperation_OnlyServiceContributes()
        {
            // 서비스 100점 + 운영률 0 → 0.6×100 + 0.4×0 = 60
            float result = HappinessFormula.Calculate(
                serviceScore: 100f, operationRate: 0f,
                serviceWeight: DefaultServiceWeight, operationWeight: DefaultOperationWeight);

            Assert.AreEqual(60f, result, Delta);
        }

        [Test]
        public void Happiness_NoServiceFullOperation_OnlyOperationContributes()
        {
            // 서비스 0점 + 운영률 1.0 → 0.6×0 + 0.4×100 = 40
            float result = HappinessFormula.Calculate(
                serviceScore: 0f, operationRate: 1.0f,
                serviceWeight: DefaultServiceWeight, operationWeight: DefaultOperationWeight);

            Assert.AreEqual(40f, result, Delta);
        }

        // ── 경계값 / 클램프 ──────────────────────────────────────────

        [Test]
        public void Happiness_ServiceScoreAbove100_ClampsTo100()
        {
            // serviceScore가 150이어도 100으로 클램프 → 0.6×100 + 0.4×100 = 100
            float result = HappinessFormula.Calculate(
                serviceScore: 150f, operationRate: 1.0f,
                serviceWeight: DefaultServiceWeight, operationWeight: DefaultOperationWeight);

            Assert.AreEqual(100f, result, Delta);
        }

        [Test]
        public void Happiness_OperationRateAbove1_ClampsTo100Percent()
        {
            // operationRate가 2.0이어도 운영 행복도는 100 → 결과 100
            float result = HappinessFormula.Calculate(
                serviceScore: 100f, operationRate: 2.0f,
                serviceWeight: DefaultServiceWeight, operationWeight: DefaultOperationWeight);

            Assert.AreEqual(100f, result, Delta);
        }

        [Test]
        public void Happiness_NegativeValues_ClampsToZero()
        {
            // 음수 입력 → 0으로 클램프
            float result = HappinessFormula.Calculate(
                serviceScore: -50f, operationRate: -1f,
                serviceWeight: DefaultServiceWeight, operationWeight: DefaultOperationWeight);

            Assert.AreEqual(0f, result, Delta);
        }

        // ── 가중치 검증 ──────────────────────────────────────────────

        [Test]
        public void Happiness_CustomWeights_CalculatesCorrectly()
        {
            // 가중치를 서비스 0.8 / 운영 0.2로 변경
            // 서비스 80점 + 운영률 0.5 → 0.8×80 + 0.2×50 = 64 + 10 = 74
            float result = HappinessFormula.Calculate(
                serviceScore: 80f, operationRate: 0.5f,
                serviceWeight: 0.8f, operationWeight: 0.2f);

            Assert.AreEqual(74f, result, Delta);
        }
    }
}

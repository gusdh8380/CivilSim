using UnityEngine;

namespace CivilSim.Population
{
    /// <summary>
    /// 시민 행복도 계산 공식.
    /// MonoBehaviour에 의존하지 않는 순수 정적 계산이므로 EditMode 테스트 가능.
    ///
    /// 행복도 공식:
    ///   happiness = serviceWeight × clamp(serviceScore, 0, 100)
    ///             + operationWeight × clamp(operationRate × 100, 0, 100)
    ///   결과는 [0, 100] 범위로 클램프된다.
    /// </summary>
    public static class HappinessFormula
    {
        /// <summary>
        /// 시민 행복도를 계산한다.
        /// </summary>
        /// <param name="serviceScore">서비스 커버리지 점수 (0~100)</param>
        /// <param name="operationRate">전력·수도 운영률 (0~1)</param>
        /// <param name="serviceWeight">서비스 가중치</param>
        /// <param name="operationWeight">운영률 가중치</param>
        /// <returns>행복도 (0~100)</returns>
        public static float Calculate(
            float serviceScore,
            float operationRate,
            float serviceWeight,
            float operationWeight)
        {
            float serviceHappiness   = Mathf.Clamp(serviceScore,        0f, 100f);
            float operationHappiness = Mathf.Clamp(operationRate * 100f, 0f, 100f);

            float result = serviceWeight * serviceHappiness
                         + operationWeight * operationHappiness;

            return Mathf.Clamp(result, 0f, 100f);
        }
    }
}

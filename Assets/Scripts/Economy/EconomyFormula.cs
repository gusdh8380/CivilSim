using UnityEngine;

namespace CivilSim.Economy
{
    /// <summary>
    /// 월별 경제 계산 공식 모음.
    /// MonoBehaviour에 의존하지 않는 순수 정적 계산이므로 EditMode 테스트 가능.
    ///
    /// 수입 공식:
    ///   incomeMultiplier = clamp(1 + demandScore × perDemandPoint, min, max)
    ///   residentIncome   = residentBase × incomeMultiplier × residentMultiplier × operationRate
    ///   jobIncome        = jobBase × incomeMultiplier × jobMultiplier × operationRate
    ///   expenditure      = baseExpenditure × maintenanceMultiplier
    ///   net              = (residentIncome + jobIncome) − expenditure
    /// </summary>
    public static class EconomyFormula
    {
        /// <summary>
        /// 수요 점수로 소득 배율을 계산한다.
        /// </summary>
        /// <param name="demandScore">수요 평균 점수 (R+C+I 평균, 0~100+)</param>
        /// <param name="perDemandPoint">수요 1점당 배율 증가량</param>
        /// <param name="min">최소 배율</param>
        /// <param name="max">최대 배율</param>
        public static float CalcIncomeMultiplier(
            float demandScore,
            float perDemandPoint,
            float min,
            float max)
        {
            return Mathf.Clamp(1f + demandScore * perDemandPoint, min, max);
        }

        /// <summary>
        /// 거주자 세수를 계산한다.
        /// </summary>
        public static int CalcResidentIncome(
            int   residentBase,
            float incomeMultiplier,
            float residentMultiplier,
            float operationRate)
        {
            return Mathf.RoundToInt(residentBase * incomeMultiplier * residentMultiplier * operationRate);
        }

        /// <summary>
        /// 고용 세수를 계산한다.
        /// </summary>
        public static int CalcJobIncome(
            int   jobBase,
            float incomeMultiplier,
            float jobMultiplier,
            float operationRate)
        {
            return Mathf.RoundToInt(jobBase * incomeMultiplier * jobMultiplier * operationRate);
        }

        /// <summary>
        /// 유지비(지출)를 계산한다.
        /// </summary>
        public static int CalcExpenditure(int baseExpenditure, float maintenanceMultiplier)
        {
            return Mathf.RoundToInt(baseExpenditure * maintenanceMultiplier);
        }

        /// <summary>
        /// 월 순수익을 계산한다. (income - expenditure)
        /// </summary>
        public static int CalcNet(int income, int expenditure)
        {
            return income - expenditure;
        }
    }
}

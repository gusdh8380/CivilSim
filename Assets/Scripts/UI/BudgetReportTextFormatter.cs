using UnityEngine;
using CivilSim.Core;

namespace CivilSim.UI
{
    /// <summary>
    /// 월간 결산 텍스트 포매팅 공통 유틸리티.
    /// HUD와 월간보고 패널의 문자열 로직을 한 곳에서 유지한다.
    /// </summary>
    public static class BudgetReportTextFormatter
    {
        public static string BuildUtilityPart(BudgetReportEvent e)
        {
            return
                $" | 전력 {Mathf.RoundToInt(e.PowerRate * 100f)}%" +
                $" | 수도 {Mathf.RoundToInt(e.WaterRate * 100f)}%" +
                $" | 운영 {Mathf.RoundToInt(e.OperationRate * 100f)}%" +
                $" | 교육 {e.EducationScore}%" +
                $" | 의료 {e.HealthcareScore}%" +
                $" | 치안 {e.SafetyScore}%" +
                $" | 위생 {e.SanitationScore}%";
        }

        public static string BuildBudgetLine(BudgetReportEvent e)
        {
            int net = e.Income - e.Expenditure;
            string netText = FormatSigned(net);

            return
                $"{e.Year}년 {e.Month:D2}월 결산 | 수입 {e.Income:N0} | 지출 {e.Expenditure:N0} | 순이익 {netText} | 잔액 {e.Balance:N0}" +
                BuildUtilityPart(e);
        }

        private static string FormatSigned(int value)
        {
            return value >= 0 ? $"+{value:N0}" : value.ToString("N0");
        }
    }
}

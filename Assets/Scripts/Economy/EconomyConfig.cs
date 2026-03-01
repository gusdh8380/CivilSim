using UnityEngine;

namespace CivilSim.Economy
{
    /// <summary>
    /// 경제 시스템 초기값 및 세율 설정.
    /// Project → Create → CivilSim → Economy → EconomyConfig
    /// </summary>
    [CreateAssetMenu(menuName = "CivilSim/Economy/EconomyConfig", fileName = "EconomyConfig")]
    public class EconomyConfig : ScriptableObject
    {
        [Header("초기 예산")]
        [Tooltip("게임 시작 시 보유 자금")]
        public int InitialBudget = 500_000;

        [Header("세율 (월별 1인당 수입)")]
        [Tooltip("주거 건물 거주자 1인당 월 세금")]
        public int TaxPerResidentPerMonth = 100;
        [Tooltip("상업/공업 건물 고용자 1인당 월 세금")]
        public int TaxPerJobPerMonth = 80;

        [Header("파산")]
        [Tooltip("이 금액 미만이면 파산 처리")]
        public int BankruptcyThreshold = -50_000;
    }
}

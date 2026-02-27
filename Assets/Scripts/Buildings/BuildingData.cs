using UnityEngine;

namespace CivilSim.Buildings
{
    public enum BuildingCategory
    {
        Residential,    // 주거
        Commercial,     // 상업
        Industrial,     // 공업
        Public,         // 공공시설 (학교, 병원, 소방서)
        Utility,        // 유틸리티 (발전소, 정수장)
        Infrastructure  // 인프라 (도로, 교량) — 추후 확장
    }

    /// <summary>
    /// 건물의 정적 데이터 정의. ScriptableObject로 에셋화.
    /// Project → Create → CivilSim → Buildings → BuildingData
    /// </summary>
    [CreateAssetMenu(menuName = "CivilSim/Buildings/BuildingData", fileName = "New BuildingData")]
    public class BuildingData : ScriptableObject
    {
        // ── 기본 정보 ─────────────────────────────────────────
        [Header("기본 정보")]
        public string BuildingName = "새 건물";
        public BuildingCategory Category = BuildingCategory.Residential;
        [TextArea(2, 3)]
        public string Description = "";

        // ── 크기 (그리드 셀 단위) ─────────────────────────────
        [Header("크기 (그리드 셀 단위)")]
        [Min(1)] public int SizeX = 1;
        [Min(1)] public int SizeZ = 1;

        // ── 비용 ─────────────────────────────────────────────
        [Header("비용")]
        [Min(0)] public int BuildCost            = 1000;  // 건설 비용
        [Min(0)] public int MaintenanceCostPerMonth = 50; // 월 유지비

        // ── 인구 / 고용 ───────────────────────────────────────
        [Header("인구 / 고용")]
        [Min(0)] public int ResidentCapacity = 0;  // 거주 가능 인원
        [Min(0)] public int JobCapacity      = 0;  // 고용 가능 인원

        // ── 유틸리티 ─────────────────────────────────────────
        [Header("유틸리티")]
        public bool RequiresPower = true;
        public bool RequiresWater = true;
        [Min(0)] public int PowerConsumption = 10; // kW/월
        [Min(0)] public int WaterConsumption = 10; // L/월

        // ── 시각화 ───────────────────────────────────────────
        [Header("시각화")]
        public GameObject Prefab; // 3D 프리팹 (없으면 큐브로 대체)
        public Sprite     Icon;   // UI 아이콘

        // ── 파생 프로퍼티 ─────────────────────────────────────
        public Vector2Int Size => new Vector2Int(SizeX, SizeZ);
        public bool Is1x1     => SizeX == 1 && SizeZ == 1;

        /// 이 건물이 정상 운영되기 위해 전기가 필요한지
        public bool NeedsUtility => RequiresPower || RequiresWater;
    }
}

using UnityEngine;

namespace CivilSim.Buildings
{
    /// <summary>
    /// 씬에 배치된 건물 하나의 런타임 상태를 보유한다.
    /// BuildingManager가 생성 및 관리. Prefab에 자동 부착.
    /// </summary>
    public class BuildingInstance : MonoBehaviour
    {
        // ── 식별 정보 ─────────────────────────────────────────
        public int          InstanceId    { get; private set; }
        public BuildingData Data          { get; private set; }
        public UnityEngine.Vector2Int GridOrigin { get; private set; }

        // ── 유틸리티 상태 ─────────────────────────────────────
        public bool IsPowered { get; set; } = false;
        public bool IsWatered { get; set; } = false;

        /// 전기/수도 조건을 모두 충족하면 운영 중
        public bool IsOperational =>
            (!Data.RequiresPower || IsPowered) &&
            (!Data.RequiresWater || IsWatered);

        // ── 초기화 ───────────────────────────────────────────

        public void Initialize(int id, BuildingData data, UnityEngine.Vector2Int gridOrigin)
        {
            InstanceId  = id;
            Data        = data;
            GridOrigin  = gridOrigin;
            name        = $"[Building] {data.BuildingName} ({gridOrigin.x},{gridOrigin.y})";
        }

        // ── 공개 API ─────────────────────────────────────────

        /// 운영 상태 변경 시 비주얼 업데이트 트리거 (추후 구현)
        public void RefreshVisual()
        {
            // TODO: 전기/수도 미연결 시 어둡게 처리
        }

        public override string ToString()
            => $"BuildingInstance[{InstanceId}] {Data?.BuildingName} @ {GridOrigin}";
    }
}

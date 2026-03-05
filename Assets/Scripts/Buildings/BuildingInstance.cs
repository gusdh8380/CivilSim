using UnityEngine;

namespace CivilSim.Buildings
{
    /// <summary>
    /// 씬에 배치된 건물 하나의 런타임 상태를 보유한다.
    /// BuildingManager가 생성 및 관리. Prefab에 자동 부착.
    /// </summary>
    public class BuildingInstance : MonoBehaviour
    {
        // -- 식별 정보 --
        public int          InstanceId { get; private set; }
        public BuildingData Data       { get; private set; }
        public UnityEngine.Vector2Int GridOrigin { get; private set; }
        /// 배치 시 회전값 (0=0°, 1=90°, 2=180°, 3=270°)
        public int Rotation { get; private set; }

        // -- 유틸리티 상태 --
        public bool IsPowered { get; set; } = false;
        public bool IsWatered { get; set; } = false;

        /// 전기/수도 조건을 모두 충족하면 운영 중
        public bool IsOperational =>
            (!Data.RequiresPower || IsPowered) &&
            (!Data.RequiresWater || IsWatered);

        // -- 비주얼 --
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color OperationalTint   = Color.white;
        private static readonly Color UnoperationalTint = new Color(0.45f, 0.45f, 0.45f, 1f);

        private MeshRenderer[] _renderers;
        private MaterialPropertyBlock _propBlock;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<MeshRenderer>(includeInactive: true);
            _propBlock = new MaterialPropertyBlock();
        }

        // -- 초기화 --

        public void Initialize(int id, BuildingData data, UnityEngine.Vector2Int gridOrigin, int rotation = 0)
        {
            InstanceId = id;
            Data       = data;
            GridOrigin = gridOrigin;
            Rotation   = rotation;
            name       = $"[Building] {data.BuildingName} ({gridOrigin.x},{gridOrigin.y}) R{rotation * 90}°";
            RefreshVisual();
        }

        /// 회전을 반영한 실제 점유 셀 크기
        public UnityEngine.Vector2Int EffectiveSize => Rotation % 2 == 0
            ? new UnityEngine.Vector2Int(Data.SizeX, Data.SizeZ)
            : new UnityEngine.Vector2Int(Data.SizeZ, Data.SizeX);

        // -- 공개 API --

        /// 전기/수도 상태에 따라 건물 색상을 갱신한다.
        /// UtilityManager가 IsPowered/IsWatered 갱신 후 호출.
        public void RefreshVisual()
        {
            if (_renderers == null) return;
            Color tint = IsOperational ? OperationalTint : UnoperationalTint;
            foreach (var r in _renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_propBlock);
                _propBlock.SetColor(BaseColorId, tint);
                r.SetPropertyBlock(_propBlock);
            }
        }

        public override string ToString()
            => $"BuildingInstance[{InstanceId}] {Data?.BuildingName} @ {GridOrigin}";
    }
}

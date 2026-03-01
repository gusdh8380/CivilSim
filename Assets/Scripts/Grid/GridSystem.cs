using System.Collections.Generic;
using UnityEngine;

namespace CivilSim.Grid
{
    /// <summary>
    /// 그리드 전체 데이터를 관리한다.
    /// 좌표 변환, 셀 상태 쿼리/수정, 유효성 검사를 담당.
    /// </summary>
    public class GridSystem : MonoBehaviour
    {
        // ── 인스펙터 ────────────────────────────────────────
        [Header("Grid Size")]
        [SerializeField, Range(10, 200)] private int _width  = 100;
        [SerializeField, Range(10, 200)] private int _height = 100;

        [Header("Cell Size")]
        [Tooltip("도로 프리팹이 할당되면 XZ 크기에서 자동 계산. 미할당 시 아래 값을 사용.")]
        [SerializeField] private GameObject _referenceRoadPrefab;
        [Tooltip("_referenceRoadPrefab 미할당 시 사용할 수동 설정값.")]
        [SerializeField, Range(1f, 50f)] private float _cellSize = 10f;

        [Header("Origin")]
        [SerializeField] private Vector3 _originOffset = Vector3.zero;

        // ── 공개 프로퍼티 ────────────────────────────────────
        public int   Width    => _width;
        public int   Height   => _height;
        public float CellSize => _cellSize;
        public Vector3 Origin => _originOffset;

        /// 그리드 중심 월드 좌표
        public Vector3 Center => _originOffset + new Vector3(_width * _cellSize * 0.5f, 0f, _height * _cellSize * 0.5f);

        // ── 내부 ─────────────────────────────────────────────
        private GridCell[,] _cells;

        // ── Unity ─────────────────────────────────────────────

        private void Awake()
        {
            if (_referenceRoadPrefab != null)
            {
                float detected = DetectTileSize(_referenceRoadPrefab);
                if (detected > 0.1f)
                {
                    Debug.Log($"[GridSystem] 도로 프리팹에서 CellSize 자동 감지: {detected:F2} (기존: {_cellSize})");
                    _cellSize = detected;
                }
                else
                {
                    Debug.LogWarning($"[GridSystem] 도로 프리팹 크기 감지 실패 — CellSize={_cellSize} 유지");
                }
            }

            InitializeGrid();
        }

        // ── 초기화 ────────────────────────────────────────────

        private void InitializeGrid()
        {
            _cells = new GridCell[_width, _height];
            for (int col = 0; col < _width; col++)
                for (int row = 0; row < _height; row++)
                    _cells[col, row] = new GridCell(col, row);

            Debug.Log($"[GridSystem] {_width}×{_height} 그리드 초기화 완료. 총 {_width * _height}셀");
        }

        // ── 좌표 변환 ─────────────────────────────────────────

        /// 그리드 좌표 → 월드 좌표 (셀 중심)
        public Vector3 GridToWorld(int col, int row)
            => _originOffset + new Vector3(col * _cellSize + _cellSize * 0.5f, 0f, row * _cellSize + _cellSize * 0.5f);

        public Vector3 GridToWorld(Vector2Int pos)
            => GridToWorld(pos.x, pos.y);

        /// 월드 좌표 → 그리드 좌표 (클램프 없음, 범위 밖일 수 있음)
        public Vector2Int WorldToGrid(Vector3 worldPos)
        {
            Vector3 local = worldPos - _originOffset;
            int col = Mathf.FloorToInt(local.x / _cellSize);
            int row = Mathf.FloorToInt(local.z / _cellSize);
            return new Vector2Int(col, row);
        }

        /// 월드 좌표 → 그리드 좌표 (그리드 범위로 클램프)
        public Vector2Int WorldToGridClamped(Vector3 worldPos)
        {
            Vector2Int pos = WorldToGrid(worldPos);
            pos.x = Mathf.Clamp(pos.x, 0, _width  - 1);
            pos.y = Mathf.Clamp(pos.y, 0, _height - 1);
            return pos;
        }

        // ── 유효성 검사 ───────────────────────────────────────

        public bool IsValid(int col, int row)
            => col >= 0 && col < _width && row >= 0 && row < _height;

        public bool IsValid(Vector2Int pos)
            => IsValid(pos.x, pos.y);

        /// 멀티셀 건물(size×size)이 배치 가능한지 검사
        public bool IsValidArea(Vector2Int origin, int sizeCol, int sizeRow)
        {
            for (int dc = 0; dc < sizeCol; dc++)
                for (int dr = 0; dr < sizeRow; dr++)
                    if (!IsValid(origin.x + dc, origin.y + dr))
                        return false;
            return true;
        }

        // ── 셀 접근 ───────────────────────────────────────────

        public GridCell GetCell(int col, int row)
            => IsValid(col, row) ? _cells[col, row] : null;

        public GridCell GetCell(Vector2Int pos)
            => GetCell(pos.x, pos.y);

        public GridCell GetCellFromWorld(Vector3 worldPos)
            => GetCell(WorldToGrid(worldPos));

        // ── 셀 상태 쿼리 ──────────────────────────────────────

        public bool IsEmpty(Vector2Int pos)
        {
            var cell = GetCell(pos);
            return cell != null && cell.IsEmpty;
        }

        public bool CanBuild(Vector2Int pos)
        {
            var cell = GetCell(pos);
            return cell != null && cell.CanBuild;
        }

        /// 멀티셀 영역이 모두 배치 가능한지
        public bool CanBuildArea(Vector2Int origin, int sizeCol, int sizeRow)
        {
            if (!IsValidArea(origin, sizeCol, sizeRow)) return false;
            for (int dc = 0; dc < sizeCol; dc++)
                for (int dr = 0; dr < sizeRow; dr++)
                    if (!CanBuild(new Vector2Int(origin.x + dc, origin.y + dr)))
                        return false;
            return true;
        }

        // ── 셀 수정 ───────────────────────────────────────────

        public void SetState(Vector2Int pos, CellState state)
        {
            var cell = GetCell(pos);
            if (cell != null) cell.State = state;
        }

        public void SetZone(Vector2Int pos, ZoneType zone)
        {
            var cell = GetCell(pos);
            if (cell == null) return;
            cell.Zone = zone;
            if (cell.State == CellState.Empty)
                cell.State = CellState.Zone;
        }

        public void PlaceBuilding(Vector2Int pos, int buildingId)
        {
            var cell = GetCell(pos);
            if (cell == null) return;
            cell.State      = CellState.Building;
            cell.BuildingId = buildingId;
        }

        public void RemoveBuilding(Vector2Int pos)
        {
            var cell = GetCell(pos);
            if (cell == null) return;
            // 철거 후 지반은 유지 (Foundation → 재건 가능)
            cell.State      = CellState.Foundation;
            cell.BuildingId = -1;
        }

        public void PlaceFoundation(Vector2Int pos)
        {
            var cell = GetCell(pos);
            if (cell == null) return;
            cell.State = CellState.Foundation;
        }

        public void RemoveFoundation(Vector2Int pos)
        {
            var cell = GetCell(pos);
            if (cell == null || cell.State != CellState.Foundation) return;
            cell.State = CellState.Empty;
        }

        public bool CanPlaceFoundationArea(Vector2Int origin, int sizeCol, int sizeRow)
        {
            if (!IsValidArea(origin, sizeCol, sizeRow)) return false;
            for (int dc = 0; dc < sizeCol; dc++)
                for (int dr = 0; dr < sizeRow; dr++)
                    if (!GetCell(new Vector2Int(origin.x + dc, origin.y + dr))?.CanPlaceFoundation ?? true)
                        return false;
            return true;
        }

        public void PlaceRoad(Vector2Int pos)
        {
            var cell = GetCell(pos);
            if (cell == null) return;
            cell.State = CellState.Road;
            cell.Zone  = ZoneType.None;
        }

        public void RemoveRoad(Vector2Int pos)
        {
            var cell = GetCell(pos);
            if (cell == null) return;
            cell.State = CellState.Empty;
        }

        // ── 이웃 셀 ───────────────────────────────────────────

        private static readonly Vector2Int[] _neighbors4 =
        {
            new( 0,  1), new( 0, -1),
            new( 1,  0), new(-1,  0)
        };

        private static readonly Vector2Int[] _neighbors8 =
        {
            new( 0,  1), new( 0, -1), new( 1,  0), new(-1,  0),
            new( 1,  1), new(-1,  1), new( 1, -1), new(-1, -1)
        };

        public List<GridCell> GetNeighbors4(Vector2Int pos)
        {
            var result = new List<GridCell>(4);
            foreach (var dir in _neighbors4)
            {
                var cell = GetCell(pos + dir);
                if (cell != null) result.Add(cell);
            }
            return result;
        }

        public List<GridCell> GetNeighbors8(Vector2Int pos)
        {
            var result = new List<GridCell>(8);
            foreach (var dir in _neighbors8)
            {
                var cell = GetCell(pos + dir);
                if (cell != null) result.Add(cell);
            }
            return result;
        }

        /// 해당 셀에 인접한 도로가 하나라도 있는지
        public bool HasAdjacentRoad(Vector2Int pos)
        {
            foreach (var neighbor in GetNeighbors4(pos))
                if (neighbor.HasRoad) return true;
            return false;
        }

        // ── 프리팹 크기 자동 감지 ─────────────────────────────

        /// <summary>
        /// 도로 프리팹의 MeshFilter.sharedMesh bounds 합산으로 XZ 타일 크기를 반환.
        /// Renderer.bounds 는 씬 밖 프리팹에서 (0,0,0) 을 반환하므로 sharedMesh 방식 사용.
        /// </summary>
        private static float DetectTileSize(GameObject prefab)
        {
            if (prefab == null) return 0f;

            var meshFilters = prefab.GetComponentsInChildren<MeshFilter>();
            if (meshFilters.Length == 0) return 0f;

            bool   hasBounds = false;
            Bounds combined  = default;

            foreach (var mf in meshFilters)
            {
                if (mf.sharedMesh == null) continue;
                Matrix4x4 m  = PrefabLocalToRootMatrix(mf.transform, prefab.transform);
                Bounds    b  = TransformBoundsStatic(mf.sharedMesh.bounds, m);
                if (!hasBounds) { combined = b; hasBounds = true; }
                else              combined.Encapsulate(b);
            }

            if (!hasBounds) return 0f;

            // XZ 중 큰 쪽을 타일 크기로 사용 (정방형이면 동일)
            float size = Mathf.Max(combined.size.x, combined.size.z);
            // 0.5 단위로 반올림
            return Mathf.Round(size * 2f) * 0.5f;
        }

        private static Matrix4x4 PrefabLocalToRootMatrix(Transform leaf, Transform root)
        {
            Matrix4x4 m = Matrix4x4.identity;
            Transform t = leaf;
            while (t != null && t != root)
            {
                m = Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale) * m;
                t = t.parent;
            }
            return m;
        }

        private static Bounds TransformBoundsStatic(Bounds local, Matrix4x4 m)
        {
            Vector3 c = local.center;
            Vector3 e = local.extents;
            Bounds  r = new Bounds(m.MultiplyPoint3x4(c + new Vector3( e.x,  e.y,  e.z)), Vector3.zero);
            r.Encapsulate(m.MultiplyPoint3x4(c + new Vector3(-e.x,  e.y,  e.z)));
            r.Encapsulate(m.MultiplyPoint3x4(c + new Vector3( e.x, -e.y,  e.z)));
            r.Encapsulate(m.MultiplyPoint3x4(c + new Vector3(-e.x, -e.y,  e.z)));
            r.Encapsulate(m.MultiplyPoint3x4(c + new Vector3( e.x,  e.y, -e.z)));
            r.Encapsulate(m.MultiplyPoint3x4(c + new Vector3(-e.x,  e.y, -e.z)));
            r.Encapsulate(m.MultiplyPoint3x4(c + new Vector3( e.x, -e.y, -e.z)));
            r.Encapsulate(m.MultiplyPoint3x4(c + new Vector3(-e.x, -e.y, -e.z)));
            return r;
        }
    }
}

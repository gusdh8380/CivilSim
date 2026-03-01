using System.Collections.Generic;
using UnityEngine;
using CivilSim.Core;
using CivilSim.Grid;

namespace CivilSim.Infrastructure
{
    /// <summary>
    /// 도로 배치·철거·스마트 Auto-Tiling을 담당한다.
    /// GameManager.Instance.Roads 로 접근.
    ///
    /// ── Auto-Tiling 비트마스크 ────────────────────────────
    ///   N=bit0(1), E=bit1(2), S=bit2(4), W=bit3(8)
    ///   조합 16가지 → isolated/end/straight/corner/tJunction/cross
    ///   + 각 방향에 맞는 Y 회전 자동 적용
    ///
    /// ── 비용 ─────────────────────────────────────────────
    ///   RoadCostPerTile (기본 500) — RoadBuilder 에서 사전 지불
    /// </summary>
    public class RoadManager : MonoBehaviour
    {
        // ── 인스펙터 ──────────────────────────────────────────
        [Header("도로 타일 데이터베이스 (Auto-Tiling)")]
        [SerializeField] private RoadTileDatabase _tileDatabase;

        [Header("도로 비용 (타일당)")]
        [SerializeField] private int _roadCostPerTile = 500;

        [Header("프리팹 Y 위치 오프셋 (피벗 보정)")]
        [Tooltip("프리팹의 피벗이 중심(0.1)이 아닌 바닥(0)이면 0, 중심이면 -0.1 등 조정")]
        [SerializeField] private float _roadHeightOffset = 0f;

        // ── 내부 상태 ─────────────────────────────────────────
        private GridSystem _grid;
        private Transform  _roadRoot;
        private Material   _fallbackMaterial;

        private readonly Dictionary<Vector2Int, GameObject> _roadObjects = new();

        public int Count           => _roadObjects.Count;
        public int RoadCostPerTile => _roadCostPerTile;

        // ── 비트마스크 조회 테이블 ────────────────────────────
        // 인덱스 = 비트마스크 (0~15)
        // 값     = (typeIdx, extraYRot)
        //   typeIdx: 0=isolated, 1=end, 2=corner, 3=straight, 4=tJunction, 5=cross
        //
        // 기준 프리팹 방향 (yRotationOffset = 0 일 때):
        //   end       → 열림 방향 = +Z (북, N)
        //   straight  → +Z ↔ -Z (남북, N-S)
        //   corner    → 북(N) + 동(E) 가 열림
        //   tJunction → 남(S)이 닫힘, 북·동·서(N·E·W) 열림  (비트마스크 11)
        //   cross     → 방향 무관
        private static readonly (int type, float rot)[] _bitmaskTable =
        {
            (0,   0f),   //  0: (없음)   → isolated
            (1,   0f),   //  1: N        → end,      열림: N (0°)
            (1,  90f),   //  2: E        → end,      열림: E (90°)
            (2,   0f),   //  3: N+E      → corner,   N·E (0°)
            (1, 180f),   //  4: S        → end,      열림: S (180°)
            (3,   0f),   //  5: N+S      → straight, 남북 (0°)
            (2,  90f),   //  6: E+S      → corner,   E·S (90°)
            (4,  90f),   //  7: N+E+S    → tJunction W 닫힘 (90°)
            (1, 270f),   //  8: W        → end,      열림: W (270°)
            (2, 270f),   //  9: N+W      → corner,   N·W (270°)
            (3,  90f),   // 10: E+W      → straight, 동서 (90°)
            (4,   0f),   // 11: N+E+W    → tJunction S 닫힘 (0°)
            (2, 180f),   // 12: S+W      → corner,   S·W (180°)
            (4, 270f),   // 13: N+S+W    → tJunction E 닫힘 (270°)
            (4, 180f),   // 14: E+S+W    → tJunction N 닫힘 (180°)
            (5,   0f),   // 15: N+E+S+W  → cross
        };

        // 4방향 오프셋 (N, E, S, W 순서 — 비트와 일치)
        private static readonly Vector2Int[] _dirs4 =
        {
            new( 0,  1),   // N (bit 0)
            new( 1,  0),   // E (bit 1)
            new( 0, -1),   // S (bit 2)
            new(-1,  0),   // W (bit 3)
        };

        // ── Unity ────────────────────────────────────────────

        private void Awake()
        {
            _roadRoot = new GameObject("=== Roads ===").transform;

            var shader        = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _fallbackMaterial = new Material(shader) { color = new Color(0.35f, 0.35f, 0.35f) };
            _fallbackMaterial.enableInstancing = true;
        }

        private void Start()
        {
            _grid = GameManager.Instance.Grid;

            // Inspector 미연결 시 씬에서 자동 탐색 (폴백)
            if (_grid == null) _grid = FindObjectOfType<GridSystem>();
            if (_grid == null) Debug.LogError("[RoadManager] GridSystem을 찾을 수 없습니다. GameManager에 할당해주세요.");
        }

        private void OnDestroy()
        {
            if (_fallbackMaterial != null) Destroy(_fallbackMaterial);
        }

        // ── 공개 API ──────────────────────────────────────────

        /// <summary>단일 타일 도로 배치. 성공 시 true.</summary>
        public bool TryPlaceRoad(Vector2Int pos)
        {
            if (_grid == null) return false;
            var cell = _grid.GetCell(pos);
            if (cell == null || cell.HasBuilding || cell.HasRoad) return false;

            _grid.PlaceRoad(pos);
            RefreshVisual(pos);
            RefreshNeighborVisuals(pos);

            GameEventBus.Publish(new RoadBuiltEvent { GridPosition = pos });
            return true;
        }

        /// <summary>단일 타일 도로 철거. 성공 시 true.</summary>
        public bool TryRemoveRoad(Vector2Int pos)
        {
            if (_grid == null) return false;
            var cell = _grid.GetCell(pos);
            if (cell == null || !cell.HasRoad) return false;

            _grid.RemoveRoad(pos);
            RemoveRoadVisual(pos);
            RefreshNeighborVisuals(pos);

            GameEventBus.Publish(new RoadRemovedEvent { GridPosition = pos });
            return true;
        }

        /// <summary>L자 라인 도로 일괄 배치. 실제 배치된 수 반환.</summary>
        public int TryPlaceRoadLine(Vector2Int start, Vector2Int end)
        {
            var cells  = GetLineCells(start, end);
            int placed = 0;
            foreach (var c in cells)
                if (TryPlaceRoad(c)) placed++;
            return placed;
        }

        /// <summary>L자 라인 도로 일괄 철거. 실제 철거된 수 반환.</summary>
        public int TryRemoveRoadLine(Vector2Int start, Vector2Int end)
        {
            var cells   = GetLineCells(start, end);
            int removed = 0;
            foreach (var c in cells)
                if (TryRemoveRoad(c)) removed++;
            return removed;
        }

        /// <summary>시작~끝 사이 L자(가로 우선) 셀 목록.</summary>
        public static List<Vector2Int> GetLineCells(Vector2Int from, Vector2Int to)
        {
            var cells = new List<Vector2Int>();
            int dx = to.x > from.x ? 1 : -1;
            int dz = to.y > from.y ? 1 : -1;

            for (int x = from.x; x != to.x; x += dx)
                cells.Add(new Vector2Int(x, from.y));
            for (int z = from.y; z != to.y; z += dz)
                cells.Add(new Vector2Int(to.x, z));
            cells.Add(to);
            return cells;
        }

        // ── Auto-Tiling 내부 ──────────────────────────────────

        /// <summary>
        /// 지정 셀의 비주얼을 비트마스크에 따라 갱신한다.
        /// 도로가 없는 셀에 호출되면 아무것도 하지 않는다.
        /// </summary>
        private void RefreshVisual(Vector2Int pos)
        {
            // 도로 없으면 처리 불필요
            var cell = _grid?.GetCell(pos);
            if (cell == null || !cell.HasRoad) return;

            // 기존 오브젝트 제거
            RemoveRoadVisual(pos);

            // 비트마스크 계산
            int bitmask               = ComputeBitmask(pos);
            var (typeIdx, extraRot)   = _bitmaskTable[bitmask];
            RoadTileEntry entry       = GetEntry(typeIdx);

            float finalRot = (entry.yRotationOffset + extraRot) % 360f;

            // 월드 위치 (GridToWorld는 셀 중심 XZ, Y=originOffset)
            Vector3 worldPos   = _grid.GridToWorld(pos);
            worldPos.y        += _roadHeightOffset;

            GameObject go;
            if (entry.prefab != null)
            {
                go = Instantiate(entry.prefab,
                                 worldPos,
                                 Quaternion.Euler(0f, finalRot, 0f),
                                 _roadRoot);
            }
            else
            {
                go = CreateFallbackCube(worldPos);
            }

            go.name     = $"Road_{pos.x}_{pos.y}";
            go.isStatic = true;
            _roadObjects[pos] = go;
        }

        /// <summary>4방향 이웃 셀의 비주얼을 모두 갱신한다.</summary>
        private void RefreshNeighborVisuals(Vector2Int pos)
        {
            foreach (var dir in _dirs4)
                RefreshVisual(pos + dir);
        }

        /// <summary>지정 위치의 도로 오브젝트만 Destroy (그리드 데이터 변경 없음).</summary>
        private void RemoveRoadVisual(Vector2Int pos)
        {
            if (!_roadObjects.TryGetValue(pos, out var go)) return;
            if (go != null) Destroy(go);
            _roadObjects.Remove(pos);
        }

        /// <summary>
        /// 비트마스크 계산: N=bit0, E=bit1, S=bit2, W=bit3.
        /// 이웃이 도로이면 해당 비트 ON.
        /// </summary>
        private int ComputeBitmask(Vector2Int pos)
        {
            int mask = 0;
            for (int i = 0; i < 4; i++)
            {
                var neighbor = _grid.GetCell(pos + _dirs4[i]);
                if (neighbor != null && neighbor.HasRoad)
                    mask |= (1 << i);
            }
            return mask;
        }

        /// <summary>typeIdx → RoadTileEntry 반환. 데이터베이스 미설정 시 폴백 엔트리.</summary>
        private RoadTileEntry GetEntry(int typeIdx)
        {
            if (_tileDatabase == null) return new RoadTileEntry();

            return typeIdx switch
            {
                0 => _tileDatabase.isolated  ?? new RoadTileEntry(),
                1 => _tileDatabase.end       ?? new RoadTileEntry(),
                2 => _tileDatabase.corner    ?? new RoadTileEntry(),
                3 => _tileDatabase.straight  ?? new RoadTileEntry(),
                4 => _tileDatabase.tJunction ?? new RoadTileEntry(),
                5 => _tileDatabase.cross     ?? new RoadTileEntry(),
                _ => new RoadTileEntry(),
            };
        }

        /// <summary>프리팹 없을 때 회색 납작 큐브 폴백 생성.</summary>
        private GameObject CreateFallbackCube(Vector3 worldPos)
        {
            float cs = _grid != null ? _grid.CellSize : 10f;
            var   go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(_roadRoot);
            // y 오프셋: 큐브 높이의 절반 (도로는 cs의 10% 두께)
            go.transform.position   = worldPos + new Vector3(0f, cs * 0.05f, 0f);
            go.transform.localScale = new Vector3(cs * 0.98f, cs * 0.1f, cs * 0.98f);
            go.GetComponent<Renderer>().sharedMaterial = _fallbackMaterial;
            Destroy(go.GetComponent<BoxCollider>());
            return go;
        }
    }
}

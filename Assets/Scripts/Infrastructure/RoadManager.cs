using System.Collections.Generic;
using UnityEngine;
using CivilSim.Core;
using CivilSim.Grid;

namespace CivilSim.Infrastructure
{
    /// <summary>
    /// 도로 배치·철거·시각화를 담당한다.
    /// GameManager.Instance.Roads 로 접근.
    ///
    /// 도로 비용: EconomyConfig.RoadCostPerTile (기본 500)
    /// 도로 시각: 1×0.1×1 큐브, 회색 (프리팹 없을 때 자동 생성)
    /// </summary>
    public class RoadManager : MonoBehaviour
    {
        // ── 인스펙터 ──────────────────────────────────────────
        [Header("도로 프리팹 (없으면 큐브 자동 생성)")]
        [SerializeField] private GameObject _roadPrefab;

        [Header("도로 비용 (타일당)")]
        [SerializeField] private int _roadCostPerTile = 500;

        // ── 내부 상태 ─────────────────────────────────────────
        private GridSystem  _grid;
        private Transform   _roadRoot;
        private Material    _roadMaterial;
        private readonly Dictionary<Vector2Int, GameObject> _roadObjects = new();

        public int Count => _roadObjects.Count;
        public int RoadCostPerTile => _roadCostPerTile;

        // ── Unity ────────────────────────────────────────────

        private void Awake()
        {
            _roadRoot = new GameObject("=== Roads ===").transform;

            // 도로 머티리얼 사전 생성
            var shader    = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _roadMaterial = new Material(shader) { color = new Color(0.35f, 0.35f, 0.35f) };
            _roadMaterial.enableInstancing = true;
        }

        private void Start()
        {
            _grid = GameManager.Instance.Grid;
        }

        private void OnDestroy()
        {
            if (_roadMaterial != null) Destroy(_roadMaterial);
        }

        // ── 공개 API ──────────────────────────────────────────

        /// <summary>단일 타일에 도로 배치. 성공 시 true.</summary>
        public bool TryPlaceRoad(Vector2Int pos)
        {
            if (_grid == null) return false;
            var cell = _grid.GetCell(pos);
            if (cell == null || cell.HasBuilding || cell.HasRoad) return false;

            _grid.PlaceRoad(pos);
            SpawnRoadVisual(pos);

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

            GameEventBus.Publish(new RoadRemovedEvent { GridPosition = pos });
            return true;
        }

        /// <summary>
        /// 시작점~끝점 사이의 L자 라인에 도로 배치.
        /// 자금이 부족하면 가능한 만큼만 배치.
        /// 실제 배치된 개수 반환.
        /// </summary>
        public int TryPlaceRoadLine(Vector2Int start, Vector2Int end)
        {
            var cells = GetLineCells(start, end);
            int placed = 0;

            foreach (var cell in cells)
                if (TryPlaceRoad(cell)) placed++;

            return placed;
        }

        /// <summary>시작점~끝점 라인의 도로 일괄 철거.</summary>
        public int TryRemoveRoadLine(Vector2Int start, Vector2Int end)
        {
            var cells = GetLineCells(start, end);
            int removed = 0;

            foreach (var cell in cells)
                if (TryRemoveRoad(cell)) removed++;

            return removed;
        }

        /// <summary>시작~끝 사이 L자 경로의 셀 목록 (가로 우선).</summary>
        public static List<Vector2Int> GetLineCells(Vector2Int from, Vector2Int to)
        {
            var cells = new List<Vector2Int>();

            int dx = to.x > from.x ? 1 : -1;
            int dz = to.y > from.y ? 1 : -1;

            // 가로 이동
            for (int x = from.x; x != to.x; x += dx)
                cells.Add(new Vector2Int(x, from.y));

            // 세로 이동
            for (int z = from.y; z != to.y; z += dz)
                cells.Add(new Vector2Int(to.x, z));

            cells.Add(to); // 끝점
            return cells;
        }

        // ── 내부 ─────────────────────────────────────────────

        private void SpawnRoadVisual(Vector2Int pos)
        {
            if (_roadObjects.ContainsKey(pos)) return;

            Vector3 worldPos = _grid.GridToWorld(pos) + new Vector3(0.5f, 0.05f, 0.5f);
            GameObject go;

            if (_roadPrefab != null)
            {
                go = Instantiate(_roadPrefab, worldPos, Quaternion.identity, _roadRoot);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.SetParent(_roadRoot);
                go.transform.position   = worldPos;
                go.transform.localScale = new Vector3(1f, 0.1f, 1f);
                go.GetComponent<Renderer>().sharedMaterial = _roadMaterial;
                Destroy(go.GetComponent<BoxCollider>());
            }

            go.name     = $"Road_{pos.x}_{pos.y}";
            go.isStatic = true;
            _roadObjects[pos] = go;
        }

        private void RemoveRoadVisual(Vector2Int pos)
        {
            if (!_roadObjects.TryGetValue(pos, out var go)) return;
            if (go != null) Destroy(go);
            _roadObjects.Remove(pos);
        }
    }
}

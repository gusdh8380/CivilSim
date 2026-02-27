using System.Collections.Generic;
using UnityEngine;
using CivilSim.Core;
using CivilSim.Grid;

namespace CivilSim.Buildings
{
    /// <summary>
    /// 건물 배치·철거·조회를 담당한다.
    /// GameManager.Instance.Buildings 로 접근.
    /// </summary>
    public class BuildingManager : MonoBehaviour
    {
        // ── 인스펙터 ─────────────────────────────────────────
        [Header("씬 오브젝트 컨테이너")]
        [SerializeField, Tooltip("배치된 건물 오브젝트의 부모 Transform (없으면 자동 생성)")]
        private Transform _buildingRoot;

        // ── 내부 상태 ─────────────────────────────────────────
        private GridSystem _grid;
        private readonly Dictionary<int, BuildingInstance> _buildings = new();
        private int _nextId;

        // ── Unity ────────────────────────────────────────────

        private void Awake()
        {
            if (_buildingRoot == null)
            {
                _buildingRoot = new GameObject("=== Buildings ===").transform;
            }
        }

        private void Start()
        {
            _grid = GameManager.Instance.Grid;
        }

        // ── 배치 ─────────────────────────────────────────────

        /// <summary>
        /// pos를 원점으로 data 크기만큼 건물을 배치한다.
        /// 배치 성공 시 true 반환.
        /// </summary>
        public bool TryPlace(Vector2Int pos, BuildingData data)
        {
            if (data == null) return false;
            if (!_grid.CanBuildArea(pos, data.SizeX, data.SizeZ)) return false;

            // TODO Phase 4: Economy.TrySpend(data.BuildCost)

            // 그리드 셀 점유
            for (int dx = 0; dx < data.SizeX; dx++)
                for (int dz = 0; dz < data.SizeZ; dz++)
                    _grid.PlaceBuilding(new Vector2Int(pos.x + dx, pos.y + dz), _nextId);

            // 프리팹 인스턴스화 (없으면 임시 큐브)
            Vector3 worldPos  = _grid.GridToWorld(pos);
            GameObject go     = SpawnBuilding(data, worldPos);
            var instance      = go.GetComponent<BuildingInstance>()
                             ?? go.AddComponent<BuildingInstance>();
            instance.Initialize(_nextId, data, pos);

            _buildings[_nextId] = instance;

            GameEventBus.Publish(new BuildingPlacedEvent
            {
                GridPosition   = pos,
                BuildingDataId = _nextId
            });

            Debug.Log($"[BuildingManager] 배치: {data.BuildingName} @ {pos}  id={_nextId}");
            _nextId++;
            return true;
        }

        // ── 철거 ─────────────────────────────────────────────

        /// <summary>pos에 있는 건물을 철거한다. 성공 시 true 반환.</summary>
        public bool TryRemove(Vector2Int pos)
        {
            var cell = _grid.GetCell(pos);
            if (cell == null || !cell.HasBuilding) return false;

            int id = cell.BuildingId;
            if (!_buildings.TryGetValue(id, out var instance)) return false;

            var data   = instance.Data;
            var origin = instance.GridOrigin;

            // 그리드 셀 해제
            for (int dx = 0; dx < data.SizeX; dx++)
                for (int dz = 0; dz < data.SizeZ; dz++)
                    _grid.RemoveBuilding(new Vector2Int(origin.x + dx, origin.y + dz));

            GameEventBus.Publish(new BuildingRemovedEvent
            {
                GridPosition   = origin,
                BuildingDataId = id
            });

            Debug.Log($"[BuildingManager] 철거: {data.BuildingName} @ {origin}  id={id}");
            _buildings.Remove(id);
            Destroy(instance.gameObject);
            return true;
        }

        // ── 조회 ─────────────────────────────────────────────

        public BuildingInstance GetBuilding(int id)
            => _buildings.TryGetValue(id, out var b) ? b : null;

        public int Count => _buildings.Count;

        public IReadOnlyDictionary<int, BuildingInstance> GetAll() => _buildings;

        // ── 내부 ─────────────────────────────────────────────

        private GameObject SpawnBuilding(BuildingData data, Vector3 worldPos)
        {
            if (data.Prefab != null)
            {
                return Instantiate(data.Prefab, worldPos, Quaternion.identity, _buildingRoot);
            }

            // 프리팹 없을 때 — ProBuilder로 교체 전까지 큐브 사용
            var go  = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(_buildingRoot);
            go.transform.position   = worldPos + new Vector3(data.SizeX * 0.5f - 0.5f, 0.5f, data.SizeZ * 0.5f - 0.5f);
            go.transform.localScale = new Vector3(data.SizeX * 0.9f, 1f, data.SizeZ * 0.9f); // 살짝 여백

            // 카테고리별 색상 구분
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = GetCategoryColor(data.Category);

            return go;
        }

        private static Color GetCategoryColor(BuildingCategory cat) => cat switch
        {
            BuildingCategory.Residential    => new Color(0.4f, 0.8f, 0.4f), // 연두
            BuildingCategory.Commercial     => new Color(0.4f, 0.6f, 1.0f), // 파랑
            BuildingCategory.Industrial     => new Color(1.0f, 0.7f, 0.3f), // 주황
            BuildingCategory.Public         => new Color(1.0f, 1.0f, 0.4f), // 노랑
            BuildingCategory.Utility        => new Color(0.8f, 0.4f, 0.8f), // 보라
            BuildingCategory.Infrastructure => new Color(0.6f, 0.6f, 0.6f), // 회색
            _                               => Color.white
        };
    }
}

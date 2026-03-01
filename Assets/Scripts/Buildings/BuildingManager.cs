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

        [Header("건물 Y 오프셋 (지반 위에 배치)")]
        [Tooltip("지반 타일(두께 ~0.2) 위에 건물이 놓이도록 조정. Pandazole 기본: 0.2")]
        [SerializeField] private float _buildingYOffset = 0.2f;

        // ── 내부 상태 ─────────────────────────────────────────
        private GridSystem _grid;
        private readonly Dictionary<int, BuildingInstance> _buildings = new();
        private int _nextId;

        // ── Unity ────────────────────────────────────────────

        private void Awake()
        {
            if (_buildingRoot == null)
                _buildingRoot = new GameObject("=== Buildings ===").transform;
        }

        private void Start()
        {
            _grid = GameManager.Instance.Grid;
            if (_grid == null) _grid = FindObjectOfType<GridSystem>();
        }

        // ── 배치 ─────────────────────────────────────────────

        /// <summary>
        /// pos를 원점으로 data 크기만큼 건물을 배치한다. rotation(0~3) 적용.
        /// 배치 성공 시 true 반환.
        /// </summary>
        public bool TryPlace(Vector2Int pos, BuildingData data, int rotation = 0)
        {
            if (data == null || _grid == null) return false;

            int sizeX = rotation % 2 == 0 ? data.SizeX : data.SizeZ;
            int sizeZ = rotation % 2 == 0 ? data.SizeZ : data.SizeX;

            if (!_grid.CanBuildArea(pos, sizeX, sizeZ)) return false;

            // 그리드 셀 점유
            for (int dx = 0; dx < sizeX; dx++)
                for (int dz = 0; dz < sizeZ; dz++)
                    _grid.PlaceBuilding(new Vector2Int(pos.x + dx, pos.y + dz), _nextId);

            // 월드 좌표 + 회전 고려한 멀티셀 중심 오프셋
            Vector3 worldPos = _grid.GridToWorld(pos);
            worldPos.y += _buildingYOffset;
            Vector3 center = worldPos + new Vector3(
                (sizeX - 1) * _grid.CellSize * 0.5f,
                0f,
                (sizeZ - 1) * _grid.CellSize * 0.5f);

            GameObject go = SpawnBuilding(data, center, sizeX, sizeZ, rotation);

            var instance = go.GetComponent<BuildingInstance>()
                        ?? go.AddComponent<BuildingInstance>();
            instance.Initialize(_nextId, data, pos, rotation);

            _buildings[_nextId] = instance;

            GameEventBus.Publish(new BuildingPlacedEvent
            {
                GridPosition   = pos,
                BuildingDataId = _nextId
            });

            Debug.Log($"[BuildingManager] 배치: {data.BuildingName} @ {pos} R{rotation * 90}° sz={sizeX}x{sizeZ}  id={_nextId}");
            _nextId++;
            return true;
        }

        // ── 철거 ─────────────────────────────────────────────

        /// <summary>pos에 있는 건물을 철거한다. 성공 시 true 반환.</summary>
        public bool TryRemove(Vector2Int pos)
        {
            var cell = _grid?.GetCell(pos);
            if (cell == null || !cell.HasBuilding) return false;

            int id = cell.BuildingId;
            if (!_buildings.TryGetValue(id, out var instance)) return false;

            var data   = instance.Data;
            var origin = instance.GridOrigin;
            var sz     = instance.EffectiveSize;   // 회전 반영된 크기

            // 그리드 셀 해제 (RemoveBuilding → State = Foundation 으로 복원)
            for (int dx = 0; dx < sz.x; dx++)
                for (int dz = 0; dz < sz.y; dz++)
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

        private GameObject SpawnBuilding(BuildingData data, Vector3 center, int sizeX, int sizeZ, int rotation)
        {
            if (data.Prefab != null)
            {
                return Instantiate(
                    data.Prefab,
                    center,
                    Quaternion.Euler(0f, rotation * 90f, 0f),
                    _buildingRoot);
            }

            // 프리팹 없을 때 — 큐브 fallback
            float cs = _grid != null ? _grid.CellSize : 10f;
            var go   = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(_buildingRoot);
            go.transform.position   = center + new Vector3(0f, cs * 0.5f, 0f);
            go.transform.rotation   = Quaternion.Euler(0f, rotation * 90f, 0f);
            go.transform.localScale = new Vector3(sizeX * cs * 0.8f, cs, sizeZ * cs * 0.8f);

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = GetCategoryColor(data.Category);

            return go;
        }

        private static Color GetCategoryColor(BuildingCategory cat) => cat switch
        {
            BuildingCategory.Residential    => new Color(0.4f, 0.8f, 0.4f),
            BuildingCategory.Commercial     => new Color(0.4f, 0.6f, 1.0f),
            BuildingCategory.Industrial     => new Color(1.0f, 0.7f, 0.3f),
            BuildingCategory.Public         => new Color(1.0f, 1.0f, 0.4f),
            BuildingCategory.Utility        => new Color(0.8f, 0.4f, 0.8f),
            BuildingCategory.Infrastructure => new Color(0.6f, 0.6f, 0.6f),
            _                               => Color.white
        };
    }
}

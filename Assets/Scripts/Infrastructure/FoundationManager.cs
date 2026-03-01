using System.Collections.Generic;
using UnityEngine;
using CivilSim.Core;
using CivilSim.Grid;

namespace CivilSim.Infrastructure
{
    /// <summary>
    /// 지반(Foundation) 타일 배치·철거·조회를 담당한다.
    /// 건물을 짓기 전 반드시 지반을 먼저 다져야 한다.
    /// 시각: Env_Road_Free.prefab (Pandazole 무료 도로 타일)
    /// GameManager.Instance.Foundation 으로 접근.
    /// </summary>
    public class FoundationManager : MonoBehaviour
    {
        // ── 인스펙터 ─────────────────────────────────────────
        [Header("지반 프리팹")]
        [Tooltip("Pandazole Env_Road_Free.prefab 을 할당한다.")]
        [SerializeField] private GameObject _foundationPrefab;

        [Header("씬 오브젝트 컨테이너")]
        [SerializeField] private Transform _foundationRoot;

        // ── 내부 상태 ─────────────────────────────────────────
        private GridSystem _grid;
        private readonly Dictionary<Vector2Int, GameObject> _placed = new();

        // ── Unity ────────────────────────────────────────────

        private void Awake()
        {
            if (_foundationRoot == null)
                _foundationRoot = new GameObject("=== Foundations ===").transform;
        }

        private void Start()
        {
            _grid = GameManager.Instance.Grid;
            if (_grid == null) _grid = FindObjectOfType<GridSystem>();
        }

        // ── 공개 API ──────────────────────────────────────────

        /// <summary>단일 셀에 지반을 설치한다. 성공 시 true 반환.</summary>
        public bool TryPlace(Vector2Int pos)
        {
            var cell = _grid?.GetCell(pos);
            if (cell == null || !cell.CanPlaceFoundation) return false;
            if (_placed.ContainsKey(pos)) return false;   // 이미 지반 있음

            _grid.PlaceFoundation(pos);

            Vector3 worldPos = _grid.GridToWorld(pos);
            worldPos.y = 0f;   // 지면 위에 평평하게

            GameObject go = SpawnFoundation(worldPos);
            go.name = $"[Foundation] ({pos.x},{pos.y})";
            _placed[pos] = go;

            Debug.Log($"[FoundationManager] 지반 설치: {pos}");
            return true;
        }

        /// <summary>단일 셀의 지반을 철거한다. 성공 시 true 반환.</summary>
        public bool TryRemove(Vector2Int pos)
        {
            var cell = _grid?.GetCell(pos);
            if (cell == null || cell.State != CellState.Foundation) return false;

            _grid.RemoveFoundation(pos);

            if (_placed.TryGetValue(pos, out var go))
            {
                Destroy(go);
                _placed.Remove(pos);
            }

            Debug.Log($"[FoundationManager] 지반 철거: {pos}");
            return true;
        }

        /// <summary>start ↔ end 직사각형 영역을 한 번에 지반으로 채운다.</summary>
        public void PlaceRect(Vector2Int start, Vector2Int end)
        {
            int minX = Mathf.Min(start.x, end.x);
            int maxX = Mathf.Max(start.x, end.x);
            int minZ = Mathf.Min(start.y, end.y);
            int maxZ = Mathf.Max(start.y, end.y);

            int placed = 0;
            for (int x = minX; x <= maxX; x++)
                for (int z = minZ; z <= maxZ; z++)
                    if (TryPlace(new Vector2Int(x, z))) placed++;

            if (placed > 0)
                Debug.Log($"[FoundationManager] 직사각형 지반 {placed}셀 완료 ({start} ~ {end})");
        }

        /// <summary>해당 셀에 지반 비주얼이 있는지 확인.</summary>
        public bool HasFoundation(Vector2Int pos) => _placed.ContainsKey(pos);

        public int Count => _placed.Count;

        // ── 내부 ─────────────────────────────────────────────

        private GameObject SpawnFoundation(Vector3 worldPos)
        {
            if (_foundationPrefab != null)
                return Instantiate(_foundationPrefab, worldPos, Quaternion.identity, _foundationRoot);

            // 프리팹 없을 때 — 회색 납작 큐브
            float cs  = _grid != null ? _grid.CellSize : 10f;
            var   go  = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(_foundationRoot);
            go.transform.position   = worldPos + new Vector3(0f, 0.1f, 0f);   // 중심 Y
            go.transform.localScale = new Vector3(cs * 0.99f, 0.2f, cs * 0.99f);

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = new Color(0.55f, 0.55f, 0.55f);

            return go;
        }
    }
}

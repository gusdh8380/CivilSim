using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using CivilSim.Core;
using CivilSim.Grid;

namespace CivilSim.Infrastructure
{
    public enum RoadBuilderMode { None, Building, Removing }

    /// <summary>
    /// 도로 배치 입력 및 드래그 미리보기를 담당한다.
    ///
    /// 조작:
    ///   LMB 클릭+드래그 : 도로 라인 배치 / 철거
    ///   RMB / Escape    : 모드 취소
    ///   F               : 도로 배치 모드 토글 (임시 단축키)
    ///
    /// 도로는 가로 우선 L자 경로로 배치된다.
    /// </summary>
    public class RoadBuilder : MonoBehaviour
    {
        // ── 인스펙터 ──────────────────────────────────────────
        [Header("미리보기 머티리얼 (미할당 시 자동 생성)")]
        [SerializeField] private Material _previewValidMaterial;
        [SerializeField] private Material _previewRemoveMaterial;

        // ── 내부 상태 ─────────────────────────────────────────
        private GridSystem   _grid;
        private RoadManager  _roads;
        private UnityEngine.Camera _cam;

        private bool       _isDragging;
        private Vector2Int _dragStart;
        private Vector2Int _lastHoverPos = new(-999, -999);

        private readonly List<GameObject> _previewTiles = new();

        public RoadBuilderMode Mode { get; private set; } = RoadBuilderMode.None;

        // ── Unity ────────────────────────────────────────────

        private void Awake()
        {
            _cam = UnityEngine.Camera.main;
            EnsureMaterials();
        }

        private void Start()
        {
            _grid  = GameManager.Instance.Grid;
            _roads = GameManager.Instance.Roads;

            // Inspector 미연결 시 씬에서 자동 탐색 (폴백)
            if (_grid  == null) _grid  = FindObjectOfType<GridSystem>();
            if (_roads == null) _roads = FindObjectOfType<RoadManager>();

            if (_grid  == null) Debug.LogError("[RoadBuilder] GridSystem을 찾을 수 없습니다. GameManager에 할당해주세요.");
            if (_roads == null) Debug.LogError("[RoadBuilder] RoadManager를 찾을 수 없습니다. GameManager에 할당해주세요.");
        }

        private void Update()
        {
            var kb    = Keyboard.current;
            var mouse = Mouse.current;

            // F키: 도로 배치 모드 토글
            if (kb != null && kb.fKey.wasPressedThisFrame)
            {
                if (Mode == RoadBuilderMode.Building) Cancel();
                else StartBuilding();
                return;
            }

            if (Mode == RoadBuilderMode.None) return;

            // UI 위에 있으면 클릭 무시
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            // 취소
            if ((kb  != null && kb.escapeKey.wasPressedThisFrame) ||
                (mouse != null && mouse.rightButton.wasPressedThisFrame))
            {
                Cancel();
                return;
            }

            Vector2Int gridPos = ScreenToGrid();
            bool posChanged    = gridPos != _lastHoverPos;
            if (posChanged) _lastHoverPos = gridPos;

            // 드래그 시작
            if (!overUI && mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                _isDragging = true;
                _dragStart  = gridPos;
            }

            // 드래그 중: 미리보기 갱신
            if (_isDragging && posChanged)
                UpdatePreview(RoadManager.GetLineCells(_dragStart, gridPos));

            // 드래그 종료: 실행
            if (_isDragging && mouse != null && mouse.leftButton.wasReleasedThisFrame)
            {
                _isDragging = false;
                ClearPreview();
                ExecuteAction(_dragStart, gridPos);
            }
        }

        // ── 공개 API ──────────────────────────────────────────

        public void StartBuilding()
        {
            Cancel();   // 자신의 상태 초기화
            // 다른 모드(건물·지반) 취소 — 단축키 충돌 방지
            GameManager.Instance?.CancelAllModes();
            Mode = RoadBuilderMode.Building;
            Debug.Log("[RoadBuilder] 도로 배치 모드 시작");
        }

        public void StartRemoving()
        {
            Cancel();
            Mode = RoadBuilderMode.Removing;
            Debug.Log("[RoadBuilder] 도로 철거 모드 시작");
        }

        public void Cancel()
        {
            _isDragging = false;
            ClearPreview();
            Mode = RoadBuilderMode.None;
        }

        // ── 실행 ─────────────────────────────────────────────

        private void ExecuteAction(Vector2Int start, Vector2Int end)
        {
            if (_roads == null)
            {
                Debug.LogError("[RoadBuilder] RoadManager가 null입니다. GameManager Inspector에 RoadManager를 할당해주세요.");
                return;
            }

            if (Mode == RoadBuilderMode.Building)
            {
                // 자금 체크: 라인 셀 수 × 타일 비용
                var cells    = RoadManager.GetLineCells(start, end);
                int totalCost = cells.Count * _roads.RoadCostPerTile;

                var economy = GameManager.Instance.Economy;
                if (economy != null && !economy.TrySpend(totalCost))
                {
                    GameEventBus.Publish(new NotificationEvent
                    {
                        Message = $"자금 부족! 도로 건설에 ₩{totalCost:N0} 필요.",
                        Type    = NotificationType.Warning
                    });
                    return;
                }

                int placed = _roads.TryPlaceRoadLine(start, end);
                if (placed > 0)
                    Debug.Log($"[RoadBuilder] 도로 {placed}타일 배치 완료");
            }
            else if (Mode == RoadBuilderMode.Removing)
            {
                int removed = _roads.TryRemoveRoadLine(start, end);
                if (removed > 0)
                    Debug.Log($"[RoadBuilder] 도로 {removed}타일 철거 완료");
            }
        }

        // ── 미리보기 ──────────────────────────────────────────

        private void UpdatePreview(List<Vector2Int> cells)
        {
            Material mat = Mode == RoadBuilderMode.Building
                ? _previewValidMaterial
                : _previewRemoveMaterial;

            // 풀 확장
            while (_previewTiles.Count < cells.Count)
                _previewTiles.Add(CreatePreviewTile());

            // 위치 갱신 & 표시
            float cs = _grid != null ? _grid.CellSize : 10f;
            for (int i = 0; i < cells.Count; i++)
            {
                var tile = _previewTiles[i];
                tile.SetActive(true);
                // GridToWorld는 이미 셀 중심(XZ)을 반환 — 추가 XZ 오프셋 불필요
                tile.transform.position   = _grid.GridToWorld(cells[i]) + new Vector3(0f, cs * 0.12f, 0f);
                tile.transform.localScale = new Vector3(cs * 0.95f, cs * 0.08f, cs * 0.95f);
                tile.GetComponent<Renderer>().material = mat;
            }

            // 초과 타일 숨김
            for (int i = cells.Count; i < _previewTiles.Count; i++)
                _previewTiles[i].SetActive(false);
        }

        private void ClearPreview()
        {
            foreach (var t in _previewTiles)
                if (t != null) t.SetActive(false);
        }

        private GameObject CreatePreviewTile()
        {
            float cs = _grid != null ? _grid.CellSize : 10f;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.localScale = new Vector3(cs * 0.95f, cs * 0.08f, cs * 0.95f);
            go.name = "[RoadPreview]";

            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            go.hideFlags = HideFlags.HideInHierarchy;
            go.SetActive(false);
            return go;
        }

        // ── 유틸 ─────────────────────────────────────────────

        private Vector2Int ScreenToGrid()
        {
            if (_cam == null || Mouse.current == null) return Vector2Int.zero;
            Ray   ray   = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            var   plane = new Plane(Vector3.up, Vector3.zero);
            return plane.Raycast(ray, out float dist)
                ? _grid.WorldToGridClamped(ray.GetPoint(dist))
                : Vector2Int.zero;
        }

        private void EnsureMaterials()
        {
            if (_previewValidMaterial == null)
                _previewValidMaterial = CreateTransparentMat(new Color(0.2f, 0.6f, 1f, 0.55f));
            if (_previewRemoveMaterial == null)
                _previewRemoveMaterial = CreateTransparentMat(new Color(1f, 0.3f, 0.3f, 0.55f));
        }

        private static Material CreateTransparentMat(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat    = new Material(shader) { color = color, hideFlags = HideFlags.HideAndDontSave };
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend",   0f);
            mat.SetFloat("_ZWrite",  0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000;
            return mat;
        }

        private void OnDestroy()
        {
            foreach (var t in _previewTiles)
                if (t != null) Destroy(t);

            if (_previewValidMaterial  != null && _previewValidMaterial.hideFlags  == HideFlags.HideAndDontSave) Destroy(_previewValidMaterial);
            if (_previewRemoveMaterial != null && _previewRemoveMaterial.hideFlags == HideFlags.HideAndDontSave) Destroy(_previewRemoveMaterial);
        }
    }
}

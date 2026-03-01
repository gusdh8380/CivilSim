using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using CivilSim.Core;
using CivilSim.Grid;

namespace CivilSim.Infrastructure
{
    /// <summary>
    /// 지반 다지기 입력 핸들러.
    ///
    /// 조작:
    ///   G        : 지반 모드 토글
    ///   LMB 드래그 : 직사각형 영역 지반 설치 (press→drag→release)
    ///   RMB / Esc : 모드 취소
    ///
    /// 미리보기:
    ///   노란색 타일 = 설치 가능, 빨간색 타일 = 설치 불가
    /// </summary>
    public class FoundationBuilder : MonoBehaviour
    {
        // ── 내부 상태 ─────────────────────────────────────────
        private GridSystem         _grid;
        private FoundationManager  _foundation;
        private UnityEngine.Camera _cam;

        private bool       _isActive  = false;
        private bool       _isDragging = false;
        private Vector2Int _dragStart;
        private Vector2Int _lastHover   = new(-999, -999);
        private Vector2Int _lastDragEnd = new(-999, -999);

        // 미리보기 타일 목록
        private readonly List<GameObject> _previews = new();
        private Material _previewValidMat;
        private Material _previewInvalidMat;

        public bool IsActive => _isActive;

        // ── Unity ────────────────────────────────────────────

        private void Awake()
        {
            _cam = UnityEngine.Camera.main;
        }

        private void Start()
        {
            _grid = GameManager.Instance.Grid;
            if (_grid == null) _grid = FindObjectOfType<GridSystem>();

            _foundation = GameManager.Instance.Foundation;
            if (_foundation == null) _foundation = FindObjectOfType<FoundationManager>();

            EnsureMaterials();
        }

        private void Update()
        {
            var kb    = Keyboard.current;
            var mouse = Mouse.current;

            // ── G키: 모드 토글 ───────────────────────────────
            if (kb != null && kb.gKey.wasPressedThisFrame)
            {
                if (_isActive) Deactivate();
                else           Activate();
                return;
            }

            if (!_isActive) return;

            // ── 취소 ─────────────────────────────────────────
            if ((kb != null && kb.escapeKey.wasPressedThisFrame) ||
                (mouse != null && mouse.rightButton.wasPressedThisFrame))
            {
                Deactivate();
                return;
            }

            Vector2Int hover = ScreenToGrid();

            // ── LMB Press: 드래그 시작 ───────────────────────
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                _dragStart  = hover;
                _isDragging = true;
            }

            // ── 드래그 중: 직사각형 미리보기 갱신 ─────────────
            if (_isDragging)
            {
                if (hover != _lastDragEnd || hover != _lastHover)
                {
                    _lastHover   = hover;
                    _lastDragEnd = hover;
                    UpdateRectPreview(_dragStart, hover);
                }
            }
            else
            {
                // 단일 셀 호버 미리보기
                if (hover != _lastHover)
                {
                    _lastHover = hover;
                    UpdateSinglePreview(hover);
                }
            }

            // ── LMB Release: 실제 배치 ────────────────────────
            if (mouse != null && mouse.leftButton.wasReleasedThisFrame && _isDragging)
            {
                _foundation.PlaceRect(_dragStart, hover);
                _isDragging  = false;
                _lastHover   = new(-999, -999);
                _lastDragEnd = new(-999, -999);
                ClearPreviews();
            }
        }

        // ── 공개 API ──────────────────────────────────────────

        public void Activate()
        {
            _isActive = true;
            Debug.Log("[FoundationBuilder] 지반 다지기 모드 시작 — LMB 드래그로 영역 지정, G/Esc/RMB 로 종료");
        }

        public void Deactivate()
        {
            _isActive   = false;
            _isDragging = false;
            _lastHover   = new(-999, -999);
            _lastDragEnd = new(-999, -999);
            ClearPreviews();
        }

        // ── 미리보기 ─────────────────────────────────────────

        private void UpdateSinglePreview(Vector2Int pos)
        {
            SetPreviewCount(1);
            var cell = _grid?.GetCell(pos);
            bool ok  = cell != null && cell.CanPlaceFoundation;
            PositionTile(0, pos, ok);
        }

        private void UpdateRectPreview(Vector2Int start, Vector2Int end)
        {
            int minX  = Mathf.Min(start.x, end.x);
            int maxX  = Mathf.Max(start.x, end.x);
            int minZ  = Mathf.Min(start.y, end.y);
            int maxZ  = Mathf.Max(start.y, end.y);
            int count = (maxX - minX + 1) * (maxZ - minZ + 1);

            SetPreviewCount(count);

            int idx = 0;
            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    var cellPos = new Vector2Int(x, z);
                    var cell    = _grid?.GetCell(cellPos);
                    bool ok     = cell != null && cell.CanPlaceFoundation;
                    PositionTile(idx, cellPos, ok);
                    idx++;
                }
            }
        }

        private void SetPreviewCount(int needed)
        {
            float cs = _grid != null ? _grid.CellSize : 10f;

            while (_previews.Count < needed)
            {
                var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.name = "[FoundationPreview]";
                DestroyImmediate(tile.GetComponent<BoxCollider>());
                tile.transform.localScale = new Vector3(cs * 0.95f, cs * 0.04f, cs * 0.95f);
                _previews.Add(tile);
            }

            while (_previews.Count > needed)
            {
                int last = _previews.Count - 1;
                Destroy(_previews[last]);
                _previews.RemoveAt(last);
            }
        }

        private void PositionTile(int idx, Vector2Int cellPos, bool ok)
        {
            if (idx >= _previews.Count) return;

            Vector3 world = _grid != null
                ? _grid.GridToWorld(cellPos)
                : new Vector3(cellPos.x * 10f, 0f, cellPos.y * 10f);
            world.y = 0.05f;   // 지면 바로 위

            _previews[idx].transform.position = world;

            var rend = _previews[idx].GetComponent<Renderer>();
            if (rend != null)
                rend.material = ok ? _previewValidMat : _previewInvalidMat;
        }

        private void ClearPreviews()
        {
            foreach (var t in _previews)
                if (t != null) Destroy(t);
            _previews.Clear();
        }

        // ── 유틸 ─────────────────────────────────────────────

        private Vector2Int ScreenToGrid()
        {
            if (_cam == null) return Vector2Int.zero;
            var mouse = Mouse.current;
            if (mouse == null) return Vector2Int.zero;

            Ray ray   = _cam.ScreenPointToRay(mouse.position.ReadValue());
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float dist))
                return _grid.WorldToGridClamped(ray.GetPoint(dist));

            return Vector2Int.zero;
        }

        private void EnsureMaterials()
        {
            // 노란색 계열 = 지반 설치 가능; 빨간색 = 불가
            _previewValidMat   = CreateTransparentMat(new Color(0.9f, 0.8f, 0.1f, 0.6f));
            _previewInvalidMat = CreateTransparentMat(new Color(0.9f, 0.2f, 0.2f, 0.6f));
        }

        private static Material CreateTransparentMat(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                      ?? Shader.Find("Standard");

            var mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            mat.color = color;
            mat.SetFloat("_Surface", 1f);   // Transparent
            mat.SetFloat("_Blend",   0f);   // Alpha
            mat.SetFloat("_ZWrite",  0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000;
            return mat;
        }

        private void OnDestroy()
        {
            ClearPreviews();

            void DestroyIfDynamic(Material m)
            {
                if (m != null && m.hideFlags == HideFlags.HideAndDontSave) Destroy(m);
            }
            DestroyIfDynamic(_previewValidMat);
            DestroyIfDynamic(_previewInvalidMat);
        }
    }
}

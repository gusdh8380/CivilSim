using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using CivilSim.Core;
using CivilSim.Grid;

namespace CivilSim.Zones
{
    /// <summary>
    /// 구역 지정 입력 담당.
    ///
    /// 조작:
    ///   Z           : 구역 모드 토글
    ///   R / C / I   : 주거/상업/공업 타입 전환
    ///   X           : 구역 해제 모드(ZoneType.None)
    ///   LMB 드래그  : 직사각형 적용
    ///   RMB / Esc   : 모드 취소
    /// </summary>
    public class ZoneBuilder : MonoBehaviour
    {
        [Header("미리보기 머티리얼 (미할당 시 자동 생성)")]
        [SerializeField] private Material _residentialPreviewMaterial;
        [SerializeField] private Material _commercialPreviewMaterial;
        [SerializeField] private Material _industrialPreviewMaterial;
        [SerializeField] private Material _clearPreviewMaterial;
        [SerializeField] private Material _invalidPreviewMaterial;

        private GridSystem _grid;
        private ZoneManager _zoneManager;
        private UnityEngine.Camera _cam;

        private bool _isActive;
        private bool _isDragging;
        private Vector2Int _dragStart;
        private Vector2Int _lastHover = new(-999, -999);
        private Vector2Int _lastDragEnd = new(-999, -999);

        private readonly List<GameObject> _previewTiles = new();

        public bool IsActive => _isActive;
        public ZoneType CurrentZoneType { get; private set; } = ZoneType.Residential;

        private void Awake()
        {
            _cam = UnityEngine.Camera.main;
            EnsureMaterials();
        }

        private void Start()
        {
            _grid = GameManager.Instance?.Grid;
            if (_grid == null) _grid = FindFirstObjectByType<GridSystem>();

            _zoneManager = GameManager.Instance?.Zone;
            if (_zoneManager == null) _zoneManager = FindFirstObjectByType<ZoneManager>();

            if (_grid == null) Debug.LogError("[ZoneBuilder] GridSystem을 찾을 수 없습니다.");
            if (_zoneManager == null) Debug.LogError("[ZoneBuilder] ZoneManager를 찾을 수 없습니다.");
        }

        private void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;

            if (kb != null && kb.zKey.wasPressedThisFrame)
            {
                if (_isActive) Deactivate();
                else Activate();
                return;
            }

            if (!_isActive) return;

            HandleZoneTypeHotkeys(kb);

            if ((kb != null && kb.escapeKey.wasPressedThisFrame) ||
                (mouse != null && mouse.rightButton.wasPressedThisFrame))
            {
                Deactivate();
                return;
            }

            if (_grid == null || _zoneManager == null || _cam == null || mouse == null)
                return;

            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            Vector2Int hover = ScreenToGrid();

            if (!overUI && mouse.leftButton.wasPressedThisFrame)
            {
                _isDragging = true;
                _dragStart = hover;
            }

            if (_isDragging)
            {
                if (hover != _lastDragEnd || hover != _lastHover)
                {
                    _lastHover = hover;
                    _lastDragEnd = hover;
                    UpdateRectPreview(_dragStart, hover);
                }
            }
            else if (hover != _lastHover)
            {
                _lastHover = hover;
                UpdateRectPreview(hover, hover);
            }

            if (_isDragging && mouse.leftButton.wasReleasedThisFrame)
            {
                int changed = _zoneManager.ApplyRect(_dragStart, hover, CurrentZoneType);
                if (changed == 0)
                {
                    GameEventBus.Publish(new NotificationEvent
                    {
                        Message = "구역 지정 가능한 셀이 없습니다.",
                        Type = NotificationType.Warning
                    });
                }

                _isDragging = false;
                _lastHover = new(-999, -999);
                _lastDragEnd = new(-999, -999);
                ClearPreview();
            }
        }

        public void Activate()
        {
            Deactivate();
            GameManager.Instance?.CancelAllModes();
            _isActive = true;
            Debug.Log($"[ZoneBuilder] 구역 지정 모드 시작 ({CurrentZoneType})");
        }

        public void Deactivate()
        {
            _isActive = false;
            _isDragging = false;
            _lastHover = new(-999, -999);
            _lastDragEnd = new(-999, -999);
            ClearPreview();
        }

        private void HandleZoneTypeHotkeys(Keyboard kb)
        {
            if (kb == null) return;

            ZoneType next = CurrentZoneType;
            if (kb.rKey.wasPressedThisFrame) next = ZoneType.Residential;
            else if (kb.cKey.wasPressedThisFrame) next = ZoneType.Commercial;
            else if (kb.iKey.wasPressedThisFrame) next = ZoneType.Industrial;
            else if (kb.xKey.wasPressedThisFrame) next = ZoneType.None;

            if (next == CurrentZoneType) return;
            CurrentZoneType = next;

            GameEventBus.Publish(new NotificationEvent
            {
                Message = $"구역 타입: {GetZoneLabel(CurrentZoneType)}",
                Type = NotificationType.Info
            });
        }

        private void UpdateRectPreview(Vector2Int start, Vector2Int end)
        {
            int minX = Mathf.Min(start.x, end.x);
            int maxX = Mathf.Max(start.x, end.x);
            int minY = Mathf.Min(start.y, end.y);
            int maxY = Mathf.Max(start.y, end.y);
            int count = (maxX - minX + 1) * (maxY - minY + 1);

            EnsurePreviewCount(count);

            int idx = 0;
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    var pos = new Vector2Int(x, y);
                    var cell = _grid.GetCell(pos);
                    bool valid = cell != null
                        && cell.State != CellState.Road
                        && cell.State != CellState.Building;
                    PositionPreview(idx++, pos, valid);
                }
            }
        }

        private void EnsurePreviewCount(int needed)
        {
            while (_previewTiles.Count < needed)
                _previewTiles.Add(CreatePreviewTile());

            for (int i = 0; i < _previewTiles.Count; i++)
                _previewTiles[i].SetActive(i < needed);
        }

        private void PositionPreview(int index, Vector2Int pos, bool valid)
        {
            if (index < 0 || index >= _previewTiles.Count) return;
            if (_grid == null) return;

            var tile = _previewTiles[index];
            tile.SetActive(true);

            float cs = _grid.CellSize;
            tile.transform.position = _grid.GridToWorld(pos) + new Vector3(0f, cs * 0.11f, 0f);
            tile.transform.localScale = new Vector3(cs * 0.95f, cs * 0.04f, cs * 0.95f);

            var renderer = tile.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material = valid ? GetPreviewMaterial(CurrentZoneType) : _invalidPreviewMaterial;
        }

        private void ClearPreview()
        {
            foreach (var tile in _previewTiles)
                if (tile != null) tile.SetActive(false);
        }

        private GameObject CreatePreviewTile()
        {
            float cs = _grid != null ? _grid.CellSize : 10f;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.localScale = new Vector3(cs * 0.95f, cs * 0.04f, cs * 0.95f);
            go.name = "[ZonePreview]";

            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            go.hideFlags = HideFlags.HideInHierarchy;
            go.SetActive(false);
            return go;
        }

        private Vector2Int ScreenToGrid()
        {
            Ray ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            var plane = new Plane(Vector3.up, Vector3.zero);
            return plane.Raycast(ray, out float dist)
                ? _grid.WorldToGridClamped(ray.GetPoint(dist))
                : Vector2Int.zero;
        }

        private void EnsureMaterials()
        {
            if (_residentialPreviewMaterial == null)
                _residentialPreviewMaterial = CreateTransparentMat(new Color(0.2f, 0.8f, 0.35f, 0.55f));
            if (_commercialPreviewMaterial == null)
                _commercialPreviewMaterial = CreateTransparentMat(new Color(0.2f, 0.5f, 1f, 0.55f));
            if (_industrialPreviewMaterial == null)
                _industrialPreviewMaterial = CreateTransparentMat(new Color(1f, 0.65f, 0.2f, 0.55f));
            if (_clearPreviewMaterial == null)
                _clearPreviewMaterial = CreateTransparentMat(new Color(0.75f, 0.75f, 0.75f, 0.45f));
            if (_invalidPreviewMaterial == null)
                _invalidPreviewMaterial = CreateTransparentMat(new Color(0.9f, 0.2f, 0.2f, 0.6f));
        }

        private Material GetPreviewMaterial(ZoneType zoneType)
        {
            return zoneType switch
            {
                ZoneType.Residential => _residentialPreviewMaterial,
                ZoneType.Commercial => _commercialPreviewMaterial,
                ZoneType.Industrial => _industrialPreviewMaterial,
                _ => _clearPreviewMaterial
            };
        }

        private static string GetZoneLabel(ZoneType zoneType)
        {
            return zoneType switch
            {
                ZoneType.Residential => "주거 (R)",
                ZoneType.Commercial => "상업 (C)",
                ZoneType.Industrial => "공업 (I)",
                _ => "해제 (X)"
            };
        }

        private static Material CreateTransparentMat(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader) { color = color, hideFlags = HideFlags.HideAndDontSave };
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000;
            return mat;
        }

        private void OnDestroy()
        {
            foreach (var tile in _previewTiles)
                if (tile != null) Destroy(tile);

            DestroyIfDynamic(_residentialPreviewMaterial);
            DestroyIfDynamic(_commercialPreviewMaterial);
            DestroyIfDynamic(_industrialPreviewMaterial);
            DestroyIfDynamic(_clearPreviewMaterial);
            DestroyIfDynamic(_invalidPreviewMaterial);
        }

        private static void DestroyIfDynamic(Material mat)
        {
            if (mat != null && mat.hideFlags == HideFlags.HideAndDontSave)
                Destroy(mat);
        }
    }
}

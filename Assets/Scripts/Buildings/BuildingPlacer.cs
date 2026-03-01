using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using CivilSim.Core;
using CivilSim.Grid;

namespace CivilSim.Buildings
{
    public enum PlacerMode { None, Placing, Removing }

    /// <summary>
    /// 건물 배치 모드의 입력 및 고스트 미리보기를 담당한다.
    ///
    /// 조작:
    ///   LMB      : 배치 / 철거 실행
    ///   RMB      : 모드 취소
    ///   R        : 건물 90° 회전
    ///   Escape   : 모드 취소
    /// </summary>
    public class BuildingPlacer : MonoBehaviour
    {
        // ── 인스펙터 ──────────────────────────────────────────
        [Header("Ghost Materials (미할당 시 자동 생성)")]
        [SerializeField] private Material _validMaterial;
        [SerializeField] private Material _invalidMaterial;

        [Header("고스트 Y 오프셋 (지반 위에 표시)")]
        [Tooltip("BuildingManager._buildingYOffset 과 맞춰야 한다. Pandazole 기본: 0.2")]
        [SerializeField] private float _ghostYOffset = 0.2f;

        // ── 내부 상태 ─────────────────────────────────────────
        private GridSystem         _grid;
        private BuildingManager    _manager;
        private UnityEngine.Camera _cam;

        private BuildingData _selectedData;
        private GameObject   _ghost;

        // 셀 지시자 (배치 예정 각 셀마다 얇은 타일)
        private readonly List<GameObject> _cellIndicators = new();
        private Material _cellValidMat;
        private Material _cellInvalidMat;

        private Vector2Int _lastGridPos = new(-999, -999);
        private bool       _isValid;
        private int        _rotation;   // 0,1,2,3 → 0°,90°,180°,270°
        private float      _lastWarningTime = -10f;   // 경고 쿨다운용
        private const float WarningCooldown = 2f;

        public PlacerMode Mode { get; private set; } = PlacerMode.None;

        // ── Unity ────────────────────────────────────────────

        private void Awake()
        {
            _cam = UnityEngine.Camera.main;
        }

        private void Start()
        {
            _grid    = GameManager.Instance.Grid;
            _manager = GameManager.Instance.Buildings;

            EnsureMaterials();
        }

        private void Update()
        {
            if (Mode == PlacerMode.None) return;

            var kb    = Keyboard.current;
            var mouse = Mouse.current;

            // ── 취소 ────────────────────────────────────────
            if ((kb != null && kb.escapeKey.wasPressedThisFrame) ||
                (mouse != null && mouse.rightButton.wasPressedThisFrame))
            {
                Cancel();
                return;
            }

            // ── 회전 (Placing 모드에서만) ────────────────────
            if (Mode == PlacerMode.Placing && kb != null && kb.rKey.wasPressedThisFrame)
                Rotate();

            // ── 고스트 위치 갱신 ─────────────────────────────
            Vector2Int gridPos = ScreenToGrid();
            bool posChanged    = gridPos != _lastGridPos;

            if (posChanged)
            {
                _lastGridPos = gridPos;
                UpdateGhost(gridPos);
            }

            // ── 클릭 실행 ────────────────────────────────────
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                if (Mode == PlacerMode.Placing)
                {
                    if (_isValid)
                        ExecutePlace(gridPos);
                    else
                        NotifyPlacementFail(gridPos);   // ← 배치 불가 이유 안내
                }
                else if (Mode == PlacerMode.Removing)
                    ExecuteRemove(gridPos);
            }
        }

        // ── 공개 API ──────────────────────────────────────────

        public void StartPlacing(BuildingData data)
        {
            if (data == null) return;
            Cancel();   // 자신의 상태 초기화
            // 다른 모드(도로·지반) 취소 — 단축키 충돌 방지
            GameManager.Instance?.CancelAllModes();

            _selectedData = data;
            _rotation     = 0;
            Mode          = PlacerMode.Placing;
            CreateGhost();
        }

        public void StartRemoving()
        {
            Cancel();
            Mode = PlacerMode.Removing;
        }

        public void Cancel()
        {
            DestroyGhost();
            ClearCellIndicators();
            _selectedData = null;
            _rotation     = 0;
            Mode          = PlacerMode.None;
        }

        // ── 실행 ─────────────────────────────────────────────

        private void ExecutePlace(Vector2Int pos)
        {
            int sizeX = _rotation % 2 == 0 ? _selectedData.SizeX : _selectedData.SizeZ;
            int sizeZ = _rotation % 2 == 0 ? _selectedData.SizeZ : _selectedData.SizeX;

            if (!_grid.CanBuildArea(pos, sizeX, sizeZ)) return;

            // 자금 체크 및 차감
            var economy = GameManager.Instance.Economy;
            if (economy != null && !economy.TrySpend(_selectedData.BuildCost))
            {
                GameEventBus.Publish(new NotificationEvent
                {
                    Message = $"자금 부족! '{_selectedData.BuildingName}' 건설에 ₩{_selectedData.BuildCost:N0} 필요.",
                    Type    = NotificationType.Warning,
                });
                return;
            }

            // ★ 회전값을 BuildingManager에 전달
            _manager.TryPlace(pos, _selectedData, _rotation);
            // 연속 배치 유지 (RMB 또는 Escape로 취소)
        }

        private void ExecuteRemove(Vector2Int pos)
        {
            _manager.TryRemove(pos);
        }

        /// <summary>
        /// 배치 불가 원인을 분석해 플레이어에게 경고 알림을 보낸다.
        /// 2초 쿨다운으로 스팸 방지.
        /// </summary>
        private void NotifyPlacementFail(Vector2Int pos)
        {
            if (Time.time - _lastWarningTime < WarningCooldown) return;
            _lastWarningTime = Time.time;

            if (_selectedData == null || _grid == null) return;

            int sizeX = _rotation % 2 == 0 ? _selectedData.SizeX : _selectedData.SizeZ;
            int sizeZ = _rotation % 2 == 0 ? _selectedData.SizeZ : _selectedData.SizeX;

            bool needsFoundation = false;
            bool alreadyOccupied = false;

            for (int dx = 0; dx < sizeX; dx++)
            for (int dz = 0; dz < sizeZ; dz++)
            {
                var cell = _grid.GetCell(new Vector2Int(pos.x + dx, pos.y + dz));
                if (cell == null) continue;
                if (cell.IsEmpty || cell.State == CellState.Zone) needsFoundation = true;
                else if (cell.HasBuilding || cell.HasRoad)        alreadyOccupied = true;
            }

            string msg = needsFoundation
                ? $"'{_selectedData.BuildingName}' 을(를) 짓기 전에 지반을 먼저 다지세요! (G 키)"
                : alreadyOccupied
                ? "이미 사용 중인 셀이 있어 배치할 수 없습니다."
                : "이 위치에는 배치할 수 없습니다.";

            GameEventBus.Publish(new NotificationEvent { Message = msg, Type = NotificationType.Warning });
        }

        // ── 고스트 ───────────────────────────────────────────

        private void CreateGhost()
        {
            DestroyGhost();
            if (_selectedData == null) return;

            if (_selectedData.Prefab != null)
            {
                _ghost = Instantiate(_selectedData.Prefab);
                // 물리/충돌 비활성화
                foreach (var col in _ghost.GetComponentsInChildren<Collider>())
                    col.enabled = false;
            }
            else
            {
                _ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
                DestroyImmediate(_ghost.GetComponent<BoxCollider>());
                int   sizeX = _rotation % 2 == 0 ? _selectedData.SizeX : _selectedData.SizeZ;
                int   sizeZ = _rotation % 2 == 0 ? _selectedData.SizeZ : _selectedData.SizeX;
                float cs    = _grid != null ? _grid.CellSize : 10f;
                // ★ CellSize 반영 (이전: sizeX * 0.9f → sizeX * cs * 0.9f)
                _ghost.transform.localScale = new Vector3(sizeX * cs * 0.9f, cs, sizeZ * cs * 0.9f);
            }

            _ghost.name = $"[Ghost] {_selectedData.BuildingName}";
            ApplyGhostRotation();
            SetGhostMaterial(_validMaterial);
        }

        private void DestroyGhost()
        {
            if (_ghost != null)
            {
                Destroy(_ghost);
                _ghost = null;
            }
        }

        private void UpdateGhost(Vector2Int gridPos)
        {
            if (Mode != PlacerMode.Placing) return;

            int   sizeX = _rotation % 2 == 0 ? _selectedData.SizeX : _selectedData.SizeZ;
            int   sizeZ = _rotation % 2 == 0 ? _selectedData.SizeZ : _selectedData.SizeX;
            float cs    = _grid != null ? _grid.CellSize : 10f;

            _isValid = _grid.CanBuildArea(gridPos, sizeX, sizeZ);

            if (_ghost != null)
            {
                Vector3 worldPos = _grid.GridToWorld(gridPos);
                // ★ 지반 오프셋 + 멀티셀 중심 보정
                worldPos.y += _ghostYOffset;
                _ghost.transform.position = worldPos + new Vector3(
                    (sizeX - 1) * cs * 0.5f,
                    0f,
                    (sizeZ - 1) * cs * 0.5f);

                SetGhostMaterial(_isValid ? _validMaterial : _invalidMaterial);
            }

            UpdateCellIndicators(gridPos, sizeX, sizeZ);
        }

        private void Rotate()
        {
            _rotation = (_rotation + 1) % 4;
            if (_ghost != null)
            {
                ApplyGhostRotation();
                // ★ 큐브 fallback 스케일 교체 (CellSize 반영)
                if (_selectedData.Prefab == null)
                {
                    int   sizeX = _rotation % 2 == 0 ? _selectedData.SizeX : _selectedData.SizeZ;
                    int   sizeZ = _rotation % 2 == 0 ? _selectedData.SizeZ : _selectedData.SizeX;
                    float cs    = _grid != null ? _grid.CellSize : 10f;
                    _ghost.transform.localScale = new Vector3(sizeX * cs * 0.9f, cs, sizeZ * cs * 0.9f);
                }
                UpdateGhost(_lastGridPos);
            }
        }

        private void ApplyGhostRotation()
        {
            if (_ghost != null)
                _ghost.transform.rotation = Quaternion.Euler(0f, _rotation * 90f, 0f);
        }

        private void SetGhostMaterial(Material mat)
        {
            if (_ghost == null || mat == null) return;
            foreach (var r in _ghost.GetComponentsInChildren<Renderer>())
                r.material = mat;
        }

        // ── 셀 지시자 ─────────────────────────────────────────

        /// <summary>
        /// 배치 예정 셀마다 얇은 오버레이 타일을 생성·갱신한다.
        /// 셀별로 CanBuild(Foundation 상태인지) 여부를 개별 판정해 색을 구분한다.
        /// </summary>
        private void UpdateCellIndicators(Vector2Int origin, int sizeX, int sizeZ)
        {
            float cs     = _grid != null ? _grid.CellSize : 10f;
            int   needed = sizeX * sizeZ;

            // 부족하면 생성
            while (_cellIndicators.Count < needed)
            {
                var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.name = "[CellIndicator]";
                DestroyImmediate(tile.GetComponent<BoxCollider>());
                tile.transform.localScale = new Vector3(cs * 0.95f, cs * 0.04f, cs * 0.95f);
                _cellIndicators.Add(tile);
            }
            // 남으면 제거
            while (_cellIndicators.Count > needed)
            {
                int last = _cellIndicators.Count - 1;
                Destroy(_cellIndicators[last]);
                _cellIndicators.RemoveAt(last);
            }

            int idx = 0;
            for (int dx = 0; dx < sizeX; dx++)
            {
                for (int dz = 0; dz < sizeZ; dz++)
                {
                    var cellPos = new Vector2Int(origin.x + dx, origin.y + dz);
                    var cell    = _grid?.GetCell(cellPos);

                    // 셀별 개별 판정: Foundation 상태이고 건물이 없어야 배치 가능
                    bool cellOk = cell != null && cell.CanBuild;

                    Vector3 worldPos = _grid != null
                        ? _grid.GridToWorld(cellPos)
                        : new Vector3(cellPos.x * 10f, 0f, cellPos.y * 10f);
                    worldPos.y = 0.05f;   // 지면 바로 위

                    var go = _cellIndicators[idx];
                    go.transform.position = worldPos;

                    var rend = go.GetComponent<Renderer>();
                    if (rend != null)
                        rend.material = cellOk ? _cellValidMat : _cellInvalidMat;

                    idx++;
                }
            }
        }

        private void ClearCellIndicators()
        {
            foreach (var tile in _cellIndicators)
                if (tile != null) Destroy(tile);
            _cellIndicators.Clear();
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
            if (_validMaterial == null)
                _validMaterial = CreateTransparentMaterial(new Color(0.2f, 1f, 0.2f, 0.4f));

            if (_invalidMaterial == null)
                _invalidMaterial = CreateTransparentMaterial(new Color(1f, 0.2f, 0.2f, 0.4f));

            // 셀 지시자용 (고스트보다 약간 진한 색)
            _cellValidMat   = CreateTransparentMaterial(new Color(0.1f, 0.9f, 0.1f, 0.6f));
            _cellInvalidMat = CreateTransparentMaterial(new Color(0.9f, 0.1f, 0.1f, 0.6f));
        }

        private static Material CreateTransparentMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                      ?? Shader.Find("Standard");

            var mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            mat.color = color;

            // URP 투명도 설정
            mat.SetFloat("_Surface", 1f);   // 1 = Transparent
            mat.SetFloat("_Blend",   0f);   // Alpha blending
            mat.SetFloat("_ZWrite",  0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000;

            return mat;
        }

        private void OnDestroy()
        {
            ClearCellIndicators();

            void DestroyIfDynamic(Material m)
            {
                if (m != null && m.hideFlags == HideFlags.HideAndDontSave) Destroy(m);
            }
            DestroyIfDynamic(_validMaterial);
            DestroyIfDynamic(_invalidMaterial);
            DestroyIfDynamic(_cellValidMat);
            DestroyIfDynamic(_cellInvalidMat);
        }
    }
}

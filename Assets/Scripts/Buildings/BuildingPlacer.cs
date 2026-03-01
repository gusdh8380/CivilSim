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

        // ── 내부 상태 ─────────────────────────────────────────
        private GridSystem      _grid;
        private BuildingManager _manager;
        private UnityEngine.Camera _cam;

        private BuildingData _selectedData;
        private GameObject   _ghost;
        private Vector2Int   _lastGridPos = new(-999, -999);
        private bool         _isValid;
        private int          _rotation;   // 0,1,2,3 → 0°,90°,180°,270°

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
                if (Mode == PlacerMode.Placing && _isValid)
                    ExecutePlace(gridPos);
                else if (Mode == PlacerMode.Removing)
                    ExecuteRemove(gridPos);
            }
        }

        // ── 공개 API ──────────────────────────────────────────

        public void StartPlacing(BuildingData data)
        {
            if (data == null) return;
            Cancel();

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
            _selectedData = null;
            _rotation     = 0;
            Mode          = PlacerMode.None;
        }

        // ── 실행 ─────────────────────────────────────────────

        private void ExecutePlace(Vector2Int pos)
        {
            // 회전 적용된 크기 계산
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

            _manager.TryPlace(pos, _selectedData);
            // 연속 배치 유지 (RMB 또는 Escape로 취소)
        }

        private void ExecuteRemove(Vector2Int pos)
        {
            _manager.TryRemove(pos);
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
                int sizeX = _rotation % 2 == 0 ? _selectedData.SizeX : _selectedData.SizeZ;
                int sizeZ = _rotation % 2 == 0 ? _selectedData.SizeZ : _selectedData.SizeX;
                _ghost.transform.localScale = new Vector3(sizeX * 0.9f, 1f, sizeZ * 0.9f);
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

            int sizeX = _rotation % 2 == 0 ? _selectedData.SizeX : _selectedData.SizeZ;
            int sizeZ = _rotation % 2 == 0 ? _selectedData.SizeZ : _selectedData.SizeX;

            _isValid = _grid.CanBuildArea(gridPos, sizeX, sizeZ);

            if (_ghost != null)
            {
                Vector3 worldPos = _grid.GridToWorld(gridPos);
                // 멀티셀의 경우 중심점 보정
                _ghost.transform.position = worldPos + new Vector3((sizeX - 1) * _grid.CellSize * 0.5f,
                                                                     0.5f,
                                                                     (sizeZ - 1) * _grid.CellSize * 0.5f);
                SetGhostMaterial(_isValid ? _validMaterial : _invalidMaterial);
            }
        }

        private void Rotate()
        {
            _rotation = (_rotation + 1) % 4;
            if (_ghost != null)
            {
                ApplyGhostRotation();
                // 스케일도 교체
                if (_selectedData.Prefab == null && _ghost != null)
                {
                    int sizeX = _rotation % 2 == 0 ? _selectedData.SizeX : _selectedData.SizeZ;
                    int sizeZ = _rotation % 2 == 0 ? _selectedData.SizeZ : _selectedData.SizeX;
                    _ghost.transform.localScale = new Vector3(sizeX * 0.9f, 1f, sizeZ * 0.9f);
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

        // ── 유틸 ─────────────────────────────────────────────

        private Vector2Int ScreenToGrid()
        {
            if (_cam == null) return Vector2Int.zero;
            var mouse = Mouse.current;
            if (mouse == null) return Vector2Int.zero;

            Ray ray      = _cam.ScreenPointToRay(mouse.position.ReadValue());
            var plane    = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float dist))
                return _grid.WorldToGridClamped(ray.GetPoint(dist));

            return Vector2Int.zero;
        }

        private void EnsureMaterials()
        {
            if (_validMaterial == null)
                _validMaterial = CreateTransparentMaterial(new Color(0.2f, 1f, 0.2f, 0.45f));

            if (_invalidMaterial == null)
                _invalidMaterial = CreateTransparentMaterial(new Color(1f, 0.2f, 0.2f, 0.45f));
        }

        private static Material CreateTransparentMaterial(Color color)
        {
            // URP → Standard 순서로 셰이더 탐색
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                      ?? Shader.Find("Standard");

            var mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            mat.color = color;

            // 투명도 설정 (URP)
            mat.SetFloat("_Surface", 1f);           // 1 = Transparent
            mat.SetFloat("_Blend",   0f);           // Alpha
            mat.SetFloat("_ZWrite",  0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000;

            return mat;
        }

        private void OnDestroy()
        {
            // 동적 생성 머티리얼 정리
            if (_validMaterial   != null && _validMaterial.hideFlags   == HideFlags.HideAndDontSave)
                Destroy(_validMaterial);
            if (_invalidMaterial != null && _invalidMaterial.hideFlags == HideFlags.HideAndDontSave)
                Destroy(_invalidMaterial);
        }
    }
}

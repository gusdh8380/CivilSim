using UnityEngine;
using UnityEngine.InputSystem;
using CivilSim.Core;
using CivilSim.Buildings;

namespace CivilSim.UI
{
    /// <summary>
    /// 일반 모드에서 배치된 건물을 클릭해 선택하고 하이라이트한다.
    /// 배치/철거 모드 중에는 동작하지 않는다.
    ///
    /// 씬에 하나만 존재하면 된다. GameManager 오브젝트에 추가 가능.
    /// </summary>
    public class PlacedBuildingSelector : MonoBehaviour
    {
        // ── 인스펙터 ──────────────────────────────────────────
        [Header("하이라이트 색상")]
        [SerializeField] private Color _highlightColor = new Color(1f, 0.95f, 0.3f);

        [Header("연결 패널")]
        [SerializeField] private SelectedBuildingPanel _infoPanel;

        // ── 내부 상태 ─────────────────────────────────────────
        private UnityEngine.Camera  _cam;
        private BuildingInstance    _selected;
        private Renderer            _selectedRenderer;
        private Color               _originalColor;

        // ── Unity ────────────────────────────────────────────

        private void Awake()
        {
            _cam = UnityEngine.Camera.main;
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            if (!mouse.leftButton.wasPressedThisFrame) return;

            // 배치/철거 모드 중에는 무시
            if (GameManager.Instance.Placer != null &&
                GameManager.Instance.Placer.Mode != PlacerMode.None)
                return;

            // UI 위에서 클릭 시 무시
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return;

            Ray ray = _cam.ScreenPointToRay(mouse.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, 500f))
            {
                var instance = hit.collider.GetComponentInParent<BuildingInstance>();
                if (instance != null)
                {
                    // 같은 건물 재클릭 → 해제
                    if (instance == _selected) { Deselect(); return; }
                    Select(instance);
                    return;
                }
            }

            Deselect();
        }

        // ── 선택 / 해제 ───────────────────────────────────────

        private void Select(BuildingInstance instance)
        {
            Deselect();

            _selected         = instance;
            _selectedRenderer = instance.GetComponentInChildren<Renderer>();

            if (_selectedRenderer != null)
            {
                _originalColor            = _selectedRenderer.material.color;
                _selectedRenderer.material.color = _highlightColor;
            }

            _infoPanel?.Show(instance);
        }

        private void Deselect()
        {
            if (_selected == null) return;

            if (_selectedRenderer != null)
                _selectedRenderer.material.color = _originalColor;

            _selected         = null;
            _selectedRenderer = null;

            _infoPanel?.Hide();
        }

        // 선택된 건물이 외부에서 철거된 경우 정리
        private void OnEnable()
        {
            GameEventBus.Subscribe<BuildingRemovedEvent>(OnBuildingRemoved);
        }

        private void OnDisable()
        {
            GameEventBus.Unsubscribe<BuildingRemovedEvent>(OnBuildingRemoved);
        }

        private void OnBuildingRemoved(BuildingRemovedEvent e)
        {
            if (_selected != null && _selected.GridOrigin == e.GridPosition)
                Deselect();
        }
    }
}

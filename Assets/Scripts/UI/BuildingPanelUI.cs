using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using CivilSim.Core;
using CivilSim.Buildings;

namespace CivilSim.UI
{
    /// <summary>
    /// 화면 하단 건물 선택 패널.
    /// 카테고리 탭 + 건물 버튼 목록을 동적으로 생성한다.
    ///
    /// 씬 구성:
    ///   Canvas
    ///   └── BuildingPanel (이 컴포넌트)
    ///       ├── TabContainer  (HorizontalLayoutGroup)
    ///       └── ButtonScrollView
    ///           └── Content   (GridLayoutGroup) ← _buttonContainer
    /// </summary>
    public class BuildingPanelUI : MonoBehaviour
    {
        // ── 인스펙터 ──────────────────────────────────────────
        [Header("패널 루트")]
        [SerializeField] private GameObject _panel;

        [Header("탭")]
        [SerializeField] private Transform  _tabContainer;
        [SerializeField] private GameObject _tabButtonPrefab; // Button + TMP_Text

        [Header("건물 버튼 목록")]
        [SerializeField] private Transform  _buttonContainer; // Content (GridLayoutGroup)
        [SerializeField] private GameObject _buildingButtonPrefab; // BuildingButtonUI Prefab

        [Header("탭 색상")]
        [SerializeField] private Color _activeTabColor   = new Color(0.25f, 0.75f, 0.40f);
        [SerializeField] private Color _inactiveTabColor = new Color(0.22f, 0.22f, 0.22f);

        // ── 내부 상태 ─────────────────────────────────────────
        private BuildingDatabase         _db;
        private BuildingCategory?        _currentCategory;
        private readonly List<BuildingButtonUI> _buttons = new();
        private Button                   _activeTabBtn;

        // 탭 정의 (label, category / null = 전체)
        private static readonly (string Label, BuildingCategory? Cat)[] TabDefs =
        {
            ("전체",  null),
            ("주거",  BuildingCategory.Residential),
            ("상업",  BuildingCategory.Commercial),
            ("공업",  BuildingCategory.Industrial),
            ("공공",  BuildingCategory.Public),
            ("유틸",  BuildingCategory.Utility),
        };

        // ── Unity ────────────────────────────────────────────

        private void Start()
        {
            _db = GameManager.Instance.BuildingDB;
            BuildTabs();
            SelectCategory(null); // 전체 탭으로 시작
        }

        private void Update()
        {
            // B키 토글
            if (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame)
                Toggle();
        }

        // ── 탭 생성 ───────────────────────────────────────────

        private void BuildTabs()
        {
            if (_tabContainer == null || _tabButtonPrefab == null) return;

            foreach (var (label, cat) in TabDefs)
            {
                var go  = Instantiate(_tabButtonPrefab, _tabContainer);
                var btn = go.GetComponent<Button>();
                var txt = go.GetComponentInChildren<TextMeshProUGUI>();

                if (txt != null) txt.text = label;

                var capturedCat = cat;
                btn.onClick.AddListener(() => SelectCategory(capturedCat));

                if (cat == null) // 초기 활성 탭
                    SetTabActive(btn);
            }
        }

        private void SelectCategory(BuildingCategory? category)
        {
            _currentCategory = category;

            // 탭 색상 갱신
            int idx = 0;
            foreach (Transform child in _tabContainer)
            {
                var btn = child.GetComponent<Button>();
                if (btn == null) continue;
                bool isActive = (TabDefs[idx].Cat == category);
                var colors    = btn.colors;
                colors.normalColor = isActive ? _activeTabColor : _inactiveTabColor;
                btn.colors         = colors;
                if (isActive) _activeTabBtn = btn;
                idx++;
            }

            RefreshButtons();
        }

        // ── 버튼 목록 갱신 ────────────────────────────────────

        private void RefreshButtons()
        {
            // 기존 버튼 제거
            foreach (var b in _buttons)
                if (b != null) Destroy(b.gameObject);
            _buttons.Clear();

            if (_db == null || _buttonContainer == null || _buildingButtonPrefab == null)
                return;

            IReadOnlyList<BuildingData> list = _currentCategory.HasValue
                ? _db.GetByCategory(_currentCategory.Value)
                : _db.All;

            foreach (var data in list)
            {
                if (data == null) continue;
                var go  = Instantiate(_buildingButtonPrefab, _buttonContainer);
                var btn = go.GetComponent<BuildingButtonUI>();
                btn?.Setup(data);
                _buttons.Add(btn);
            }
        }

        // ── 공개 API ──────────────────────────────────────────

        public void Toggle() => _panel?.SetActive(!_panel.activeSelf);
        public void Show()   => _panel?.SetActive(true);
        public void Hide()   => _panel?.SetActive(false);

        /// 외부에서 선택 버튼 하이라이트 해제 (철거 모드 등)
        public void ClearSelection()
        {
            foreach (var b in _buttons)
                b?.Deselect();
        }

        private void SetTabActive(Button btn)
        {
            if (btn == null) return;
            var colors             = btn.colors;
            colors.normalColor     = _activeTabColor;
            btn.colors             = colors;
            _activeTabBtn          = btn;
        }
    }
}

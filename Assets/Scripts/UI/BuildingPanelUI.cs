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
    ///   └── BuildingPanelRoot (이 컴포넌트 — 항상 Active)
    ///       └── PanelVisual (Image) ← _panel에 할당 (CanvasGroup 자동 추가됨)
    ///           ├── TabContainer  (HorizontalLayoutGroup)
    ///           └── ButtonScrollView
    ///               └── Content   (GridLayoutGroup) ← _buttonContainer
    ///
    /// ⚠️ _panel 은 반드시 이 스크립트보다 아래(자식)의 GameObject 이어야 합니다.
    ///    같은 오브젝트에 할당하면 B 키가 작동하지 않습니다.
    /// </summary>
    public class BuildingPanelUI : MonoBehaviour
    {
        // ── 인스펙터 ──────────────────────────────────────────
        [Header("패널 루트 (자식 오브젝트를 할당 — 이 스크립트와 다른 오브젝트!)")]
        [SerializeField] private GameObject _panel;          // 시각적 패널 (자식)

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
        private CanvasGroup              _panelGroup;      // ← SetActive 대신 사용
        private bool                     _isVisible;

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

        private void Awake()
        {
            // _panel에 CanvasGroup 자동 추가 (없으면)
            if (_panel != null)
            {
                _panelGroup = _panel.GetComponent<CanvasGroup>();
                if (_panelGroup == null)
                    _panelGroup = _panel.AddComponent<CanvasGroup>();
            }
            // 시작 시 숨김 (CanvasGroup 방식 — SetActive 대신)
            SetVisible(false);
        }

        private void Start()
        {
            _db = GameManager.Instance?.BuildingDB;

            if (_db == null)
                Debug.LogWarning("[BuildingPanelUI] BuildingDatabase가 GameManager에 할당되지 않았습니다!");
            else if (_db.Count == 0)
                Debug.LogWarning("[BuildingPanelUI] BuildingDatabase에 BuildingData가 없습니다. 데이터를 추가해주세요.");

            BuildTabs();
            SelectCategory(null); // 전체 탭으로 시작
        }

        private void Update()
        {
            // B키 토글 — CanvasGroup 방식이라 이 Update()는 항상 실행됨
            if (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame)
                Toggle();
        }

        // ── 탭 생성 ───────────────────────────────────────────

        private void BuildTabs()
        {
            if (_tabContainer == null || _tabButtonPrefab == null)
            {
                Debug.LogWarning("[BuildingPanelUI] TabContainer 또는 TabButtonPrefab이 할당되지 않았습니다.");
                return;
            }

            // 기존 탭 제거
            foreach (Transform child in _tabContainer)
                Destroy(child.gameObject);

            foreach (var (label, cat) in TabDefs)
            {
                var go  = Instantiate(_tabButtonPrefab, _tabContainer);
                var btn = go.GetComponent<Button>();
                var txt = go.GetComponentInChildren<TextMeshProUGUI>();

                if (txt  != null) txt.text = label;
                if (btn  == null) { Debug.LogError("[BuildingPanelUI] TabButtonPrefab에 Button 컴포넌트가 없습니다!"); continue; }

                // ← 캡처 변수로 클로저 버그 방지
                var capturedCat = cat;
                btn.onClick.AddListener(() => SelectCategory(capturedCat));

                // 초기 색상 적용
                var colors = btn.colors;
                colors.normalColor = (cat == null) ? _activeTabColor : _inactiveTabColor;
                btn.colors = colors;
            }
        }

        private void SelectCategory(BuildingCategory? category)
        {
            _currentCategory = category;

            // 탭 색상 갱신
            int idx = 0;
            foreach (Transform child in _tabContainer)
            {
                if (idx >= TabDefs.Length) break;
                var btn = child.GetComponent<Button>();
                if (btn == null) { idx++; continue; }

                bool isActive = (TabDefs[idx].Cat == category);
                var colors    = btn.colors;
                colors.normalColor = isActive ? _activeTabColor : _inactiveTabColor;
                btn.colors         = colors;
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

        /// <summary>B 키 또는 외부에서 패널 토글</summary>
        public void Toggle() => SetVisible(!_isVisible);

        public void Show() => SetVisible(true);
        public void Hide() => SetVisible(false);

        private void SetVisible(bool visible)
        {
            _isVisible = visible;
            if (_panelGroup == null) return;

            // 패널이 열릴 때 도로·지반 모드 취소 (단축키 충돌 방지)
            if (visible)
                GameManager.Instance?.CancelAllModes();

            _panelGroup.alpha          = visible ? 1f : 0f;
            _panelGroup.interactable   = visible;
            _panelGroup.blocksRaycasts = visible;
        }

        /// 외부에서 선택 버튼 하이라이트 해제 (철거 모드 등)
        public void ClearSelection()
        {
            foreach (var b in _buttons)
                b?.Deselect();
        }
    }
}

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CivilSim.Buildings;

namespace CivilSim.UI
{
    /// <summary>
    /// 배치된 건물 클릭 시 표시되는 정보 패널.
    /// PlacedBuildingSelector가 Show() / Hide()를 호출한다.
    ///
    /// 씬 구성 예시:
    ///   Canvas
    ///   └── SelectedBuildingPanel (이 컴포넌트)
    ///       ├── NameText       (TMP_Text)
    ///       ├── CategoryText   (TMP_Text)
    ///       ├── StatusText     (TMP_Text)
    ///       ├── PositionText   (TMP_Text)
    ///       └── RemoveButton   (Button) ← 철거 버튼
    /// </summary>
    public class SelectedBuildingPanel : MonoBehaviour
    {
        // ── 인스펙터 ──────────────────────────────────────────
        [Header("UI 요소")]
        [SerializeField] private GameObject      _panel;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _categoryText;
        [SerializeField] private TextMeshProUGUI _statusText;
        [SerializeField] private TextMeshProUGUI _positionText;
        [SerializeField] private Button          _removeButton;

        // ── 내부 상태 ─────────────────────────────────────────
        private BuildingInstance _current;

        // ── Unity ────────────────────────────────────────────

        private void Awake()
        {
            _removeButton?.onClick.AddListener(OnRemoveClicked);
            _panel?.SetActive(false);
        }

        // ── 공개 API ─────────────────────────────────────────

        public void Show(BuildingInstance instance)
        {
            _current = instance;
            _panel?.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            _current = null;
            _panel?.SetActive(false);
        }

        public void Refresh()
        {
            if (_current == null) return;

            var data = _current.Data;

            if (_nameText     != null) _nameText.text     = data.BuildingName;
            if (_categoryText != null) _categoryText.text = CategoryLabel(data.Category);
            if (_positionText != null) _positionText.text =
                $"위치 ({_current.GridOrigin.x}, {_current.GridOrigin.y})  크기 {data.SizeX}×{data.SizeZ}";

            // 운영 상태
            if (_statusText != null)
            {
                if (_current.IsOperational)
                {
                    _statusText.text  = "✅ 운영 중";
                    _statusText.color = Color.green;
                }
                else
                {
                    string warn = "⚠️ ";
                    if (data.RequiresPower && !_current.IsPowered) warn += "전기 없음  ";
                    if (data.RequiresWater && !_current.IsWatered) warn += "수도 없음";
                    _statusText.text  = warn.Trim();
                    _statusText.color = Color.yellow;
                }
            }
        }

        // ── 내부 ─────────────────────────────────────────────

        private void OnRemoveClicked()
        {
            if (_current == null) return;
            Core.GameManager.Instance.Buildings.TryRemove(_current.GridOrigin);
            Hide();
        }

        private static string CategoryLabel(BuildingCategory cat) => cat switch
        {
            BuildingCategory.Residential    => "주거",
            BuildingCategory.Commercial     => "상업",
            BuildingCategory.Industrial     => "공업",
            BuildingCategory.Public         => "공공시설",
            BuildingCategory.Utility        => "유틸리티",
            BuildingCategory.Infrastructure => "인프라",
            _                               => ""
        };
    }
}

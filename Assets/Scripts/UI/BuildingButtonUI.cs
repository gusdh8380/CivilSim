using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using CivilSim.Core;
using CivilSim.Buildings;

namespace CivilSim.UI
{
    /// <summary>
    /// 건물 선택 패널의 개별 버튼.
    /// 호버 시 툴팁 표시, 클릭 시 배치 모드 시작, 선택 하이라이트 관리.
    ///
    /// Prefab 구조 예시:
    ///   BuildingButton (Button + BuildingButtonUI)
    ///   ├── Background (Image)     ← _background
    ///   ├── Icon       (Image)     ← _icon
    ///   ├── Name       (TMP_Text)  ← _nameText
    ///   └── Cost       (TMP_Text)  ← _costText
    /// </summary>
    public class BuildingButtonUI : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        // ── 인스펙터 ──────────────────────────────────────────
        [Header("UI 요소")]
        [SerializeField] private Image              _icon;
        [SerializeField] private Image              _background;
        [SerializeField] private TextMeshProUGUI    _nameText;
        [SerializeField] private TextMeshProUGUI    _costText;

        [Header("상태 색상")]
        [SerializeField] private Color _normalColor   = new Color(0.18f, 0.18f, 0.18f);
        [SerializeField] private Color _hoverColor    = new Color(0.28f, 0.45f, 0.28f);
        [SerializeField] private Color _selectedColor = new Color(0.20f, 0.65f, 0.25f);

        // ── 내부 상태 ─────────────────────────────────────────
        private BuildingData _data;

        // 현재 선택된 버튼 (static으로 한 번에 하나만 선택)
        private static BuildingButtonUI _current;

        // ── 초기화 ───────────────────────────────────────────

        public void Setup(BuildingData data)
        {
            _data = data;

            if (_nameText   != null) _nameText.text   = data.BuildingName;
            if (_costText   != null) _costText.text   = $"₩{data.BuildCost:N0}";
            if (_icon       != null && data.Icon != null) _icon.sprite = data.Icon;
            if (_background != null) _background.color = _normalColor;
        }

        // ── 이벤트 핸들러 ─────────────────────────────────────

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_current != this && _background != null)
                _background.color = _hoverColor;

            BuildingTooltipUI.Show(_data);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_current != this && _background != null)
                _background.color = _normalColor;

            BuildingTooltipUI.Hide();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // 이미 선택된 버튼 재클릭 → 배치 취소
            if (_current == this)
            {
                Deselect();
                GameManager.Instance.CancelPlacing();
                return;
            }

            // 이전 버튼 해제
            _current?.Deselect();

            // 현재 버튼 선택
            _current = this;
            if (_background != null) _background.color = _selectedColor;

            // 배치 모드 시작
            GameManager.Instance.StartPlacing(_data);
        }

        // ── 공개 API ─────────────────────────────────────────

        public void Deselect()
        {
            if (_background != null) _background.color = _normalColor;
            if (_current == this) _current = null;
        }

        // 씬 오브젝트가 파괴될 때 static 참조 정리
        private void OnDestroy()
        {
            if (_current == this) _current = null;
        }
    }
}

using UnityEngine;

namespace CivilSim.Grid
{
    /// <summary>
    /// 런타임에 그리드 라인을 그린다.
    /// GL.Lines를 이용한 프로토타입 구현 — 추후 커스텀 메시/셰이더로 교체 가능.
    /// </summary>
    [RequireComponent(typeof(GridSystem))]
    public class GridVisualizer : MonoBehaviour
    {
        [Header("표시 설정")]
        [SerializeField] private bool  _showGrid  = true;
        [SerializeField] private Color _lineColor = new Color(1f, 1f, 1f, 0.15f);
        [SerializeField] private float _lineHeight = 0.02f; // 지면에서 살짝 위

        [Header("퍼포먼스")]
        [SerializeField, Tooltip("줌이 이 이상이면 그리드를 숨김")]
        private float _hideAboveFOV = 50f;

        // ── 내부 ─────────────────────────────────────────────
        private GridSystem _grid;
        private Material   _lineMaterial;
        private Camera     _cam;

        // ── Unity ─────────────────────────────────────────────

        private void Awake()
        {
            _grid = GetComponent<GridSystem>();
            _cam  = Camera.main;
            CreateLineMaterial();
        }

        private void OnRenderObject()
        {
            if (!_showGrid) return;

            // 줌 레벨 체크 (너무 멀면 숨김)
            if (_cam != null && !_cam.orthographic && _cam.fieldOfView > _hideAboveFOV)
                return;

            DrawGrid();
        }

        // ── 공개 API ─────────────────────────────────────────

        public void SetVisible(bool visible)  => _showGrid = visible;
        public void ToggleVisible()           => _showGrid = !_showGrid;

        // ── 내부 ─────────────────────────────────────────────

        private void DrawGrid()
        {
            _lineMaterial.SetPass(0);

            GL.Begin(GL.LINES);
            GL.Color(_lineColor);

            float w = _grid.Width  * _grid.CellSize;
            float h = _grid.Height * _grid.CellSize;
            Vector3 origin = _grid.Origin + new Vector3(0f, _lineHeight, 0f);

            // 세로선 (col 방향)
            for (int col = 0; col <= _grid.Width; col++)
            {
                float x = col * _grid.CellSize;
                GL.Vertex(origin + new Vector3(x, 0f, 0f));
                GL.Vertex(origin + new Vector3(x, 0f, h));
            }

            // 가로선 (row 방향)
            for (int row = 0; row <= _grid.Height; row++)
            {
                float z = row * _grid.CellSize;
                GL.Vertex(origin + new Vector3(0f, 0f, z));
                GL.Vertex(origin + new Vector3(w,  0f, z));
            }

            GL.End();
        }

        private void CreateLineMaterial()
        {
            // Unity 내장 셰이더 사용 (URP 호환)
            var shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            _lineMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _lineMaterial.SetInt("_Cull",     (int)UnityEngine.Rendering.CullMode.Off);
            _lineMaterial.SetInt("_ZWrite",   0);
        }

        private void OnDestroy()
        {
            if (_lineMaterial != null)
                Destroy(_lineMaterial);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 인스펙터에서 값 변경 시 즉시 반영
        }
#endif
    }
}

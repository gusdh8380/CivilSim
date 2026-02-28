using UnityEngine;

namespace CivilSim.World
{
    /// <summary>
    /// 그리드 전체 영역에 지형 타일을 자동 생성합니다.
    ///
    /// - Perlin Noise 기반 클러스터링 (비슷한 색끼리 뭉침)
    /// - 3~4가지 비슷한 초록색으로 자연스러운 땅 표현
    /// - 타일 스케일 1,1,1 의 Cube Prefab 사용
    /// - StaticBatchingUtility로 런타임 드로우콜 최소화
    ///
    /// ContextMenu "Regenerate" → 에디터에서 즉시 미리보기 가능
    /// </summary>
    public class GroundGenerator : MonoBehaviour
    {
        // ── 프리팹 / 색상 ──────────────────────────────────────
        [Header("Ground Prefabs (3~4개, 스케일 1·1·1 Cube)")]
        [Tooltip("비워두면 아래 자동 색상 모드로 동작합니다.")]
        [SerializeField] private GameObject[] _groundPrefabs;

        [Header("Auto Color 모드 (Prefabs 비어있을 때 사용)")]
        [SerializeField] private Color[] _groundColors = new Color[]
        {
            new Color(0.33f, 0.52f, 0.21f),   // 1. 진한 풀 (Dark Grass)
            new Color(0.40f, 0.60f, 0.27f),   // 2. 중간 풀 (Medium Grass)
            new Color(0.47f, 0.67f, 0.34f),   // 3. 밝은 풀 (Light Grass)
            new Color(0.38f, 0.56f, 0.25f),   // 4. 중간-진한 풀 (Med-Dark)
        };

        // ── 그리드 설정 ────────────────────────────────────────
        [Header("Grid Settings")]
        [Tooltip("GridSystem._width 와 동일하게 설정하세요.")]
        [SerializeField] private int   _gridWidth  = 100;
        [Tooltip("GridSystem._height 와 동일하게 설정하세요.")]
        [SerializeField] private int   _gridHeight = 100;
        [SerializeField] private float _cellSize   = 1f;
        [Tooltip("Y=-0.5 이면 큐브 윗면이 y=0 (그리드 바닥)에 맞음.")]
        [SerializeField] private float _groundY    = -0.5f;

        // ── Noise 설정 ─────────────────────────────────────────
        [Header("Noise / Cluster 설정")]
        [Tooltip("값이 작을수록 색상 클러스터가 커짐. (0.05~0.15 권장)")]
        [SerializeField] private float _primaryScale    = 0.07f;   // 대형 클러스터
        [Tooltip("보조 노이즈 — 미세한 경계 변화.")]
        [SerializeField] private float _secondaryScale  = 0.22f;
        [Tooltip("보조 노이즈 비중. 0 = 클러스터 강조, 1 = 완전 랜덤.")]
        [Range(0f, 1f)]
        [SerializeField] private float _secondaryWeight = 0.20f;
        [SerializeField] private int   _seed            = 42;

        // ── 내부 ──────────────────────────────────────────────
        private Transform  _container;
        private Material[] _autoMaterials;

        // ── Unity ─────────────────────────────────────────────

        private void Start()
        {
            Generate();
        }

        // ── 공개 API ──────────────────────────────────────────

        [ContextMenu("Regenerate")]
        public void Generate()
        {
            Clear();

            bool usePrefabs = _groundPrefabs != null && _groundPrefabs.Length > 0;
            int variantCount = usePrefabs ? _groundPrefabs.Length : _groundColors.Length;

            if (variantCount == 0)
            {
                Debug.LogError("[GroundGenerator] Prefab 또는 Color 중 하나는 설정해야 합니다.");
                return;
            }

            // 컨테이너 생성
            var containerGO = new GameObject("=== Ground ===");
            containerGO.transform.SetParent(transform, false);
            _container = containerGO.transform;

            // AutoColor 모드라면 머티리얼 미리 생성
            if (!usePrefabs)
                PrepareAutoMaterials();

            float ox = _seed * 13.37f;
            float oz = _seed *  7.91f;

            for (int x = 0; x < _gridWidth; x++)
            {
                for (int z = 0; z < _gridHeight; z++)
                {
                    // ─ Perlin Noise 두 옥타브 합성 ─
                    float n1 = Mathf.PerlinNoise((x + ox) * _primaryScale,
                                                 (z + oz) * _primaryScale);
                    float n2 = Mathf.PerlinNoise((x + ox + 200f) * _secondaryScale,
                                                 (z + oz + 200f) * _secondaryScale);
                    float noise = Mathf.Lerp(n1, n2, _secondaryWeight);

                    // noise [0,1] → variant index
                    int idx = Mathf.Clamp(
                        Mathf.FloorToInt(noise * variantCount),
                        0, variantCount - 1);

                    // 타일 중심 위치
                    var pos = new Vector3(
                        x * _cellSize + _cellSize * 0.5f,
                        _groundY,
                        z * _cellSize + _cellSize * 0.5f);

                    GameObject tile;
                    if (usePrefabs)
                    {
                        tile = Instantiate(_groundPrefabs[idx], pos, Quaternion.identity, _container);
                    }
                    else
                    {
                        tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        tile.transform.SetParent(_container, false);
                        tile.transform.position   = pos;
                        tile.transform.localScale = Vector3.one;
                        tile.GetComponent<MeshRenderer>().sharedMaterial = _autoMaterials[idx];

                        // 퍼포먼스: 땅 타일에는 콜라이더 불필요
                        Destroy(tile.GetComponent<BoxCollider>());
                    }

                    tile.name     = $"G{x}_{z}";
                    tile.isStatic = true;
                }
            }

            // 스태틱 배칭 — 드로우콜 대폭 감소
            StaticBatchingUtility.Combine(_container.gameObject);

            int total = _gridWidth * _gridHeight;
            Debug.Log($"[GroundGenerator] {total:N0}개 타일 생성 완료 (mode: {(usePrefabs ? "Prefab" : "AutoColor")})");
        }

        [ContextMenu("Clear")]
        public void Clear()
        {
            var existing = transform.Find("=== Ground ===");
            if (existing != null)
                DestroyImmediate(existing.gameObject);

            _container    = null;
            _autoMaterials = null;
        }

        // ── 내부 ──────────────────────────────────────────────

        /// AutoColor 모드: 색상 배열로 URP 머티리얼 동적 생성
        private void PrepareAutoMaterials()
        {
            _autoMaterials = new Material[_groundColors.Length];
            for (int i = 0; i < _groundColors.Length; i++)
            {
                // URP Lit → Standard 순으로 폴백
                var shader = Shader.Find("Universal Render Pipeline/Lit")
                          ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                          ?? Shader.Find("Standard");

                var mat = new Material(shader)
                {
                    color           = _groundColors[i],
                    enableInstancing = true,
                };
                _autoMaterials[i] = mat;
            }
        }
    }
}

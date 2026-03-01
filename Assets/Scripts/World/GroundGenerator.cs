using UnityEngine;

namespace CivilSim.World
{
    /// <summary>
    /// 그리드 전체 영역에 지형 타일을 자동 생성합니다.
    ///
    /// - Prefab 방식 전용 (머티리얼은 Prefab에서 직접 설정)
    /// - Perlin Noise 기반 클러스터링 (비슷한 타입끼리 뭉침)
    /// - StaticBatchingUtility로 런타임 드로우콜 최소화
    ///
    /// 사용법:
    ///   1. _groundPrefabs 에 3~4종 Ground Prefab 할당
    ///   2. ContextMenu "Regenerate" 또는 Play 시 자동 생성
    /// </summary>
    public class GroundGenerator : MonoBehaviour
    {
        // ── Ground Prefabs ─────────────────────────────────────
        [Header("Ground Prefabs (3~4개, CellSize에 맞는 평지 타일)")]
        [Tooltip("Pandazole 사용 시: 10×0.2×10 평지 타일 프리팹 3~4종.\n" +
                 "직접 제작 시: 1×1×1 큐브에 머티리얼 적용 후 할당.")]
        [SerializeField] private GameObject[] _groundPrefabs;

        // ── 그리드 설정 ────────────────────────────────────────
        [Header("Grid Settings")]
        [Tooltip("GridSystem._width 와 동일하게 설정하세요.")]
        [SerializeField] private int   _gridWidth  = 100;
        [Tooltip("GridSystem._height 와 동일하게 설정하세요.")]
        [SerializeField] private int   _gridHeight = 100;
        [Tooltip("GridSystem._cellSize 와 동일하게 설정하세요. (Pandazole 기본 10)")]
        [SerializeField] private float _cellSize   = 10f;
        [Tooltip("타일 중심의 Y 위치.\n" +
                 "Pandazole 평지 타일(두께 ~0.2): -0.1 권장 (윗면이 y=0)\n" +
                 "1×1×1 큐브 폴백: -0.5")]
        [SerializeField] private float _groundY    = -0.1f;

        // ── Noise / 클러스터 설정 ──────────────────────────────
        [Header("Noise / Cluster 설정")]
        [Tooltip("값이 작을수록 같은 타입 클러스터가 넓어짐. (0.05~0.15 권장)")]
        [SerializeField] private float _primaryScale    = 0.07f;
        [Tooltip("보조 노이즈 — 미세한 경계 불규칙성.")]
        [SerializeField] private float _secondaryScale  = 0.22f;
        [Tooltip("보조 노이즈 비중. 0 = 큰 덩어리, 1 = 완전 랜덤.")]
        [Range(0f, 1f)]
        [SerializeField] private float _secondaryWeight = 0.20f;
        [SerializeField] private int   _seed            = 42;

        // ── 내부 ──────────────────────────────────────────────
        private Transform _container;

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

            if (_groundPrefabs == null || _groundPrefabs.Length == 0)
            {
                Debug.LogError("[GroundGenerator] _groundPrefabs 에 Prefab을 1개 이상 할당해주세요.");
                return;
            }

            int variantCount = _groundPrefabs.Length;

            // 컨테이너 생성
            var containerGO = new GameObject("=== Ground ===");
            containerGO.transform.SetParent(transform, false);
            _container = containerGO.transform;

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

                    // noise [0,1] → prefab index
                    int idx = Mathf.Clamp(
                        Mathf.FloorToInt(noise * variantCount),
                        0, variantCount - 1);

                    // 타일 중심 위치
                    var pos = new Vector3(
                        x * _cellSize + _cellSize * 0.5f,
                        _groundY,
                        z * _cellSize + _cellSize * 0.5f);

                    var tile = Instantiate(_groundPrefabs[idx], pos, Quaternion.identity, _container);
                    tile.name     = $"G{x}_{z}";
                    tile.isStatic = true;
                }
            }

            // 스태틱 배칭 — 드로우콜 대폭 감소
            StaticBatchingUtility.Combine(_container.gameObject);

            Debug.Log($"[GroundGenerator] {_gridWidth * _gridHeight:N0}개 타일 생성 완료 ({variantCount}종 Prefab)");
        }

        [ContextMenu("Clear")]
        public void Clear()
        {
            var existing = transform.Find("=== Ground ===");
            if (existing != null)
                DestroyImmediate(existing.gameObject);

            _container = null;
        }
    }
}

using UnityEngine;
using CivilSim.Grid;
using CivilSim.Buildings;
using CivilSim.Economy;
using CivilSim.Infrastructure;

namespace CivilSim.Core
{
    /// <summary>
    /// 게임의 진입점이자 모든 서브시스템 참조를 보유하는 싱글턴.
    /// 다른 스크립트에서 GameManager.Instance.{시스템} 으로 접근한다.
    /// FindObjectOfType 사용 금지.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        // ── 싱글턴 ──────────────────────────────────────────
        public static GameManager Instance { get; private set; }

        // ── 서브시스템 참조 (Inspector에서 할당) ─────────────
        [Header("Core Systems")]
        [SerializeField] private GameClock  _gameClock;
        [SerializeField] private TickSystem _tickSystem;

        [Header("Grid")]
        [SerializeField] private GridSystem     _gridSystem;
        [SerializeField] private GridVisualizer _gridVisualizer;

        [Header("Buildings")]
        [SerializeField] private BuildingManager  _buildingManager;
        [SerializeField] private BuildingPlacer   _buildingPlacer;
        [SerializeField] private BuildingDatabase _buildingDatabase;

        [Header("Economy")]
        [SerializeField] private EconomyManager _economyManager;

        [Header("Infrastructure")]
        [SerializeField] private RoadManager _roadManager;
        [SerializeField] private RoadBuilder _roadBuilder;

        // ── 공개 접근자 ──────────────────────────────────────
        public GameClock        Clock      => _gameClock;
        public TickSystem       Tick       => _tickSystem;
        public GridSystem       Grid       => _gridSystem;
        public GridVisualizer   GridVisual => _gridVisualizer;
        public BuildingManager  Buildings  => _buildingManager;
        public BuildingPlacer   Placer     => _buildingPlacer;
        public BuildingDatabase BuildingDB => _buildingDatabase;
        public EconomyManager   Economy    => _economyManager;
        public RoadManager      Roads      => _roadManager;
        public RoadBuilder      RoadBuild  => _roadBuilder;

        // ── Unity ───────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            ValidateSystems();
            GameEventBus.Publish(new GameStartedEvent());

            Debug.Log("[GameManager] 시스템 초기화 완료.");
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                GameEventBus.Clear();
                Instance = null;
            }
        }

        // ── 내부 ────────────────────────────────────────────

        private void ValidateSystems()
        {
            if (_gameClock        == null) Debug.LogError("[GameManager] GameClock이 할당되지 않았습니다.");
            if (_tickSystem       == null) Debug.LogError("[GameManager] TickSystem이 할당되지 않았습니다.");
            if (_gridSystem       == null) Debug.LogError("[GameManager] GridSystem이 할당되지 않았습니다.");
            if (_buildingManager  == null) Debug.LogError("[GameManager] BuildingManager가 할당되지 않았습니다.");
            if (_buildingPlacer   == null) Debug.LogError("[GameManager] BuildingPlacer가 할당되지 않았습니다.");
            if (_buildingDatabase == null) Debug.LogWarning("[GameManager] BuildingDatabase가 할당되지 않았습니다.");
            if (_economyManager   == null) Debug.LogWarning("[GameManager] EconomyManager가 할당되지 않았습니다.");
            if (_roadManager      == null) Debug.LogWarning("[GameManager] RoadManager가 할당되지 않았습니다.");
            if (_roadBuilder      == null) Debug.LogWarning("[GameManager] RoadBuilder가 할당되지 않았습니다.");
        }

        // ── 편의 메서드 ──────────────────────────────────────

        public void SetTimeSpeed(TimeSpeed speed) => _gameClock?.SetSpeed(speed);
        public void TogglePause()                 => _gameClock?.TogglePause();

        // 건물 배치/철거
        public void StartPlacing(BuildingData data) => _buildingPlacer?.StartPlacing(data);
        public void StartRemoving()                 => _buildingPlacer?.StartRemoving();
        public void CancelPlacing()                 => _buildingPlacer?.Cancel();

        // 도로 배치/철거
        public void StartRoadBuilding() => _roadBuilder?.StartBuilding();
        public void StartRoadRemoving() => _roadBuilder?.StartRemoving();
        public void CancelRoad()        => _roadBuilder?.Cancel();
    }
}

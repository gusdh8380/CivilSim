using UnityEngine;
using CivilSim.Grid;
using CivilSim.Buildings;
using CivilSim.Economy;
using CivilSim.Infrastructure;
using CivilSim.Population;
using CivilSim.Zones;

namespace CivilSim.Core
{
    /// <summary>
    /// 게임의 진입점이자 모든 서브시스템 참조를 보유하는 싱글턴.
    /// 다른 스크립트에서 GameManager.Instance.{시스템} 으로 접근한다.
    /// FindObjectOfType 사용 금지.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        // -- 싱글턴 --
        public static GameManager Instance { get; private set; }

        // -- 서브시스템 참조 (Inspector에서 할당) --
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
        [SerializeField] private RoadManager       _roadManager;
        [SerializeField] private RoadBuilder       _roadBuilder;
        [SerializeField] private FoundationManager _foundationManager;
        [SerializeField] private FoundationBuilder _foundationBuilder;

        [Header("Zones")]
        [SerializeField] private ZoneManager _zoneManager;
        [SerializeField] private ZoneBuilder _zoneBuilder;

        [Header("Population")]
        [SerializeField] private CityDemandSystem _cityDemandSystem;

        [Header("Progression")]
        [SerializeField] private CityProgressionManager _cityProgressionManager;

        // -- 공개 접근자 --
        public GameClock        Clock           => _gameClock;
        public TickSystem       Tick            => _tickSystem;
        public GridSystem       Grid            => _gridSystem;
        public GridVisualizer   GridVisual      => _gridVisualizer;
        public BuildingManager  Buildings       => _buildingManager;
        public BuildingPlacer   Placer          => _buildingPlacer;
        public BuildingDatabase BuildingDB      => _buildingDatabase;
        public EconomyManager   Economy         => _economyManager;
        public RoadManager      Roads           => _roadManager;
        public RoadBuilder      RoadBuild       => _roadBuilder;
        public FoundationManager Foundation     => _foundationManager;
        public FoundationBuilder FoundationBuild => _foundationBuilder;
        public ZoneManager      Zone            => _zoneManager;
        public ZoneBuilder      ZoneBuild       => _zoneBuilder;
        public CityDemandSystem Demand          => _cityDemandSystem;
        public CityProgressionManager Progression => _cityProgressionManager;

        // -- Unity --

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            AutoDiscoverSystems();   // Inspector 미연결 시 자동 탐색
            ValidateSystems();
            GameEventBus.Publish(new GameStartedEvent());

            Debug.Log("[GameManager] 시스템 초기화 완료.");
        }

        /// <summary>
        /// Inspector에서 할당되지 않은 서브시스템을 씬에서 자동으로 탐색한다.
        /// 씬에 컴포넌트가 존재하면 연결하므로 Inspector 할당을 빠뜨려도 동작한다.
        /// </summary>
        private void AutoDiscoverSystems()
        {
            if (_gameClock       == null) _gameClock       = FindFirstObjectByType<GameClock>();
            if (_tickSystem      == null) _tickSystem      = FindFirstObjectByType<TickSystem>();
            if (_gridSystem      == null) _gridSystem      = FindFirstObjectByType<GridSystem>();
            if (_gridVisualizer  == null) _gridVisualizer  = FindFirstObjectByType<GridVisualizer>();
            if (_buildingManager == null) _buildingManager = FindFirstObjectByType<BuildingManager>();
            if (_buildingPlacer  == null) _buildingPlacer  = FindFirstObjectByType<BuildingPlacer>();
            if (_economyManager  == null) _economyManager  = FindFirstObjectByType<EconomyManager>();
            if (_roadManager        == null) _roadManager        = FindFirstObjectByType<RoadManager>();
            if (_roadBuilder        == null) _roadBuilder        = FindFirstObjectByType<RoadBuilder>();
            if (_foundationManager  == null) _foundationManager  = FindFirstObjectByType<FoundationManager>();
            if (_foundationBuilder  == null) _foundationBuilder  = FindFirstObjectByType<FoundationBuilder>();
            if (_zoneManager        == null) _zoneManager        = FindFirstObjectByType<ZoneManager>();
            if (_zoneBuilder        == null) _zoneBuilder        = FindFirstObjectByType<ZoneBuilder>();
            if (_cityDemandSystem   == null) _cityDemandSystem   = FindFirstObjectByType<CityDemandSystem>();
            if (_cityProgressionManager == null) _cityProgressionManager = FindFirstObjectByType<CityProgressionManager>();
            if (_cityProgressionManager == null)
                _cityProgressionManager = gameObject.AddComponent<CityProgressionManager>();
            // BuildingDatabase는 ScriptableObject라 FindObjectOfType 대상 아님 — Inspector 할당 필수
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                GameEventBus.Clear();
                Instance = null;
            }
        }

        // -- 내부 --

        private void ValidateSystems()
        {
            if (_gameClock        == null) Debug.LogError("[GameManager] GameClock이 할당되지 않았습니다.");
            if (_tickSystem       == null) Debug.LogError("[GameManager] TickSystem이 할당되지 않았습니다.");
            if (_gridSystem       == null) Debug.LogError("[GameManager] GridSystem이 할당되지 않았습니다.");
            if (_buildingManager  == null) Debug.LogError("[GameManager] BuildingManager가 할당되지 않았습니다.");
            if (_buildingPlacer   == null) Debug.LogError("[GameManager] BuildingPlacer가 할당되지 않았습니다.");
            if (_buildingDatabase == null) Debug.LogWarning("[GameManager] BuildingDatabase가 할당되지 않았습니다.");
            if (_economyManager   == null) Debug.LogWarning("[GameManager] EconomyManager가 할당되지 않았습니다.");
            if (_roadManager       == null) Debug.LogWarning("[GameManager] RoadManager가 할당되지 않았습니다.");
            if (_roadBuilder       == null) Debug.LogWarning("[GameManager] RoadBuilder가 할당되지 않았습니다.");
            if (_foundationManager == null) Debug.LogWarning("[GameManager] FoundationManager가 할당되지 않았습니다.");
            if (_foundationBuilder == null) Debug.LogWarning("[GameManager] FoundationBuilder가 할당되지 않았습니다.");
            if (_zoneManager       == null) Debug.LogWarning("[GameManager] ZoneManager가 할당되지 않았습니다.");
            if (_zoneBuilder       == null) Debug.LogWarning("[GameManager] ZoneBuilder가 할당되지 않았습니다.");
            if (_cityDemandSystem  == null) Debug.LogWarning("[GameManager] CityDemandSystem이 할당되지 않았습니다.");
            if (_cityProgressionManager == null) Debug.LogWarning("[GameManager] CityProgressionManager가 할당되지 않았습니다.");
        }

        // -- 편의 메서드 --

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

        // 지반 다지기
        public void StartFoundationBuilding() => _foundationBuilder?.Activate();
        public void StopFoundationBuilding()  => _foundationBuilder?.Deactivate();

        // 구역 지정
        public void StartZoning() => _zoneBuilder?.Activate();
        public void StopZoning()  => _zoneBuilder?.Deactivate();

        /// <summary>
        /// 현재 활성화된 모든 배치 모드(건물·도로·지반·구역)를 동시에 취소한다.
        /// 다른 모드를 시작하기 전에 호출해 단축키 충돌을 방지한다.
        /// </summary>
        public void CancelAllModes()
        {
            _buildingPlacer?.Cancel();
            _roadBuilder?.Cancel();
            _foundationBuilder?.Deactivate();
            _zoneBuilder?.Deactivate();
        }
    }
}

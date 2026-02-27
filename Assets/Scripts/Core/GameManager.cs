using UnityEngine;
using CivilSim.Grid;

namespace CivilSim.Core
{
    /// <summary>
    /// 게임의 진입점이자 모든 서브시스템 참조를 보유하는 싱글턴.
    /// 다른 스크립트에서 GameManager.Instance.Grid 처럼 접근한다.
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

        // ── 공개 접근자 ──────────────────────────────────────
        public GameClock     Clock      => _gameClock;
        public TickSystem    Tick       => _tickSystem;
        public GridSystem    Grid       => _gridSystem;
        public GridVisualizer GridVisual => _gridVisualizer;

        // ── Unity ───────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            // 씬 전환 시에도 유지 (필요 없으면 제거)
            // DontDestroyOnLoad(gameObject);

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
            if (_gameClock    == null) Debug.LogError("[GameManager] GameClock이 할당되지 않았습니다.");
            if (_tickSystem   == null) Debug.LogError("[GameManager] TickSystem이 할당되지 않았습니다.");
            if (_gridSystem   == null) Debug.LogError("[GameManager] GridSystem이 할당되지 않았습니다.");
        }

        // ── 편의 메서드 ──────────────────────────────────────

        public void SetTimeSpeed(TimeSpeed speed) => _gameClock?.SetSpeed(speed);
        public void TogglePause()                 => _gameClock?.TogglePause();
    }
}

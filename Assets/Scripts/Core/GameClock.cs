using UnityEngine;

namespace CivilSim.Core
{
    public enum TimeSpeed
    {
        Paused   = 0,
        Normal   = 1,  // x1
        Fast     = 2,  // x2
        VeryFast = 3   // x4
    }

    /// <summary>
    /// 게임 내 시간 흐름을 담당한다.
    /// 실제 5초 = 게임 1일 (Normal 기준). 일→월→년 자동 진행.
    /// </summary>
    public class GameClock : MonoBehaviour
    {
        [Header("시간 설정")]
        [SerializeField, Tooltip("Normal 속도 기준 1게임일 = 몇 초")]
        private float _dayDurationSeconds = 5f;

        // ── 공개 상태 ───────────────────────────────────────
        public int Day   { get; private set; } = 1;
        public int Month { get; private set; } = 1;
        public int Year  { get; private set; } = 2025;
        public TimeSpeed CurrentSpeed { get; private set; } = TimeSpeed.Normal;

        public string DateString => $"{Year}년 {Month:D2}월 {Day:D2}일";

        // ── 내부 ────────────────────────────────────────────
        private static readonly float[] SpeedMultipliers = { 0f, 1f, 2f, 4f };
        private float _timer;

        private const int DaysPerMonth = 30;
        private const int MonthsPerYear = 12;

        // ── Unity ───────────────────────────────────────────

        private void Update()
        {
            float multiplier = SpeedMultipliers[(int)CurrentSpeed];
            if (multiplier <= 0f) return;

            _timer += Time.deltaTime * multiplier;

            if (_timer >= _dayDurationSeconds)
            {
                _timer -= _dayDurationSeconds;
                AdvanceDay();
            }
        }

        // ── 공개 API ─────────────────────────────────────────

        public void SetSpeed(TimeSpeed speed)
        {
            CurrentSpeed = speed;
            GameEventBus.Publish(new TimeSpeedChangedEvent { Speed = speed });
        }

        public void TogglePause()
        {
            SetSpeed(CurrentSpeed == TimeSpeed.Paused ? TimeSpeed.Normal : TimeSpeed.Paused);
        }

        // ── 내부 ────────────────────────────────────────────

        private void AdvanceDay()
        {
            Day++;

            if (Day > DaysPerMonth)
            {
                Day = 1;
                Month++;

                if (Month > MonthsPerYear)
                {
                    Month = 1;
                    Year++;
                }
            }

            GameEventBus.Publish(new TickEvent
            {
                Day   = Day,
                Month = Month,
                Year  = Year
            });
        }
    }
}

using UnityEngine;

namespace CivilSim.Core
{
    /// <summary>
    /// GameClock의 TickEvent를 받아 DailyTickEvent / MonthlyTickEvent로 분배한다.
    /// Economy, Population 등이 이 이벤트를 구독해 주기적 업데이트를 수행한다.
    /// </summary>
    public class TickSystem : MonoBehaviour
    {
        private void OnEnable()
        {
            GameEventBus.Subscribe<TickEvent>(OnTick);
        }

        private void OnDisable()
        {
            GameEventBus.Unsubscribe<TickEvent>(OnTick);
        }

        private void OnTick(TickEvent e)
        {
            // 매일 발행
            GameEventBus.Publish(new DailyTickEvent
            {
                Day   = e.Day,
                Month = e.Month,
                Year  = e.Year
            });

            // 매월 1일에 발행 (경제 정산, 인구 업데이트 등)
            if (e.Day == 1)
            {
                GameEventBus.Publish(new MonthlyTickEvent
                {
                    Month = e.Month,
                    Year  = e.Year
                });
            }
        }
    }
}

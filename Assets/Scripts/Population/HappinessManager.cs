using UnityEngine;
using CivilSim.Core;

namespace CivilSim.Population
{
    /// <summary>
    /// 도시 시민 행복도를 계산하고 발행한다.
    ///
    /// 행복도 = ServiceScore × serviceWeight + OperationRate × 100 × operationWeight
    ///   - ServiceScore: 교육·의료·치안·위생 커버리지 종합 점수 (0~100)
    ///   - OperationRate: 전력·수도 공급률에서 결정되는 운영률 (0~1)
    ///
    /// UtilityStatusChangedEvent를 구독해 실시간 갱신한다.
    /// GameManager.Instance.Happiness 로 접근.
    /// </summary>
    public class HappinessManager : MonoBehaviour
    {
        [Header("행복도 가중치 (합산 = 1.0 권장)")]
        [Tooltip("서비스 커버리지(교육·의료·치안·위생)가 행복도에 반영되는 비율")]
        [SerializeField, Range(0f, 1f)] private float _serviceWeight = 0.6f;
        [Tooltip("전력·수도 운영률이 행복도에 반영되는 비율")]
        [SerializeField, Range(0f, 1f)] private float _operationWeight = 0.4f;

        [Header("알림 임계값")]
        [Tooltip("행복도가 이 값 이상 변동할 때 플레이어에게 알림을 발송")]
        [SerializeField, Range(1f, 50f)] private float _notifyThreshold = 10f;

        /// <summary>현재 시민 행복도 (0~100).</summary>
        public float Happiness { get; private set; } = 100f;

        private float _serviceScore   = 100f;
        private float _operationRate  = 1f;
        private float _lastNotifiedHappiness = 100f;

        // -- Unity --

        private void Start()
        {
            Recalculate();
        }

        private void OnEnable()
        {
            GameEventBus.Subscribe<UtilityStatusChangedEvent>(OnUtilityChanged);
        }

        private void OnDisable()
        {
            GameEventBus.Unsubscribe<UtilityStatusChangedEvent>(OnUtilityChanged);
        }

        // -- 내부 --

        private void OnUtilityChanged(UtilityStatusChangedEvent e)
        {
            _serviceScore  = e.ServiceScore;
            _operationRate = e.OperationRate;
            Recalculate();
        }

        private void Recalculate()
        {
            float serviceHappiness   = Mathf.Clamp(_serviceScore,        0f, 100f);
            float operationHappiness = Mathf.Clamp(_operationRate * 100f, 0f, 100f);

            float newHappiness = _serviceWeight * serviceHappiness
                               + _operationWeight * operationHappiness;
            newHappiness = Mathf.Clamp(newHappiness, 0f, 100f);

            // 실질적으로 변하지 않으면 이벤트 생략
            if (Mathf.Approximately(newHappiness, Happiness)) return;

            float previous = Happiness;
            Happiness = newHappiness;
            GameEventBus.Publish(new HappinessChangedEvent { NewHappiness = Happiness });

            // 임계값 이상 변동 시 플레이어 알림
            if (Mathf.Abs(Happiness - _lastNotifiedHappiness) >= _notifyThreshold)
            {
                bool improved = Happiness > previous;
                GameEventBus.Publish(new NotificationEvent
                {
                    Message = $"시민 행복도 {(improved ? "상승" : "하락")}: {Mathf.RoundToInt(Happiness)}점",
                    Type    = improved ? NotificationType.Info : NotificationType.Warning
                });
                _lastNotifiedHappiness = Happiness;
            }
        }
    }
}

using UnityEngine;
using CivilSim.Buildings;
using CivilSim.Core;

namespace CivilSim.Infrastructure
{
    /// <summary>
    /// 전기/수도 공급-수요와 서비스 점수를 계산한다.
    /// 1차 구현은 도시 전체 풀 방식으로 운영한다.
    /// </summary>
    public class UtilityManager : MonoBehaviour
    {
        [Header("서비스 점수 보정")]
        [SerializeField, Min(1)] private int _serviceScoreAtFull = 100;
        [SerializeField, Range(0.5f, 1.0f)] private float _minServiceMultiplier = 0.85f;
        [SerializeField, Range(1.0f, 1.5f)] private float _maxServiceMultiplier = 1.15f;

        [Header("알림")]
        [SerializeField] private bool _notifyOnMonthlyShortage = true;

        public int PowerDemand { get; private set; }
        public int PowerSupply { get; private set; }
        public int WaterDemand { get; private set; }
        public int WaterSupply { get; private set; }
        public int ServiceScore { get; private set; }

        public float PowerRate { get; private set; } = 1f;
        public float WaterRate { get; private set; } = 1f;
        public float OperationRate { get; private set; } = 1f;
        public float ServiceMultiplier { get; private set; } = 1f;

        private BuildingManager _buildings;
        private bool _wasPowerShortage;
        private bool _wasWaterShortage;

        private void Start()
        {
            _buildings = GameManager.Instance?.Buildings;
            RecalculateAndPublish(false);
        }

        private void OnEnable()
        {
            GameEventBus.Subscribe<BuildingPlacedEvent>(OnBuildingChanged);
            GameEventBus.Subscribe<BuildingRemovedEvent>(OnBuildingChanged);
            GameEventBus.Subscribe<MonthlyTickEvent>(OnMonthlyTick);
        }

        private void OnDisable()
        {
            GameEventBus.Unsubscribe<BuildingPlacedEvent>(OnBuildingChanged);
            GameEventBus.Unsubscribe<BuildingRemovedEvent>(OnBuildingChanged);
            GameEventBus.Unsubscribe<MonthlyTickEvent>(OnMonthlyTick);
        }

        private void OnBuildingChanged(BuildingPlacedEvent e) => RecalculateAndPublish(false);
        private void OnBuildingChanged(BuildingRemovedEvent e) => RecalculateAndPublish(false);
        private void OnMonthlyTick(MonthlyTickEvent e) => RecalculateAndPublish(true);

        private void RecalculateAndPublish(bool notifyShortage)
        {
            if (_buildings == null) _buildings = GameManager.Instance?.Buildings;
            if (_buildings == null) return;

            int totalPowerDemand = 0;
            int totalPowerSupply = 0;
            int totalWaterDemand = 0;
            int totalWaterSupply = 0;
            int totalServiceValue = 0;

            foreach (var kv in _buildings.GetAll())
            {
                BuildingData data = kv.Value?.Data;
                if (data == null) continue;

                if (data.RequiresPower)
                    totalPowerDemand += Mathf.Max(0, data.PowerConsumption);
                if (data.RequiresWater)
                    totalWaterDemand += Mathf.Max(0, data.WaterConsumption);

                totalPowerSupply += Mathf.Max(0, data.PowerSupply);
                totalWaterSupply += Mathf.Max(0, data.WaterSupply);
                totalServiceValue += Mathf.Max(0, data.ServiceValue);
            }

            PowerDemand = totalPowerDemand;
            PowerSupply = totalPowerSupply;
            WaterDemand = totalWaterDemand;
            WaterSupply = totalWaterSupply;

            PowerRate = totalPowerDemand <= 0 ? 1f : Mathf.Clamp01((float)totalPowerSupply / totalPowerDemand);
            WaterRate = totalWaterDemand <= 0 ? 1f : Mathf.Clamp01((float)totalWaterSupply / totalWaterDemand);
            OperationRate = Mathf.Min(PowerRate, WaterRate);

            float scoreRatio = (totalServiceValue * OperationRate) / Mathf.Max(1, _serviceScoreAtFull);
            ServiceScore = Mathf.Clamp(Mathf.RoundToInt(scoreRatio * 100f), 0, 100);
            ServiceMultiplier = Mathf.Lerp(_minServiceMultiplier, _maxServiceMultiplier, ServiceScore / 100f);

            GameEventBus.Publish(new UtilityStatusChangedEvent
            {
                PowerDemand = PowerDemand,
                PowerSupply = PowerSupply,
                WaterDemand = WaterDemand,
                WaterSupply = WaterSupply,
                ServiceScore = ServiceScore,
                PowerRate = PowerRate,
                WaterRate = WaterRate,
                OperationRate = OperationRate,
                ServiceMultiplier = ServiceMultiplier
            });

            if (notifyShortage)
                NotifyShortageState();
        }

        private void NotifyShortageState()
        {
            if (!_notifyOnMonthlyShortage) return;

            bool powerShortage = PowerDemand > 0 && PowerRate < 0.999f;
            bool waterShortage = WaterDemand > 0 && WaterRate < 0.999f;

            if (powerShortage == _wasPowerShortage && waterShortage == _wasWaterShortage)
                return;

            if (powerShortage && waterShortage)
            {
                GameEventBus.Publish(new NotificationEvent
                {
                    Message = $"유틸 부족: 전력 {Mathf.RoundToInt(PowerRate * 100f)}%, 수도 {Mathf.RoundToInt(WaterRate * 100f)}%",
                    Type = NotificationType.Warning
                });
            }
            else if (powerShortage)
            {
                GameEventBus.Publish(new NotificationEvent
                {
                    Message = $"전력 부족: 공급률 {Mathf.RoundToInt(PowerRate * 100f)}%",
                    Type = NotificationType.Warning
                });
            }
            else if (waterShortage)
            {
                GameEventBus.Publish(new NotificationEvent
                {
                    Message = $"수도 부족: 공급률 {Mathf.RoundToInt(WaterRate * 100f)}%",
                    Type = NotificationType.Warning
                });
            }
            else
            {
                GameEventBus.Publish(new NotificationEvent
                {
                    Message = "유틸 공급 정상화",
                    Type = NotificationType.Info
                });
            }

            _wasPowerShortage = powerShortage;
            _wasWaterShortage = waterShortage;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using CivilSim.Buildings;
using CivilSim.Core;

namespace CivilSim.Infrastructure
{
    /// <summary>
    /// 전기/수도 공급-수요와 서비스 커버리지를 계산한다.
    /// 서비스는 반경 기반(셀 단위)으로 계산한다.
    /// </summary>
    public class UtilityManager : MonoBehaviour
    {
        [Header("서비스 커버리지")]
        [SerializeField, Min(1)] private int _defaultServiceRadius = 8;
        [SerializeField, Min(1)] private int _serviceValueAtFullCoverage = 60;
        [SerializeField, Range(0f, 1f)] private float _edgeCoverageFactor = 0.25f;

        [Header("서비스 배율")]
        [SerializeField, Range(0.5f, 1.2f)] private float _minEducationMultiplier = 0.90f;
        [SerializeField, Range(0.8f, 1.5f)] private float _maxEducationMultiplier = 1.10f;
        [SerializeField, Range(0.5f, 1.2f)] private float _minHealthcareMultiplier = 0.90f;
        [SerializeField, Range(0.8f, 1.5f)] private float _maxHealthcareMultiplier = 1.10f;
        [SerializeField, Range(0.5f, 1.2f)] private float _minSafetyMultiplier = 0.90f;
        [SerializeField, Range(0.8f, 1.5f)] private float _maxSafetyMultiplier = 1.10f;
        [SerializeField, Range(0.5f, 1.2f)] private float _minSanitationMultiplier = 0.90f;
        [SerializeField, Range(0.8f, 1.5f)] private float _maxSanitationMultiplier = 1.10f;

        [Header("알림")]
        [SerializeField] private bool _notifyOnMonthlyShortage = true;

        public int PowerDemand { get; private set; }
        public int PowerSupply { get; private set; }
        public int WaterDemand { get; private set; }
        public int WaterSupply { get; private set; }
        public int ServiceScore { get; private set; }
        public int EducationScore { get; private set; }
        public int HealthcareScore { get; private set; }
        public int SafetyScore { get; private set; }
        public int SanitationScore { get; private set; }

        public float PowerRate { get; private set; } = 1f;
        public float WaterRate { get; private set; } = 1f;
        public float OperationRate { get; private set; } = 1f;
        public float ServiceMultiplier { get; private set; } = 1f;
        public float EducationMultiplier { get; private set; } = 1f;
        public float HealthcareMultiplier { get; private set; } = 1f;
        public float SafetyMultiplier { get; private set; } = 1f;
        public float SanitationMultiplier { get; private set; } = 1f;
        public float ResidentMultiplier { get; private set; } = 1f;
        public float JobMultiplier { get; private set; } = 1f;
        public float MaintenanceMultiplier { get; private set; } = 1f;

        private BuildingManager _buildings;
        private bool _wasPowerShortage;
        private bool _wasWaterShortage;
        private readonly List<BuildingInstance> _providerCache = new(64);
        private readonly List<BuildingInstance> _targetCache = new(256);

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
            _providerCache.Clear();
            _targetCache.Clear();

            foreach (var kv in _buildings.GetAll())
            {
                BuildingInstance inst = kv.Value;
                BuildingData data = inst?.Data;
                if (data == null) continue;

                if (data.RequiresPower)
                    totalPowerDemand += Mathf.Max(0, data.PowerConsumption);
                if (data.RequiresWater)
                    totalWaterDemand += Mathf.Max(0, data.WaterConsumption);

                totalPowerSupply += Mathf.Max(0, data.PowerSupply);
                totalWaterSupply += Mathf.Max(0, data.WaterSupply);

                _targetCache.Add(inst);
                if (data.ProvidesService && data.ServiceKind != ServiceType.None)
                    _providerCache.Add(inst);
            }

            PowerDemand = totalPowerDemand;
            PowerSupply = totalPowerSupply;
            WaterDemand = totalWaterDemand;
            WaterSupply = totalWaterSupply;

            PowerRate = totalPowerDemand <= 0 ? 1f : Mathf.Clamp01((float)totalPowerSupply / totalPowerDemand);
            WaterRate = totalWaterDemand <= 0 ? 1f : Mathf.Clamp01((float)totalWaterSupply / totalWaterDemand);
            OperationRate = Mathf.Min(PowerRate, WaterRate);

            EducationScore = CalculateServiceScore(ServiceType.Education);
            HealthcareScore = CalculateServiceScore(ServiceType.Healthcare);
            SafetyScore = CalculateServiceScore(ServiceType.Safety);
            SanitationScore = CalculateServiceScore(ServiceType.Sanitation);
            ServiceScore = Mathf.Clamp(
                Mathf.RoundToInt((EducationScore + HealthcareScore + SafetyScore + SanitationScore) * 0.25f),
                0, 100);

            EducationMultiplier = ScoreToMultiplier(EducationScore, _minEducationMultiplier, _maxEducationMultiplier);
            HealthcareMultiplier = ScoreToMultiplier(HealthcareScore, _minHealthcareMultiplier, _maxHealthcareMultiplier);
            SafetyMultiplier = ScoreToMultiplier(SafetyScore, _minSafetyMultiplier, _maxSafetyMultiplier);
            SanitationMultiplier = ScoreToMultiplier(SanitationScore, _minSanitationMultiplier, _maxSanitationMultiplier);

            ResidentMultiplier = Mathf.Clamp((HealthcareMultiplier + SafetyMultiplier) * 0.5f, 0.75f, 1.25f);
            JobMultiplier = Mathf.Clamp((EducationMultiplier + SafetyMultiplier) * 0.5f, 0.75f, 1.25f);
            float sanitationAndSafety = (SanitationMultiplier + SafetyMultiplier) * 0.5f;
            MaintenanceMultiplier = Mathf.Clamp(2f - sanitationAndSafety, 0.75f, 1.25f);
            ServiceMultiplier = Mathf.Clamp((ResidentMultiplier + JobMultiplier) * 0.5f, 0.75f, 1.25f);

            GameEventBus.Publish(new UtilityStatusChangedEvent
            {
                PowerDemand = PowerDemand,
                PowerSupply = PowerSupply,
                WaterDemand = WaterDemand,
                WaterSupply = WaterSupply,
                ServiceScore = ServiceScore,
                EducationScore = EducationScore,
                HealthcareScore = HealthcareScore,
                SafetyScore = SafetyScore,
                SanitationScore = SanitationScore,
                PowerRate = PowerRate,
                WaterRate = WaterRate,
                OperationRate = OperationRate,
                ServiceMultiplier = ServiceMultiplier,
                EducationMultiplier = EducationMultiplier,
                HealthcareMultiplier = HealthcareMultiplier,
                SafetyMultiplier = SafetyMultiplier,
                SanitationMultiplier = SanitationMultiplier,
                ResidentMultiplier = ResidentMultiplier,
                JobMultiplier = JobMultiplier,
                MaintenanceMultiplier = MaintenanceMultiplier
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

        private int CalculateServiceScore(ServiceType type)
        {
            float totalWeight = 0f;
            float coveredWeight = 0f;
            float neededValue = Mathf.Max(1f, _serviceValueAtFullCoverage);
            float operation = OperationRate;

            for (int i = 0; i < _targetCache.Count; i++)
            {
                BuildingInstance target = _targetCache[i];
                BuildingData targetData = target?.Data;
                if (targetData == null || !IsTargetForService(type, targetData)) continue;

                float targetWeight = GetTargetWeight(type, targetData);
                if (targetWeight <= 0f) continue;

                totalWeight += targetWeight;
                float providedValue = GetServiceValueAtTarget(type, target);
                float targetCoverage = Mathf.Clamp01((providedValue * operation) / neededValue);
                coveredWeight += targetCoverage * targetWeight;
            }

            if (totalWeight <= 0f)
                return 100;

            float ratio = Mathf.Clamp01(coveredWeight / totalWeight);
            return Mathf.RoundToInt(ratio * 100f);
        }

        private float GetServiceValueAtTarget(ServiceType type, BuildingInstance target)
        {
            Vector2 targetCenter = GetCenterCell(target);
            float totalValue = 0f;

            for (int i = 0; i < _providerCache.Count; i++)
            {
                BuildingInstance provider = _providerCache[i];
                BuildingData providerData = provider?.Data;
                if (providerData == null) continue;
                if (providerData.ServiceKind != type || providerData.ServiceValue <= 0) continue;

                int radius = providerData.ServiceRadius > 0 ? providerData.ServiceRadius : _defaultServiceRadius;
                if (radius <= 0) continue;

                Vector2 providerCenter = GetCenterCell(provider);
                float distance = Vector2.Distance(providerCenter, targetCenter);
                if (distance > radius) continue;

                float normalized = 1f - (distance / Mathf.Max(1f, radius));
                float falloff = Mathf.Lerp(_edgeCoverageFactor, 1f, Mathf.Clamp01(normalized));
                totalValue += providerData.ServiceValue * falloff;
            }

            return totalValue;
        }

        private static Vector2 GetCenterCell(BuildingInstance inst)
        {
            Vector2Int size = inst.EffectiveSize;
            return new Vector2(
                inst.GridOrigin.x + (size.x - 1) * 0.5f,
                inst.GridOrigin.y + (size.y - 1) * 0.5f);
        }

        private static bool IsTargetForService(ServiceType type, BuildingData data)
        {
            switch (type)
            {
                case ServiceType.Education:
                    return data.ResidentCapacity > 0 || data.JobCapacity > 0;
                case ServiceType.Healthcare:
                    return data.ResidentCapacity > 0;
                case ServiceType.Safety:
                    return data.Category != BuildingCategory.Infrastructure;
                case ServiceType.Sanitation:
                    return data.Category == BuildingCategory.Residential ||
                           data.Category == BuildingCategory.Commercial ||
                           data.Category == BuildingCategory.Industrial ||
                           data.Category == BuildingCategory.Public;
                default:
                    return false;
            }
        }

        private static float GetTargetWeight(ServiceType type, BuildingData data)
        {
            switch (type)
            {
                case ServiceType.Education:
                    return Mathf.Max(1f, data.ResidentCapacity + data.JobCapacity * 0.75f);
                case ServiceType.Healthcare:
                    return Mathf.Max(1f, data.ResidentCapacity);
                case ServiceType.Safety:
                    return Mathf.Max(1f, data.ResidentCapacity * 0.6f + data.JobCapacity * 0.6f + data.SizeX * data.SizeZ * 2f);
                case ServiceType.Sanitation:
                    float industrialBonus = data.Category == BuildingCategory.Industrial ? 20f : 0f;
                    return Mathf.Max(1f, data.ResidentCapacity * 0.5f + data.JobCapacity * 0.8f + industrialBonus);
                default:
                    return 0f;
            }
        }

        private static float ScoreToMultiplier(int score, float min, float max)
        {
            return Mathf.Lerp(min, max, Mathf.Clamp01(score / 100f));
        }
    }
}

using UnityEngine;
using CivilSim.Buildings;
using CivilSim.Core;
using CivilSim.Infrastructure;

namespace CivilSim.Population
{
    /// <summary>
    /// 도시 인구/고용/수요(RCI)를 경량 수식으로 계산한다.
    /// 자동 개발은 하지 않고, HUD/알림용 수치만 제공한다.
    /// </summary>
    public class CityDemandSystem : MonoBehaviour
    {
        [Header("수요 스케일 (인구 대비)")]
        [SerializeField, Range(0.05f, 1.0f)] private float _commercialDemandFactor = 0.25f;
        [SerializeField, Range(0.05f, 1.0f)] private float _industrialDemandFactor = 0.20f;
        [SerializeField, Range(1, 50)] private int _normalizationDivisor = 5;

        [Header("수요 변동 알림")]
        [Tooltip("이 값 이상 수요가 바뀔 때만 플레이어에게 알림을 보냄 (매달 스팸 방지)")]
        [SerializeField, Min(1)] private int _demandNotifyThreshold = 20;

        public int Residents { get; private set; }
        public int JobsTotal { get; private set; }
        public int ResidentialDemand { get; private set; }
        public int CommercialDemand { get; private set; }
        public int IndustrialDemand { get; private set; }
        public float CommercialDemandFactor => _commercialDemandFactorRuntime;
        public float IndustrialDemandFactor => _industrialDemandFactorRuntime;

        private BuildingManager _buildings;
        private UtilityManager _utility;
        private int _lastPopulation;
        private float _commercialDemandFactorRuntime;
        private float _industrialDemandFactorRuntime;

        // 마지막 알림 시 수요값 — 변동 감지에 사용
        private int _lastNotifiedResDemand;
        private int _lastNotifiedComDemand;
        private int _lastNotifiedIndDemand;

        private void Awake()
        {
            _commercialDemandFactorRuntime = Mathf.Clamp(_commercialDemandFactor, 0.05f, 1.0f);
            _industrialDemandFactorRuntime = Mathf.Clamp(_industrialDemandFactor, 0.05f, 1.0f);
        }

        private void Start()
        {
            _buildings = GameManager.Instance?.Buildings;
            _utility = GameManager.Instance?.Utility;
            RecalculateAndPublish();
        }

        private void OnEnable()
        {
            GameEventBus.Subscribe<BuildingPlacedEvent>(OnBuildingChanged);
            GameEventBus.Subscribe<BuildingRemovedEvent>(OnBuildingChanged);
            GameEventBus.Subscribe<RoadRemovedEvent>(OnRoadRemoved);
            GameEventBus.Subscribe<MonthlyTickEvent>(OnMonthlyTick);
        }

        private void OnDisable()
        {
            GameEventBus.Unsubscribe<BuildingPlacedEvent>(OnBuildingChanged);
            GameEventBus.Unsubscribe<BuildingRemovedEvent>(OnBuildingChanged);
            GameEventBus.Unsubscribe<RoadRemovedEvent>(OnRoadRemoved);
            GameEventBus.Unsubscribe<MonthlyTickEvent>(OnMonthlyTick);
        }

        private void OnBuildingChanged(BuildingPlacedEvent e) => RecalculateAndPublish();
        private void OnBuildingChanged(BuildingRemovedEvent e) => RecalculateAndPublish();
        private void OnRoadRemoved(RoadRemovedEvent e) => RecalculateAndPublish();
        private void OnMonthlyTick(MonthlyTickEvent e)
        {
            RecalculateAndPublish();

            // 임계값 이상 변동 시에만 알림 (매달 스팸 방지)
            bool significantChange =
                Mathf.Abs(ResidentialDemand - _lastNotifiedResDemand) >= _demandNotifyThreshold ||
                Mathf.Abs(CommercialDemand  - _lastNotifiedComDemand)  >= _demandNotifyThreshold ||
                Mathf.Abs(IndustrialDemand  - _lastNotifiedIndDemand)  >= _demandNotifyThreshold;

            if (significantChange)
            {
                GameEventBus.Publish(new NotificationEvent
                {
                    Message = $"수요 변동 R:{ResidentialDemand:+#;-#;0} C:{CommercialDemand:+#;-#;0} I:{IndustrialDemand:+#;-#;0}",
                    Type = NotificationType.Info
                });
                _lastNotifiedResDemand = ResidentialDemand;
                _lastNotifiedComDemand = CommercialDemand;
                _lastNotifiedIndDemand = IndustrialDemand;
            }
        }

        private void RecalculateAndPublish()
        {
            if (_buildings == null) _buildings = GameManager.Instance?.Buildings;
            if (_buildings == null) return;

            if (_utility == null) _utility = GameManager.Instance?.Utility;

            int residentsRaw = 0;
            int commercialJobsRaw = 0;
            int industrialJobsRaw = 0;
            int totalJobsRaw = 0;

            foreach (var kv in _buildings.GetAll())
            {
                var inst = kv.Value;
                var data = inst?.Data;
                if (data == null) continue;

                residentsRaw += data.ResidentCapacity;
                totalJobsRaw += data.JobCapacity;

                if (data.Category == BuildingCategory.Commercial)
                    commercialJobsRaw += data.JobCapacity;
                else if (data.Category == BuildingCategory.Industrial)
                    industrialJobsRaw += data.JobCapacity;
            }

            float operationRate = _utility != null ? _utility.OperationRate : 1f;
            float residentMultiplier = _utility != null ? _utility.ResidentMultiplier : 1f;
            float jobMultiplier = _utility != null ? _utility.JobMultiplier : 1f;

            int residents = Mathf.RoundToInt(residentsRaw * operationRate * residentMultiplier);
            int totalJobs = Mathf.RoundToInt(totalJobsRaw * operationRate * jobMultiplier);
            int commercialJobs = Mathf.RoundToInt(commercialJobsRaw * operationRate * jobMultiplier);
            int industrialJobs = Mathf.RoundToInt(industrialJobsRaw * operationRate * jobMultiplier);

            Residents = residents;
            JobsTotal = totalJobs;

            int rawResidential = totalJobs - residents;
            int rawCommercial = Mathf.RoundToInt(residents * _commercialDemandFactorRuntime) - commercialJobs;
            int rawIndustrial = Mathf.RoundToInt(residents * _industrialDemandFactorRuntime) - industrialJobs;

            int div = Mathf.Max(1, _normalizationDivisor);
            ResidentialDemand = Mathf.Clamp(rawResidential / div, -100, 100);
            CommercialDemand = Mathf.Clamp(rawCommercial / div, -100, 100);
            IndustrialDemand = Mathf.Clamp(rawIndustrial / div, -100, 100);

            int delta = residents - _lastPopulation;
            _lastPopulation = residents;

            GameEventBus.Publish(new PopulationChangedEvent
            {
                NewPopulation = residents,
                Delta = delta
            });

            GameEventBus.Publish(new DemandChangedEvent
            {
                ResidentialDemand = ResidentialDemand,
                CommercialDemand = CommercialDemand,
                IndustrialDemand = IndustrialDemand
            });
        }

        public void SetCommercialDemandFactor(float value)
        {
            _commercialDemandFactorRuntime = Mathf.Clamp(value, 0.05f, 1.0f);
            RecalculateAndPublish();
        }

        public void SetIndustrialDemandFactor(float value)
        {
            _industrialDemandFactorRuntime = Mathf.Clamp(value, 0.05f, 1.0f);
            RecalculateAndPublish();
        }
    }
}

using UnityEngine;
using CivilSim.Buildings;
using CivilSim.Core;

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

        [Header("디버그/확인용")]
        [SerializeField] private bool _notifyMonthlyDemandUpdate = true;

        public int Residents { get; private set; }
        public int JobsTotal { get; private set; }
        public int ResidentialDemand { get; private set; }
        public int CommercialDemand { get; private set; }
        public int IndustrialDemand { get; private set; }
        public float CommercialDemandFactor => _commercialDemandFactorRuntime;
        public float IndustrialDemandFactor => _industrialDemandFactorRuntime;

        private BuildingManager _buildings;
        private int _lastPopulation;
        private float _commercialDemandFactorRuntime;
        private float _industrialDemandFactorRuntime;

        private void Awake()
        {
            _commercialDemandFactorRuntime = Mathf.Clamp(_commercialDemandFactor, 0.05f, 1.0f);
            _industrialDemandFactorRuntime = Mathf.Clamp(_industrialDemandFactor, 0.05f, 1.0f);
        }

        private void Start()
        {
            _buildings = GameManager.Instance?.Buildings;
            RecalculateAndPublish();
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

        private void OnBuildingChanged(BuildingPlacedEvent e) => RecalculateAndPublish();
        private void OnBuildingChanged(BuildingRemovedEvent e) => RecalculateAndPublish();
        private void OnMonthlyTick(MonthlyTickEvent e)
        {
            RecalculateAndPublish();

            if (_notifyMonthlyDemandUpdate)
            {
                GameEventBus.Publish(new NotificationEvent
                {
                    Message = $"[{e.Year}/{e.Month:D2}] 수요 갱신 R:{ResidentialDemand:+#;-#;0} C:{CommercialDemand:+#;-#;0} I:{IndustrialDemand:+#;-#;0}",
                    Type = NotificationType.Info
                });
            }
        }

        private void RecalculateAndPublish()
        {
            if (_buildings == null) _buildings = GameManager.Instance?.Buildings;
            if (_buildings == null) return;

            int residents = 0;
            int commercialJobs = 0;
            int industrialJobs = 0;
            int totalJobs = 0;

            foreach (var kv in _buildings.GetAll())
            {
                var inst = kv.Value;
                var data = inst?.Data;
                if (data == null) continue;

                residents += data.ResidentCapacity;
                totalJobs += data.JobCapacity;

                if (data.Category == BuildingCategory.Commercial)
                    commercialJobs += data.JobCapacity;
                else if (data.Category == BuildingCategory.Industrial)
                    industrialJobs += data.JobCapacity;
            }

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

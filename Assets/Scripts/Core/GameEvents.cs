using UnityEngine;
using CivilSim.Grid;

namespace CivilSim.Core
{
    // --
    // 시간 / 틱
    // --

    public struct TickEvent
    {
        public int Day;
        public int Month;
        public int Year;
    }

    public struct DailyTickEvent
    {
        public int Day;
        public int Month;
        public int Year;
    }

    public struct MonthlyTickEvent
    {
        public int Month;
        public int Year;
    }

    public struct TimeSpeedChangedEvent
    {
        public TimeSpeed Speed;
    }

    // --
    // 경제
    // --

    public struct MoneyChangedEvent
    {
        public int NewAmount;
        public int Delta;
    }

    public struct BudgetReportEvent
    {
        public int BaseIncome;
        public int ResidentBaseIncome;
        public int JobBaseIncome;
        public int Income;
        public int ResidentIncome;
        public int JobIncome;
        public int BaseExpenditure;
        public int Expenditure;
        public int Balance;
        public int Month;
        public int Year;
        public float IncomeMultiplier;
        public float ServiceMultiplier;
        public float ResidentMultiplier;
        public float JobMultiplier;
        public float MaintenanceMultiplier;
        public float EducationMultiplier;
        public float HealthcareMultiplier;
        public float SafetyMultiplier;
        public float SanitationMultiplier;
        public float PowerRate;
        public float WaterRate;
        public float OperationRate;
        public int PowerDemand;
        public int PowerSupply;
        public int WaterDemand;
        public int WaterSupply;
        public int ServiceScore;
        public int EducationScore;
        public int HealthcareScore;
        public int SafetyScore;
        public int SanitationScore;
    }

    public struct GoalProgressEvent
    {
        public int TargetPopulation;
        public int CurrentPopulation;
        public int TargetBalance;
        public int CurrentBalance;
        public bool UseBalanceGoal;
    }

    // --
    // 인구
    // --

    public struct PopulationChangedEvent
    {
        public int NewPopulation;
        public int Delta;
    }

    public struct HappinessChangedEvent
    {
        public float NewHappiness; // 0~100
    }

    public struct DemandChangedEvent
    {
        public int ResidentialDemand; // -100 ~ 100
        public int CommercialDemand;  // -100 ~ 100
        public int IndustrialDemand;  // -100 ~ 100
    }

    // --
    // 건물
    // --

    public struct BuildingPlacedEvent
    {
        public Vector2Int GridPosition;
        public int BuildingDataId;
    }

    public struct BuildingRemovedEvent
    {
        public Vector2Int GridPosition;
        public int BuildingDataId;
    }

    // --
    // 도로
    // --

    public struct RoadBuiltEvent
    {
        public Vector2Int GridPosition;
    }

    public struct RoadRemovedEvent
    {
        public Vector2Int GridPosition;
    }

    // --
    // 유틸리티
    // --

    public struct UtilityStatusChangedEvent
    {
        public int PowerDemand;
        public int PowerSupply;
        public int WaterDemand;
        public int WaterSupply;
        public int ServiceScore;
        public int EducationScore;
        public int HealthcareScore;
        public int SafetyScore;
        public int SanitationScore;
        public float PowerRate;
        public float WaterRate;
        public float OperationRate;
        public float ServiceMultiplier;
        public float EducationMultiplier;
        public float HealthcareMultiplier;
        public float SafetyMultiplier;
        public float SanitationMultiplier;
        public float ResidentMultiplier;
        public float JobMultiplier;
        public float MaintenanceMultiplier;
    }

    // --
    // 구역
    // --

    public struct ZonedEvent
    {
        public Vector2Int GridPosition;
        public ZoneType ZoneType;
    }

    // --
    // 알림
    // --

    public enum NotificationType { Info, Warning, Alert }

    public struct NotificationEvent
    {
        public string Message;
        public NotificationType Type;
    }

    // --
    // 게임 상태
    // --

    public struct GameStartedEvent { }
    public struct GameSavedEvent { public string SlotName; }
    public struct GameLoadedEvent { public string SlotName; }

    public struct GameWonEvent
    {
        public string Reason;
        public int Month;
        public int Year;
    }

    public struct GameLostEvent
    {
        public string Reason;
        public int Month;
        public int Year;
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using CivilSim.Buildings;
using CivilSim.Economy;
using CivilSim.Grid;
using CivilSim.Infrastructure;
using CivilSim.Population;

namespace CivilSim.Core
{
    /// <summary>
    /// 저장/불러오기 1차 구현.
    /// 저장 범위: 시간, 경제, 정책값, 건물, 도로, 지반, 구역.
    /// </summary>
    public class SaveLoadManager : MonoBehaviour
    {
        [Serializable]
        public struct SaveSlotInfo
        {
            public string SlotName;
            public int Version;
            public long SavedAtUtcTicks;
            public int Year;
            public int Month;
            public int Day;
            public int Money;
        }

        [Header("저장 슬롯")]
        [SerializeField] private string _defaultSlotName = "quick";

        [Header("단축키")]
        [SerializeField] private bool _useKeyboardShortcut = false;
        [SerializeField] private Key _saveKey = Key.F5;
        [SerializeField] private Key _loadKey = Key.F9;

        [Header("알림")]
        [SerializeField] private bool _notifySuccess = true;

        private const int SaveVersion = 1;

        private GameManager _gameManager;

        private void Awake()
        {
            _gameManager = GameManager.Instance;
        }

        private void Start()
        {
            if (_gameManager == null)
                _gameManager = GameManager.Instance;

            if (GameStartContext.ConsumePendingLoadSlot(out string pendingSlot))
                StartCoroutine(LoadPendingSlotRoutine(pendingSlot));
        }

        private void Update()
        {
            if (!_useKeyboardShortcut) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            if (WasPressed(kb, _saveKey))
                Save();
            else if (WasPressed(kb, _loadKey))
                Load();
        }

        public bool Save()
        {
            return SaveToSlot(_defaultSlotName);
        }

        public bool Load()
        {
            return LoadFromSlot(_defaultSlotName);
        }

        public bool SaveToSlot(string slotName)
        {
            if (!TryResolveSystems(out var systems)) return false;

            string normalizedSlot = NormalizeSlotName(slotName);
            string path = GetSavePath(normalizedSlot);

            try
            {
                var saveData = BuildSaveData(systems, normalizedSlot);
                string json = JsonUtility.ToJson(saveData, true);
                File.WriteAllText(path, json);

                GameEventBus.Publish(new GameSavedEvent { SlotName = normalizedSlot });
                if (_notifySuccess)
                {
                    GameEventBus.Publish(new NotificationEvent
                    {
                        Message = $"저장 완료: {normalizedSlot}",
                        Type = NotificationType.Info
                    });
                }

                Debug.Log($"[SaveLoad] 저장 완료: {path}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveLoad] 저장 실패: {ex}");
                GameEventBus.Publish(new NotificationEvent
                {
                    Message = "저장 실패",
                    Type = NotificationType.Warning
                });
                return false;
            }
        }

        public static bool HasAnySaveFiles()
        {
            return GetSaveSlotInfos().Count > 0;
        }

        public static List<SaveSlotInfo> GetSaveSlotInfos()
        {
            var result = new List<SaveSlotInfo>();
            string dir = Application.persistentDataPath;
            if (!Directory.Exists(dir)) return result;

            string[] files = Directory.GetFiles(dir, "civilsim_save_*.json", SearchOption.TopDirectoryOnly);
            foreach (string file in files)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var data = JsonUtility.FromJson<SaveFileData>(json);
                    if (data == null) continue;

                    string slot = string.IsNullOrWhiteSpace(data.SlotName)
                        ? ExtractSlotNameFromPath(file)
                        : NormalizeSlotName(data.SlotName);

                    int year = data.Clock != null ? data.Clock.Year : 1;
                    int month = data.Clock != null ? data.Clock.Month : 1;
                    int day = data.Clock != null ? data.Clock.Day : 1;
                    int money = data.Economy != null ? data.Economy.Money : 0;

                    result.Add(new SaveSlotInfo
                    {
                        SlotName = slot,
                        Version = data.Version,
                        SavedAtUtcTicks = data.SavedAtUtcTicks,
                        Year = year,
                        Month = month,
                        Day = day,
                        Money = money
                    });
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SaveLoad] 저장 파일 읽기 실패: {file} / {ex.Message}");
                }
            }

            result.Sort((a, b) => b.SavedAtUtcTicks.CompareTo(a.SavedAtUtcTicks));
            return result;
        }

        public bool LoadFromSlot(string slotName)
        {
            if (!TryResolveSystems(out var systems)) return false;

            string normalizedSlot = NormalizeSlotName(slotName);
            string path = GetSavePath(normalizedSlot);

            if (!File.Exists(path))
            {
                GameEventBus.Publish(new NotificationEvent
                {
                    Message = $"저장 파일 없음: {normalizedSlot}",
                    Type = NotificationType.Warning
                });
                return false;
            }

            try
            {
                string json = File.ReadAllText(path);
                var saveData = JsonUtility.FromJson<SaveFileData>(json);
                if (saveData == null)
                {
                    Debug.LogWarning("[SaveLoad] 불러오기 실패: 데이터 파싱 결과가 null");
                    return false;
                }

                RestoreFromSaveData(saveData, systems);
                GameEventBus.Publish(new GameLoadedEvent { SlotName = normalizedSlot });

                if (_notifySuccess)
                {
                    GameEventBus.Publish(new NotificationEvent
                    {
                        Message = $"불러오기 완료: {normalizedSlot}",
                        Type = NotificationType.Info
                    });
                }

                Debug.Log($"[SaveLoad] 불러오기 완료: {path}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveLoad] 불러오기 실패: {ex}");
                GameEventBus.Publish(new NotificationEvent
                {
                    Message = "불러오기 실패",
                    Type = NotificationType.Warning
                });
                return false;
            }
        }

        private SaveFileData BuildSaveData(SystemRefs systems, string normalizedSlotName)
        {
            var data = new SaveFileData
            {
                Version = SaveVersion,
                SlotName = normalizedSlotName,
                SavedAtUtcTicks = DateTime.UtcNow.Ticks,
                Clock = new ClockData
                {
                    Year = systems.Clock.Year,
                    Month = systems.Clock.Month,
                    Day = systems.Clock.Day,
                    Speed = (int)systems.Clock.CurrentSpeed
                },
                Economy = new EconomyData
                {
                    Money = systems.Economy.Money,
                    ResidentTaxPerMonth = systems.Economy.ResidentTaxPerMonth,
                    JobTaxPerMonth = systems.Economy.JobTaxPerMonth
                },
                Demand = new DemandData
                {
                    CommercialFactor = systems.Demand != null ? systems.Demand.CommercialDemandFactor : 0.25f,
                    IndustrialFactor = systems.Demand != null ? systems.Demand.IndustrialDemandFactor : 0.20f
                },
                Foundations = new List<CellCoordData>(),
                Roads = new List<CellCoordData>(),
                Zones = new List<ZoneCellData>(),
                Buildings = new List<BuildingSaveData>()
            };

            // 건물
            foreach (var kv in systems.Buildings.GetAll())
            {
                var inst = kv.Value;
                if (inst == null || inst.Data == null) continue;

                data.Buildings.Add(new BuildingSaveData
                {
                    BuildingName = inst.Data.BuildingName,
                    X = inst.GridOrigin.x,
                    Y = inst.GridOrigin.y,
                    Rotation = inst.Rotation
                });
            }

            // 도로
            foreach (var pos in systems.Roads.GetAllRoadPositions())
            {
                data.Roads.Add(new CellCoordData
                {
                    X = pos.x,
                    Y = pos.y
                });
            }

            // 지반/구역
            for (int x = 0; x < systems.Grid.Width; x++)
            {
                for (int y = 0; y < systems.Grid.Height; y++)
                {
                    var cell = systems.Grid.GetCell(x, y);
                    if (cell == null) continue;

                    if (cell.State == CellState.Foundation || cell.State == CellState.Building)
                    {
                        data.Foundations.Add(new CellCoordData
                        {
                            X = x,
                            Y = y
                        });
                    }

                    if (cell.Zone != ZoneType.None)
                    {
                        data.Zones.Add(new ZoneCellData
                        {
                            X = x,
                            Y = y,
                            ZoneType = (int)cell.Zone
                        });
                    }
                }
            }

            return data;
        }

        private void RestoreFromSaveData(SaveFileData data, SystemRefs systems)
        {
            systems.Clock.SetSpeed(TimeSpeed.Paused);

            systems.Buildings.ClearAll();
            systems.Roads.ClearAllRoads();
            systems.Foundation?.ClearAll();
            systems.Grid.ResetAllCells();

            // 지반 복원
            if (data.Foundations != null)
            {
                foreach (var f in data.Foundations)
                {
                    var pos = new Vector2Int(f.X, f.Y);
                    if (!systems.Grid.IsValid(pos)) continue;

                    if (systems.Foundation != null)
                        systems.Foundation.TryPlace(pos);
                    else
                        systems.Grid.PlaceFoundation(pos);
                }
            }

            // 구역 복원
            if (data.Zones != null)
            {
                foreach (var z in data.Zones)
                {
                    var pos = new Vector2Int(z.X, z.Y);
                    if (!systems.Grid.IsValid(pos)) continue;

                    var zoneType = (ZoneType)Mathf.Clamp(z.ZoneType, 0, (int)ZoneType.Industrial);
                    if (zoneType != ZoneType.None)
                        systems.Grid.SetZone(pos, zoneType);
                }
            }

            // 도로 복원
            if (data.Roads != null)
            {
                foreach (var r in data.Roads)
                {
                    var pos = new Vector2Int(r.X, r.Y);
                    if (!systems.Grid.IsValid(pos)) continue;
                    systems.Roads.TryPlaceRoad(pos);
                }
            }

            // 건물 복원
            if (data.Buildings != null)
            {
                foreach (var b in data.Buildings)
                {
                    if (string.IsNullOrWhiteSpace(b.BuildingName)) continue;
                    var buildingData = systems.BuildingDB.GetByName(b.BuildingName);
                    if (buildingData == null)
                    {
                        Debug.LogWarning($"[SaveLoad] BuildingData 누락: {b.BuildingName}");
                        continue;
                    }

                    var pos = new Vector2Int(b.X, b.Y);
                    if (!systems.Grid.IsValid(pos)) continue;
                    systems.Buildings.TryPlace(pos, buildingData, Mathf.Clamp(b.Rotation, 0, 3));
                }
            }

            // 경제/정책값 복원
            if (data.Economy != null)
            {
                systems.Economy.SetResidentTaxPerMonth(data.Economy.ResidentTaxPerMonth);
                systems.Economy.SetJobTaxPerMonth(data.Economy.JobTaxPerMonth);
                systems.Economy.SetMoneyForLoad(data.Economy.Money);
            }

            if (systems.Demand != null && data.Demand != null)
            {
                systems.Demand.SetCommercialDemandFactor(data.Demand.CommercialFactor);
                systems.Demand.SetIndustrialDemandFactor(data.Demand.IndustrialFactor);
            }

            // 날짜/배속 복원
            if (data.Clock != null)
            {
                systems.Clock.SetDate(data.Clock.Year, data.Clock.Month, data.Clock.Day, false);
                systems.Clock.SetSpeed((TimeSpeed)Mathf.Clamp(data.Clock.Speed, 0, (int)TimeSpeed.VeryFast));

                GameEventBus.Publish(new DailyTickEvent
                {
                    Day = systems.Clock.Day,
                    Month = systems.Clock.Month,
                    Year = systems.Clock.Year
                });
            }
        }

        private bool TryResolveSystems(out SystemRefs refsOut)
        {
            refsOut = null;
            if (_gameManager == null)
                _gameManager = GameManager.Instance;
            if (_gameManager == null)
            {
                Debug.LogWarning("[SaveLoad] GameManager를 찾을 수 없습니다.");
                return false;
            }

            var clock = _gameManager.Clock;
            var economy = _gameManager.Economy;
            var demand = _gameManager.Demand;
            var grid = _gameManager.Grid;
            var buildings = _gameManager.Buildings;
            var roads = _gameManager.Roads;
            var foundation = _gameManager.Foundation;
            var buildingDb = _gameManager.BuildingDB;

            bool invalid =
                clock == null ||
                economy == null ||
                grid == null ||
                buildings == null ||
                roads == null ||
                buildingDb == null;

            if (invalid)
            {
                Debug.LogWarning("[SaveLoad] 필수 시스템 참조가 누락되어 저장/불러오기를 수행할 수 없습니다.");
                return false;
            }

            refsOut = new SystemRefs
            {
                Clock = clock,
                Economy = economy,
                Demand = demand,
                Grid = grid,
                Buildings = buildings,
                Roads = roads,
                Foundation = foundation,
                BuildingDB = buildingDb
            };
            return true;
        }

        private static bool WasPressed(Keyboard keyboard, Key key)
        {
            var keyControl = keyboard[key];
            return keyControl != null && keyControl.wasPressedThisFrame;
        }

        private static string NormalizeSlotName(string slotName)
        {
            if (string.IsNullOrWhiteSpace(slotName))
                return "quick";
            return slotName.Trim().ToLowerInvariant();
        }

        private static string GetSavePath(string slotName)
        {
            string fileName = $"civilsim_save_{slotName}.json";
            return Path.Combine(Application.persistentDataPath, fileName);
        }

        private static string ExtractSlotNameFromPath(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            const string prefix = "civilsim_save_";
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return NormalizeSlotName(name.Substring(prefix.Length));
            return NormalizeSlotName(name);
        }

        private IEnumerator LoadPendingSlotRoutine(string slotName)
        {
            // 다른 서브시스템 Start() 완료 이후 로드
            yield return null;
            yield return null;
            LoadFromSlot(slotName);
        }

        [Serializable]
        private class SaveFileData
        {
            public int Version;
            public string SlotName;
            public long SavedAtUtcTicks;
            public ClockData Clock;
            public EconomyData Economy;
            public DemandData Demand;
            public List<CellCoordData> Foundations;
            public List<CellCoordData> Roads;
            public List<ZoneCellData> Zones;
            public List<BuildingSaveData> Buildings;
        }

        [Serializable]
        private class ClockData
        {
            public int Year;
            public int Month;
            public int Day;
            public int Speed;
        }

        [Serializable]
        private class EconomyData
        {
            public int Money;
            public int ResidentTaxPerMonth;
            public int JobTaxPerMonth;
        }

        [Serializable]
        private class DemandData
        {
            public float CommercialFactor;
            public float IndustrialFactor;
        }

        [Serializable]
        private class CellCoordData
        {
            public int X;
            public int Y;
        }

        [Serializable]
        private class ZoneCellData
        {
            public int X;
            public int Y;
            public int ZoneType;
        }

        [Serializable]
        private class BuildingSaveData
        {
            public string BuildingName;
            public int X;
            public int Y;
            public int Rotation;
        }

        private class SystemRefs
        {
            public GameClock Clock;
            public EconomyManager Economy;
            public CityDemandSystem Demand;
            public GridSystem Grid;
            public BuildingManager Buildings;
            public RoadManager Roads;
            public FoundationManager Foundation;
            public BuildingDatabase BuildingDB;
        }
    }
}

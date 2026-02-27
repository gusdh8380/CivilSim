# CLAUDE.md — CivilSim

Unity 6 WebGL 기반 심시티 스타일 토목/건축 시뮬레이터 개발 가이드.

---

## 프로젝트 개요

| 항목 | 값 |
|------|-----|
| Engine | Unity 6000.3.10f1 |
| Build Target | WebGL |
| Render Pipeline | URP 17 |
| Input System | Unity Input System 1.18 |
| 언어 | C# (.NET Standard 2.1) |

---

## 아키텍처 원칙

### 1. 이벤트 기반 설계 (Event-Driven)
시스템 간 직접 참조를 최소화하고, `GameEventBus`를 통해 통신한다.
```csharp
// 이벤트 발행
GameEventBus.Publish(new BuildingPlacedEvent(buildingData, gridPos));
// 이벤트 구독
GameEventBus.Subscribe<BuildingPlacedEvent>(OnBuildingPlaced);
```

### 2. Manager Singleton 패턴
각 서브시스템은 `GameManager`가 초기화하고 참조를 관리한다.
`FindObjectOfType` 사용 금지 — 항상 `GameManager.Instance.{SystemName}` 으로 접근.
```csharp
GameManager.Instance.Economy.AddTax(100);
GameManager.Instance.Grid.PlaceBuilding(pos, data);
```

### 3. ScriptableObject 기반 데이터 분리
건물/구역/유틸리티의 설정값은 모두 `ScriptableObject`로 관리.
런타임 로직과 정적 데이터를 철저히 분리.
```
Assets/Data/
├── Buildings/       # BuildingData.asset
├── Zones/           # ZoneData.asset
└── Economy/         # EconomyConfig.asset
```

### 4. WebGL 제약 엄수
- `Thread` 직접 생성 금지 → `async/await` 또는 코루틴 사용
- `System.IO` 파일 접근 금지 → `PlayerPrefs` 또는 `IndexedDB` (ES6 interop)
- 씬 로딩은 모두 `Addressables` 기반 비동기

---

## 폴더 구조 및 역할

```
Assets/
├── Scenes/
│   ├── Boot.unity          # 초기 로딩, 시스템 초기화
│   ├── MainMenu.unity      # 타이틀 화면
│   └── GamePlay.unity      # 메인 게임 씬
│
├── Scripts/
│   ├── Core/               # 게임 부트스트랩, 이벤트 버스, 유틸
│   │   ├── GameManager.cs
│   │   ├── GameEventBus.cs
│   │   ├── GameClock.cs    # 시간 배속 (1x/2x/4x/일시정지)
│   │   └── SaveLoadSystem.cs
│   │
│   ├── Grid/               # 타일 그리드 핵심 로직
│   │   ├── GridSystem.cs   # 그리드 데이터, 좌표 변환
│   │   ├── GridCell.cs     # 셀 상태 (빈칸/도로/건물/구역)
│   │   └── GridVisualizer.cs
│   │
│   ├── Buildings/          # 건물 배치, 타입, 레벨
│   │   ├── BuildingManager.cs
│   │   ├── BuildingPlacer.cs   # 드래그 배치 미리보기
│   │   ├── BuildingData.cs     # ScriptableObject
│   │   └── BuildingInstance.cs # 런타임 건물 상태
│   │
│   ├── Infrastructure/     # 도로, 전기, 수도
│   │   ├── RoadManager.cs
│   │   ├── RoadBuilder.cs      # 드래그 도로 생성
│   │   ├── UtilityNetwork.cs   # 전기/수도 연결 그래프
│   │   └── TrafficSimulator.cs # 경량 교통 흐름
│   │
│   ├── Economy/
│   │   ├── EconomyManager.cs   # 예산, 세금, 지출
│   │   ├── EconomyConfig.cs    # ScriptableObject
│   │   └── TickSystem.cs       # n초마다 경제 업데이트
│   │
│   ├── Population/
│   │   ├── PopulationManager.cs
│   │   ├── CitizenData.cs
│   │   └── HappinessCalculator.cs
│   │
│   ├── UI/
│   │   ├── HUDController.cs    # 상단 정보바 (자금, 인구, 시간)
│   │   ├── BuildingPanel.cs    # 건물 선택 패널
│   │   ├── CityInfoPanel.cs    # 도시 상세 정보
│   │   ├── TooltipSystem.cs
│   │   └── NotificationSystem.cs
│   │
│   └── Camera/
│       ├── RTSCameraController.cs  # Pan/Zoom/Rotate
│       └── CameraSettings.cs       # ScriptableObject
│
├── Data/                   # ScriptableObject 에셋 보관
│   ├── Buildings/
│   ├── Zones/
│   └── Economy/
│
├── Prefabs/
│   ├── Buildings/
│   ├── Infrastructure/
│   └── UI/
│
├── Materials/
├── Textures/
├── Audio/
│   ├── BGM/
│   └── SFX/
└── UI/
    ├── Icons/
    └── Sprites/
```

---

## 네임스페이스 규칙

```csharp
namespace CivilSim.Core        // GameManager, EventBus, Clock
namespace CivilSim.Grid        // 그리드 시스템
namespace CivilSim.Buildings   // 건물
namespace CivilSim.Infrastructure
namespace CivilSim.Economy
namespace CivilSim.Population
namespace CivilSim.UI
namespace CivilSim.Camera
namespace CivilSim.Data        // ScriptableObject 데이터 클래스
```

---

## 코딩 컨벤션

```csharp
// 클래스명: PascalCase
public class BuildingManager : MonoBehaviour { }

// 공개 프로퍼티: PascalCase
public int Population { get; private set; }

// private 필드: _camelCase
private GridSystem _gridSystem;

// 상수: UPPER_SNAKE_CASE
private const int MAX_GRID_SIZE = 100;

// 이벤트: On + 동사 과거형
public event Action<BuildingData> OnBuildingPlaced;

// ScriptableObject: 클래스명 + Data / Config
[CreateAssetMenu(menuName = "CivilSim/Buildings/BuildingData")]
public class BuildingData : ScriptableObject { }
```

---

## 그리드 시스템 설계

- 그리드 크기: 기본 100×100 (확장 가능)
- 셀 크기: 1 Unity Unit = 1 타일 (10m 기준)
- 좌표계: `Vector2Int` (col, row) — `GridSystem.WorldToGrid()` / `GridToWorld()` 로 변환
- 멀티레이어: Ground / Road / Building / Utility 레이어 분리

```csharp
// 예시: 좌표 변환
Vector3 worldPos = GridSystem.GridToWorld(new Vector2Int(5, 3));
Vector2Int gridPos = GridSystem.WorldToGrid(transform.position);
```

---

## 핵심 이벤트 목록

| 이벤트 | 발행자 | 구독자 |
|--------|--------|--------|
| `BuildingPlacedEvent` | BuildingPlacer | Economy, Population, UI |
| `BuildingRemovedEvent` | BuildingManager | Economy, Population |
| `RoadBuiltEvent` | RoadBuilder | TrafficSimulator, UI |
| `ZonedEvent` | ZoneManager | Population, Economy |
| `TickEvent` | TickSystem | Economy, Population |
| `MoneyChangedEvent` | EconomyManager | HUDController |
| `PopulationChangedEvent` | PopulationManager | HUDController |
| `TimeChangedEvent` | GameClock | TickSystem, UI |

---

## WebGL 최적화 지침

1. **텍스처**: DXT 압축, Atlas 최대 2048×2048
2. **드로우콜**: 건물 인스턴싱(`GPU Instancing`) 필수
3. **오브젝트 풀링**: 건물/도로 Prefab은 반드시 Pool 사용
4. **메모리**: IL2CPP 빌드, GC 압박 최소화 (struct 이벤트 선호)
5. **저장**: `PlayerPrefs` (소용량) / `IndexedDB` via jslib (대용량 세이브)
6. **비동기**: 씬 전환 / 리소스 로딩 모두 `async/await` + `UniTask` 또는 코루틴

---

## 개발 워크플로우

```bash
# 브랜치 전략
main          # 배포 가능 상태 (WebGL 빌드 자동화)
develop       # 통합 개발 브랜치
feature/xxx   # 기능 단위 개발
fix/xxx       # 버그 수정

# 커밋 컨벤션
feat: 그리드 시스템 기초 구현
fix: 건물 배치 시 충돌 오류 수정
refactor: EconomyManager 이벤트 기반으로 리팩터
chore: WebGL 빌드 워크플로우 업데이트
```

---

## 주의사항

- `Update()` 안에서 무거운 연산 금지 — `TickSystem` 또는 코루틴으로 분산
- `Camera.main` 캐싱: 매 프레임 `Camera.main` 호출 금지
- `string` 기반 태그 비교 최소화 → `LayerMask` 또는 컴포넌트 캐싱
- WebGL에서 `Application.Quit()` 동작 안 함 → 처리 불필요
- `Physics.Raycast`는 레이어마스크 필터링 필수 (성능)

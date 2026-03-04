# CivilSim

> Unity 6 WebGL 기반 미니 시티빌더 — 수동 배치 중심의 경량 도시 시뮬레이터

**🌐 플레이:** [civilsim-webgl-20260304-161225.netlify.app](https://civilsim-webgl-20260304-161225.netlify.app)

---

## 소개

CivilSim은 심시티에서 영감을 받은 개인 프로젝트입니다.
자동 건설 없이 플레이어가 **도로, 지반, 건물을 직접 배치**해 도시를 성장시키고, 월별 예산·인구·수요 수치로 즉각적인 피드백을 받는 구조입니다.

운영 난이도는 전기/수도 공급률과 공공 서비스 범위에 따라 도시 전체 운영률이 오르내리는 방식으로 설계했습니다.
유틸리티가 부족해도 즉시 게임 오버가 아닌 **운영률 저하**로 체감되도록 의도했습니다.

---

## 주요 기능

| 시스템 | 내용 |
|--------|------|
| **그리드 기반 배치** | 100×100 타일 그리드, 도로 인접·지반 조건 검사 |
| **자동 도로 타일링** | 16가지 연결 패턴 자동 판별 (직선/코너/T자/교차로) |
| **25종 건물** | 주거 6 · 상업 9 · 공업 4 · 공공 3 · 유틸 3 |
| **월별 경제 정산** | 세수·유지비·수요 배율·서비스 배율·운영률 복합 계산 |
| **전기/수도 시스템** | 전체 공급-수요 풀 방식, OperationRate로 도시 전체 효율 반영 |
| **구역 지정** | 주거(R)/상업(C)/공업(I) 정보 레이어 (수동 배치 보조) |
| **저장/불러오기** | JSON 슬롯 방식, 건물·도로·지반·구역 전체 직렬화 |
| **설정 커스터마이징** | 단축키 리바인딩, 카메라 속도 조정 |
| **WebGL 배포** | Netlify 자동 배포 파이프라인 |

---

## 핵심 게임플레이 루프

```
지반 정비(G) → 도로 배치(F) → 구역 지정(Z) → 건물 수동 배치(B)
                                        ↓
                        월 정산 (예산 / 인구 / 수요 피드백)
                                        ↓
                          정책 조정 · 철거 · 재배치
```

**경제 공식**

```
최종 수입 = BaseIncome × DemandMultiplier × ServiceMultiplier × OperationRate

BaseIncome = (주거 인원 × 주거세) + (고용 인원 × 고용세)
OperationRate = min(PowerRate, WaterRate)   // 0 ~ 1
```

파산 조건: 잔고 −20,000 미만 또는 12개월 연속 적자

---

## 조작 키 (기본값)

| 키 | 기능 |
|----|------|
| `B` | 건물 패널 토글 |
| `F` | 도로 모드 |
| `G` | 지반 모드 |
| `Z` | 구역 모드 |
| `R / C / I / X` | 구역 타입 전환 / 해제 |
| `T` | 건물 회전 |
| `ESC` / `RMB` | 현재 모드 취소 |
| `F5 / F9` | 저장 / 불러오기 |
| `WASD` | 카메라 이동 |
| `휠` | 카메라 줌 |
| `Alt + 드래그` | 카메라 회전 |

> 단축키는 게임 내 설정 패널에서 자유롭게 변경할 수 있습니다.

---

## 기술 스택

| 항목 | 버전 |
|------|------|
| Unity | 6000.3.10f1 |
| Render Pipeline | URP 17 |
| Input System | Unity Input System 1.18 |
| 언어 | C# (.NET Standard 2.1) |
| 빌드 타겟 | WebGL |
| 배포 | Netlify |

---

## 아키텍처

### 이벤트 기반 설계

시스템 간 직접 참조를 배제하고 `GameEventBus`를 통해 통신합니다.
MonoBehaviour 간 의존성 없이 독립적으로 시스템을 테스트·교체할 수 있습니다.

```csharp
// 발행
GameEventBus.Publish(new BuildingPlacedEvent(buildingData, gridPos));

// 구독
GameEventBus.Subscribe<BuildingPlacedEvent>(OnBuildingPlaced);
```

### Manager Singleton 패턴

`GameManager.Instance`가 모든 서브시스템 참조를 중앙 관리합니다.
`FindObjectOfType` 호출 없이 `GameManager.Instance.Economy.AddTax(100)` 형태로 접근합니다.

### ScriptableObject 데이터 분리

건물·구역·경제 설정값은 모두 ScriptableObject 에셋으로 분리했습니다.
런타임 로직을 수정하지 않고 수치를 조정할 수 있습니다.

```
Assets/Data/
├── Buildings/     # BuildingData.asset (25종)
├── Zones/         # ZoneData.asset
└── Economy/       # EconomyConfig.asset
```

### 핵심 이벤트 목록

| 이벤트 | 발행자 | 주요 구독자 |
|--------|--------|------------|
| `BuildingPlacedEvent` | BuildingPlacer | Economy, Population, UI |
| `RoadBuiltEvent` | RoadBuilder | UI |
| `TickEvent` | GameClock | TickSystem |
| `MoneyChangedEvent` | EconomyManager | HUDController |
| `PopulationChangedEvent` | PopulationManager | HUDController |
| `UtilityStatusChangedEvent` | UtilityManager | EconomyManager, UI |
| `GameWonEvent / GameLostEvent` | CityProgressionManager | UI |

---

## 프로젝트 구조

```
Assets/
├── Scenes/
│   ├── Entry.unity          # 타이틀 / 저장 슬롯 선택
│   ├── LoadingScene.unity   # 비동기 씬 전환
│   └── Game Play.unity      # 메인 게임플레이
│
├── Scripts/
│   ├── Core/                # GameManager, EventBus, Clock, Save
│   ├── Grid/                # GridSystem, GridCell, GridVisualizer
│   ├── Buildings/           # BuildingManager, BuildingPlacer, BuildingData
│   ├── Infrastructure/      # RoadManager, RoadBuilder, FoundationManager
│   ├── Economy/             # EconomyManager, EconomyConfig, TickSystem
│   ├── Population/          # CityDemandSystem
│   ├── Zones/               # ZoneManager, ZoneBuilder
│   ├── UI/                  # HUD, 패널, 툴팁, 알림
│   └── Camera/              # RTSCameraController
│
└── Data/                    # ScriptableObject 에셋
    ├── Buildings/
    └── Economy/
```

---

## 에디터 실행

1. Unity Hub에서 프로젝트 열기 (Unity `6000.3.10f1` 필요)
2. `Assets/Scenes/Entry.unity` 열기
3. Play 실행

---

## WebGL 빌드 & 배포

### 빌드

```
Tools/CivilSim/WebGL/Apply Release Settings (1920x1080)
Tools/CivilSim/WebGL/Build Release (1920x1080)
```

> 이 메뉴를 사용해야 WebGL 셸 패치와 레이아웃 설정이 함께 적용됩니다.
> `File → Build Profiles` 기본 빌드는 웹 레이아웃 패치가 누락될 수 있습니다.

출력 경로: `Builds/WebGL`

### Netlify 배포

```bash
npx --yes netlify-cli deploy --dir=Builds/WebGL --prod
```

설정 파일: `netlify.toml` (publish: `Builds/WebGL`)

---

## 라이선스

MIT

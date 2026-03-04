# CivilSim

Unity 6 기반 미니 시티빌더 프로젝트다.  
현재는 `Entry -> LoadingScene -> Game Play` 흐름과 수동 배치 중심 코어 루프를 구현한 상태다.

## 프로젝트 상태

- 엔진: Unity 6 (6000.3.x)
- 렌더링: URP
- 입력: Unity Input System
- 타겟: WebGL

### 현재 구현된 핵심 루프

1. 지반 정비
2. 도로 배치/철거
3. 구역 지정/해제
4. 건물 수동 배치/철거
5. 월 정산(예산/수요/인구 반영)
6. 정책, 월간보고, 게임설정 패널 운영
7. 저장/불러오기(엔트리 슬롯 선택 포함)

## 씬 구성

- `Assets/Scenes/Entry.unity`
  - 새 게임 시작
  - 저장 슬롯 목록 불러오기
- `Assets/Scenes/LoadingScene.unity`
  - 비동기 로딩 UI
- `Assets/Scenes/Game Play.unity`
  - 실제 플레이 씬

Build Settings에는 위 3개 씬이 등록되어 있어야 한다.

## 조작 키

- `B`: 건물 패널 토글
- `F`: 도로 모드 토글
- `G`: 지반 모드 토글
- `Z`: 구역 모드 토글
- `R/C/I/X`: 구역 타입 전환/해제
- `ESC` or `RMB`: 현재 모드 취소

## 데이터 개요

- 건물 데이터: `Assets/Data/Buildings`
- 건물 DB: `Assets/Data/BuildingDatabase.asset`
- 경제 설정: `Assets/Data/Economy/EconomyConfig.asset`

현재 등록 건물은 총 25종이다.

- 주거 6
- 상업 9
- 공업 4
- 공공 3
- 유틸 3

## 문서

- 확정 기획서(v1.1): `MINI_SIMCITY_PLAN.md`
- 장기 로드맵(참고): `ROADMAP.md`

## 실행 방법

1. Unity Hub에서 Unity 6으로 프로젝트를 연다.
2. `File -> Build Profiles`에서 WebGL 프로파일을 선택한다.
3. `Assets/Scenes/Entry.unity`를 열고 Play한다.

## WebGL 릴리즈 빌드

- 메뉴:
  - `Tools/CivilSim/WebGL/Apply Release Settings (1920x1080)`
  - `Tools/CivilSim/WebGL/Build Release (1920x1080)`
- 출력 경로:
  - `Builds/WebGL`
- 상세 배포 가이드:
  - `WEBGL_RELEASE_GUIDE.md`
- Netlify 설정:
  - `netlify.toml` (publish: `Builds/WebGL`)

## 개발 원칙

- 자동 개발(구역 기반 자동 건설) 금지
- 건물은 수동 배치만 허용
- 변경 전 기획 고정, 이후 코드 구현

## 라이선스

MIT

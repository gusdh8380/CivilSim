# CivilSim

Unity 6 기반 미니 시티빌더 프로젝트다.  
핵심 방향은 자동 건설이 아닌 수동 배치 중심 운영이며, 현재 `Entry -> LoadingScene -> Game Play` 3씬 흐름으로 동작한다.

## 1. 프로젝트 개요

- 엔진: Unity 6 (`6000.3.10f1`)
- 렌더링: URP
- 입력: Unity Input System
- 주요 타겟: WebGL
- 현재 배포 URL: `https://civilsim-webgl-20260304-161225.netlify.app`

## 2. 현재 구현 범위

1. 지반 정비
2. 도로 배치/철거
3. 구역 지정/해제
4. 건물 수동 배치/철거
5. 월 정산(예산/수요/인구/운영 상태 반영)
6. 정책, 월간보고, 게임설정 패널
7. 저장/불러오기(엔트리 씬 슬롯 선택 포함)
8. 전기/수도 공급률 기반 운영률 반영

## 3. 씬 구성

- `Assets/Scenes/Entry.unity`
- `Assets/Scenes/LoadingScene.unity`
- `Assets/Scenes/Game Play.unity`

Build Settings에 위 3개 씬이 반드시 등록되어 있어야 한다.

## 4. 조작 키(기본값)

- `B`: 건물 패널 토글
- `F`: 도로 모드 토글
- `G`: 지반 모드 토글
- `Z`: 구역 모드 토글
- `R/C/I/X`: 구역 타입 전환/해제
- `T`: 건물 회전
- `ESC` 또는 `RMB`: 현재 모드 취소

단축키는 게임 설정 패널에서 변경 가능하다.

## 5. 데이터 구성

- 건물 데이터: `Assets/Data/Buildings`
- 건물 DB: `Assets/Data/BuildingDatabase.asset`
- 경제 설정: `Assets/Data/Economy/EconomyConfig.asset`

현재 등록 건물 총 25종:

- 주거 6
- 상업 9
- 공업 4
- 공공 3
- 유틸 3

## 6. 에디터 실행

1. Unity Hub에서 프로젝트를 연다.
2. `Assets/Scenes/Entry.unity`를 연다.
3. Play 실행.

## 7. WebGL 릴리즈 빌드

반드시 아래 메뉴로 빌드할 것:

- `Tools/CivilSim/WebGL/Apply Release Settings (1920x1080)`
- `Tools/CivilSim/WebGL/Build Release (1920x1080)`

출력 경로:

- `Builds/WebGL`

참고:

- 이 메뉴는 WebGL 릴리즈 옵션, 헤더 파일, 웹 셸 패치를 함께 적용한다.
- `File -> Build Profiles` 기본 빌드만 사용하면 웹 레이아웃 패치가 누락될 수 있다.

## 8. 웹 배포(Netlify)

- 설정 파일: `netlify.toml`
- publish 디렉토리: `Builds/WebGL`
- 배포 명령:
  - `npx --yes netlify-cli deploy --dir=Builds/WebGL --prod`

상세 문서:

- `WEBGL_RELEASE_GUIDE.md`

## 9. 문서

- 확정 기획서(v1.1): `MINI_SIMCITY_PLAN.md`
- 건물 아이콘 프롬프트(전체): `ICON_PROMPTS_ALL_BUILDINGS.md`
- 공공/유틸 아이콘 프롬프트: `ICON_PROMPTS_PUBLIC_UTILITY.md`

## 10. 트러블슈팅

- 웹에서 화면 상하가 잘림:
  - `Cmd + Shift + R`로 강력 새로고침
  - WebGL은 반드시 `Tools/CivilSim/WebGL/Build Release` 메뉴로 다시 빌드
  - 재배포 후 배포 페이지가 새 해시 파일을 참조하는지 확인
- 건물 스크롤 영역 밖 아이콘이 보임:
  - `ScrollRect.Viewport`가 올바르게 지정되어 있는지 확인
  - `Scroll View`에 `RectMask2D` 적용, `Content` 오브젝트의 불필요한 `Mask` 비활성화

## 11. 개발 원칙

- 자동 개발(구역 기반 자동 건설) 금지
- 건물은 수동 배치만 허용
- 기획 확정 후 구현

## 12. 라이선스

MIT

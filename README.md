# CivilSim

심시티 스타일의 토목/건축 시뮬레이터 게임. Unity 6 + WebGL.

## 기술 스택

- **Engine**: Unity 6 (6000.x)
- **Build Target**: WebGL
- **Render Pipeline**: Universal Render Pipeline (URP)
- **Input**: Unity Input System

## 게임 개요

플레이어가 도시를 설계하고, 도로·건물·인프라를 배치하여 성장하는 도시를 관리하는 시뮬레이션 게임.

### 핵심 기능 (예정)

- [ ] 그리드 기반 건물 배치 시스템
- [ ] 도로 / 교통 인프라
- [ ] 전기 / 수도 네트워크
- [ ] 인구 및 경제 시스템
- [ ] 구역 지정 (주거 / 상업 / 공업)
- [ ] 토목 시설 (댐, 교량, 터널)

## 프로젝트 구조

```
Assets/
├── Scenes/          # 씬 파일
├── Scripts/
│   ├── Core/        # GameManager, EventSystem
│   ├── Grid/        # 그리드 배치 시스템
│   ├── Buildings/   # 건물 타입 및 배치
│   ├── Infrastructure/ # 도로, 유틸리티
│   ├── Economy/     # 자원 및 경제
│   ├── Population/  # 인구 시스템
│   ├── UI/          # UI 컨트롤러
│   └── Camera/      # 카메라 컨트롤
├── Prefabs/
├── Materials/
├── Textures/
└── Audio/
```

## 시작하기

1. Unity Hub에서 Unity 6 (6000.x)로 이 프로젝트를 연다.
2. Build Settings → WebGL로 플랫폼 전환.
3. `Assets/Scenes/GamePlay.unity`에서 개발 시작.

## GitHub Actions

`main` 브랜치에 push 시 WebGL 빌드가 자동으로 실행됩니다.
빌드 결과물은 Actions 탭 → Artifacts에서 다운로드할 수 있습니다.

### 빌드 Secret 설정 (필요 시)

Repository Settings → Secrets에 다음을 추가:

- `UNITY_LICENSE`
- `UNITY_EMAIL`
- `UNITY_PASSWORD`

## 라이선스

MIT

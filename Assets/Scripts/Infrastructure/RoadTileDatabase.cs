using UnityEngine;

namespace CivilSim.Infrastructure
{
    /// <summary>
    /// 도로 타일 프리팹 항목 하나 (프리팹 + 기본 Y 회전 오프셋).
    /// yRotationOffset 은 모델의 기본 방향 보정용 (0/90/180/270).
    /// </summary>
    [System.Serializable]
    public class RoadTileEntry
    {
        [Tooltip("Pandazole 도로 프리팹 (null 이면 큐브 폴백)")]
        public GameObject prefab;

        [Tooltip("프리팹 고유 기본 Y 회전 보정값 (모델이 어느 방향을 바라보는지에 따라 조정)\n" +
                 "예) 모델이 +X(동) 방향이면 0, -Z(남) 방향이면 90 등")]
        public float yRotationOffset = 0f;
    }

    /// <summary>
    /// 도로 타입 6종(isolated / end / straight / corner / tJunction / cross)에
    /// 프리팹을 매핑하는 ScriptableObject.
    ///
    /// Assets ▸ Create ▸ CivilSim ▸ Infrastructure ▸ RoadTileDatabase
    ///
    /// ┌──────────────────────────────────────────────────────────────────┐
    /// │  Pandazole 권장 매핑                                              │
    /// │  isolated   → Env_Road_Free                                       │
    /// │  end        → Env_Road_End_01  (기본 방향: +Z(북) 쪽이 열림)     │
    /// │  straight   → Env_Road_Straight_02  (기본: 남북 방향)            │
    /// │  corner     → Env_Road_Cornor_01    (기본: 북·동이 열림)         │
    /// │  tJunction  → Env_Road_Side_02      (기본: 북·동·서 열림)        │
    /// │  cross      → Env_Road_Cross_02                                   │
    /// └──────────────────────────────────────────────────────────────────┘
    /// </summary>
    [CreateAssetMenu(
        menuName = "CivilSim/Infrastructure/RoadTileDatabase",
        fileName = "RoadTileDatabase")]
    public class RoadTileDatabase : ScriptableObject
    {
        [Header("도로 타입별 프리팹 설정")]

        [Tooltip("고립 타일 – 인접 도로 없음\nPandazole: Env_Road_Free")]
        public RoadTileEntry isolated;

        [Tooltip("종단 타일 – 1방향만 연결\nPandazole: Env_Road_End_01")]
        public RoadTileEntry end;

        [Tooltip("직선 타일 – 2방향 마주보기\nPandazole: Env_Road_Straight_02")]
        public RoadTileEntry straight;

        [Tooltip("코너 타일 – 2방향 L자\nPandazole: Env_Road_Cornor_01")]
        public RoadTileEntry corner;

        [Tooltip("T자 타일 – 3방향\nPandazole: Env_Road_Side_02")]
        public RoadTileEntry tJunction;

        [Tooltip("십자 타일 – 4방향\nPandazole: Env_Road_Cross_02")]
        public RoadTileEntry cross;
    }
}

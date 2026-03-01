using System;
using UnityEngine;

namespace CivilSim.Grid
{
    public enum CellState
    {
        Empty,       // 아무것도 없음 (지반 없음)
        Foundation,  // 지반 정비됨 (건물 배치 가능)
        Road,        // 도로 점유
        Building,    // 건물 점유
        Zone         // 구역 지정만 됨 (호환용)
    }

    public enum ZoneType
    {
        None,
        Residential,  // 주거
        Commercial,   // 상업
        Industrial    // 공업
    }

    /// <summary>
    /// 그리드의 단일 셀 데이터. GridSystem이 2D 배열로 관리한다.
    /// MonoBehaviour 아님 — 순수 C# 클래스.
    /// </summary>
    [Serializable]
    public class GridCell
    {
        public int Col { get; }
        public int Row { get; }

        // ── 상태 ──────────────────────────────────────────
        public CellState State    { get; set; } = CellState.Empty;
        public ZoneType  Zone     { get; set; } = ZoneType.None;

        // 건물 ID (-1 = 없음)
        public int BuildingId { get; set; } = -1;

        // 유틸리티 연결 여부
        public bool HasPower { get; set; } = false;
        public bool HasWater { get; set; } = false;

        // ── 파생 프로퍼티 ──────────────────────────────────
        public bool IsEmpty           => State == CellState.Empty;
        public bool HasBuilding       => State == CellState.Building;
        public bool HasRoad           => State == CellState.Road;
        public bool HasFoundation     => State == CellState.Foundation || State == CellState.Building;
        public bool IsZoned           => Zone != ZoneType.None;
        /// 건물 배치 가능: 지반이 있고 빈 상태일 때만
        public bool CanBuild          => State == CellState.Foundation;
        /// 지반 설치 가능: 완전히 비어 있거나 Zone일 때
        public bool CanPlaceFoundation => State == CellState.Empty || State == CellState.Zone;

        public Vector2Int Position => new Vector2Int(Col, Row);

        // ── 생성자 ────────────────────────────────────────
        public GridCell(int col, int row)
        {
            Col = col;
            Row = row;
        }

        // ── 조작 ──────────────────────────────────────────
        public void Clear()
        {
            State      = CellState.Empty;
            Zone       = ZoneType.None;
            BuildingId = -1;
            HasPower   = false;
            HasWater   = false;
        }

        public override string ToString()
            => $"GridCell({Col},{Row}) State={State} Zone={Zone} Building={BuildingId}";
    }
}

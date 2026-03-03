using UnityEngine;
using CivilSim.Core;
using CivilSim.Grid;

namespace CivilSim.Zones
{
    /// <summary>
    /// 구역 지정 데이터 적용 담당.
    /// 건물/도로 셀은 구역 지정 대상에서 제외한다.
    /// </summary>
    public class ZoneManager : MonoBehaviour
    {
        private GridSystem _grid;

        private void Start()
        {
            _grid = GameManager.Instance?.Grid;
            if (_grid == null) _grid = FindFirstObjectByType<GridSystem>();

            if (_grid == null)
                Debug.LogError("[ZoneManager] GridSystem을 찾을 수 없습니다.");
        }

        /// <summary>
        /// 직사각형 영역에 구역을 일괄 적용한다.
        /// zoneType이 None이면 구역 해제한다.
        /// </summary>
        public int ApplyRect(Vector2Int start, Vector2Int end, ZoneType zoneType)
        {
            if (_grid == null) return 0;

            int minX = Mathf.Min(start.x, end.x);
            int maxX = Mathf.Max(start.x, end.x);
            int minY = Mathf.Min(start.y, end.y);
            int maxY = Mathf.Max(start.y, end.y);

            int changed = 0;

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    if (!_grid.IsValid(x, y)) continue;

                    var pos = new Vector2Int(x, y);
                    if (TrySetZone(pos, zoneType))
                        changed++;
                }
            }

            if (changed > 0)
                Debug.Log($"[ZoneManager] 구역 적용 완료: {zoneType} {changed}셀");

            return changed;
        }

        private bool TrySetZone(Vector2Int pos, ZoneType zoneType)
        {
            var cell = _grid.GetCell(pos);
            if (cell == null) return false;

            // 도로/건물은 구역 지정 대상 제외
            if (cell.State == CellState.Road || cell.State == CellState.Building)
                return false;

            if (zoneType == ZoneType.None)
            {
                if (cell.Zone == ZoneType.None) return false;

                cell.Zone = ZoneType.None;
                if (cell.State == CellState.Zone)
                    cell.State = CellState.Empty;

                GameEventBus.Publish(new ZonedEvent
                {
                    GridPosition = pos,
                    ZoneType = ZoneType.None
                });
                return true;
            }

            if (cell.Zone == zoneType) return false;

            _grid.SetZone(pos, zoneType);
            GameEventBus.Publish(new ZonedEvent
            {
                GridPosition = pos,
                ZoneType = zoneType
            });
            return true;
        }
    }
}

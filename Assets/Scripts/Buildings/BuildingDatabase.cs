using System.Collections.Generic;
using UnityEngine;

namespace CivilSim.Buildings
{
    /// <summary>
    /// 게임에 등록된 모든 BuildingData의 목록을 보관하는 ScriptableObject.
    /// GameManager Inspector에서 할당한다.
    /// Project → Create → CivilSim → Buildings → BuildingDatabase
    /// </summary>
    [CreateAssetMenu(menuName = "CivilSim/Buildings/BuildingDatabase", fileName = "BuildingDatabase")]
    public class BuildingDatabase : ScriptableObject
    {
        [SerializeField] private List<BuildingData> _buildings = new();

        // ── 캐시 ────────────────────────────────────────────
        private Dictionary<string, BuildingData> _nameCache;

        // ── 공개 API ─────────────────────────────────────────

        public IReadOnlyList<BuildingData> All => _buildings;

        /// 카테고리별 필터링
        public List<BuildingData> GetByCategory(BuildingCategory category)
        {
            var result = new List<BuildingData>();
            foreach (var b in _buildings)
                if (b != null && b.Category == category)
                    result.Add(b);
            return result;
        }

        /// 이름으로 검색 (대소문자 무시)
        public BuildingData GetByName(string buildingName)
        {
            if (_nameCache == null) BuildCache();
            return _nameCache.TryGetValue(buildingName.ToLower(), out var data) ? data : null;
        }

        /// 인덱스로 접근
        public BuildingData GetAt(int index)
            => (index >= 0 && index < _buildings.Count) ? _buildings[index] : null;

        public int Count => _buildings.Count;

        // ── 내부 ─────────────────────────────────────────────

        private void BuildCache()
        {
            _nameCache = new Dictionary<string, BuildingData>();
            foreach (var b in _buildings)
                if (b != null && !string.IsNullOrEmpty(b.BuildingName))
                    _nameCache[b.BuildingName.ToLower()] = b;
        }

        private void OnValidate()
        {
            _nameCache = null; // 에디터에서 변경 시 캐시 무효화
        }
    }
}

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using CivilSim.Buildings;

namespace CivilSim.Editor
{
    /// <summary>
    /// Pandazole City Town Pack 프리팹으로 BuildingData SO를 일괄 생성하는 에디터 도구.
    ///
    /// 메뉴: Tools ▸ CivilSim ▸ Generate Pandazole BuildingData
    ///
    /// 동작:
    ///  1. Pandazole 빌딩 프리팹을 이름 기반으로 분류 (ResidentBuilding / CommercialBuilding 등)
    ///  2. 프리팹의 Renderer.bounds 에서 XZ 크기를 읽어 셀 수 자동 계산 (CellSize=10 기준)
    ///  3. 카테고리별 기본 비용·인구·고용 수치 자동 설정
    ///  4. Assets/Data/Buildings/Pandazole/ 에 BuildingData .asset 파일 저장
    ///  5. BuildingDatabase.asset 이 있으면 All 리스트에 자동 추가
    /// </summary>
    public class PandazoleBuildingDataGenerator : UnityEditor.Editor
    {
        // ── 설정 상수 ──────────────────────────────────────────────
        private const string PrefabRoot   = "Assets/Pandazole_Ultimate_Pack/Pandazole City Town Pack/Prefabs";
        private const string OutputFolder = "Assets/Data/Buildings/Pandazole";
        private const string DatabasePath = "Assets/Data/BuildingDatabase.asset";
        private const float  CellSize     = 10f;   // GridSystem._cellSize

        // ── 메뉴 진입점 ───────────────────────────────────────────

        [MenuItem("Tools/CivilSim/Generate Pandazole BuildingData")]
        public static void GenerateAll()
        {
            // 출력 폴더 생성
            if (!AssetDatabase.IsValidFolder(OutputFolder))
            {
                AssetDatabase.CreateFolder("Assets/Data/Buildings", "Pandazole");
                AssetDatabase.Refresh();
            }

            var prefabs = FindBuildingPrefabs();
            if (prefabs.Count == 0)
            {
                EditorUtility.DisplayDialog("오류", $"Pandazole 빌딩 프리팹을 찾을 수 없습니다.\n경로 확인: {PrefabRoot}", "확인");
                return;
            }

            // BuildingDatabase 로드 (있으면)
            var database = AssetDatabase.LoadAssetAtPath<BuildingDatabase>(DatabasePath);

            int created = 0, skipped = 0;
            AssetDatabase.StartAssetEditing();

            try
            {
                foreach (var info in prefabs)
                {
                    string assetPath = $"{OutputFolder}/BD_{info.assetName}.asset";

                    // 이미 존재하면 건너뜀
                    if (AssetDatabase.LoadAssetAtPath<BuildingData>(assetPath) != null)
                    {
                        skipped++;
                        continue;
                    }

                    var data = ScriptableObject.CreateInstance<BuildingData>();
                    PopulateData(data, info);

                    AssetDatabase.CreateAsset(data, assetPath);

                    // Database에 추가
                    if (database != null)
                        AddToDatabase(database, data);

                    created++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            // Database dirty 표시
            if (database != null)
                EditorUtility.SetDirty(database);

            EditorUtility.DisplayDialog(
                "완료",
                $"BuildingData 생성 완료!\n생성: {created}개 / 스킵(이미 존재): {skipped}개\n저장 위치: {OutputFolder}",
                "확인");

            Debug.Log($"[PandazoleGenerator] 완료 — 생성: {created}, 스킵: {skipped}");
        }

        // ── 프리팹 탐색 ────────────────────────────────────────────

        private static List<PrefabInfo> FindBuildingPrefabs()
        {
            var result = new List<PrefabInfo>();

            // 분류 규칙: (이름 접두어, 카테고리, 레이블)
            var rules = new (string prefix, BuildingCategory cat, string label)[]
            {
                ("Env_ResidentBuilding_",   BuildingCategory.Residential, "주거 건물"),
                ("Env_CommercialBuilding_", BuildingCategory.Commercial,  "상업 건물"),
                ("Env_CompanyBuilding_",    BuildingCategory.Industrial,  "회사 건물"),
                ("Env_Motel_",             BuildingCategory.Commercial,  "모텔"),
            };

            // Pandazole Prefabs 폴더에서 모든 prefab GUID 수집
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot });

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileNameWithoutExtension(path);

                // Demo 폴더 제외
                if (path.Contains("/Demo/")) continue;

                foreach (var (prefix, cat, label) in rules)
                {
                    if (!fileName.StartsWith(prefix)) continue;

                    // 번호 추출 (Env_ResidentBuilding_01 → "01")
                    string num = fileName.Substring(prefix.Length);

                    // 프리팹 로드해서 bounds 계산
                    var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    Vector2Int cellSize = CalcCellSize(prefabAsset);

                    result.Add(new PrefabInfo
                    {
                        prefabPath = path,
                        assetName  = fileName,
                        label      = $"{label} {num}",
                        category   = cat,
                        cellSizeX  = cellSize.x,
                        cellSizeZ  = cellSize.y,
                    });
                    break;
                }
            }

            // 이름순 정렬
            result.Sort((a, b) => string.Compare(a.assetName, b.assetName, System.StringComparison.Ordinal));
            return result;
        }

        // ── 셀 크기 자동 계산 ─────────────────────────────────────

        /// <summary>
        /// 프리팹의 Renderer 합산 Bounds에서 XZ 크기를 읽어
        /// CellSize 단위로 몇 셀인지 계산한다. (최소 1, 최대 4)
        /// </summary>
        private static Vector2Int CalcCellSize(GameObject prefab)
        {
            if (prefab == null) return Vector2Int.one;

            // 임시 인스턴스로 bounds 계산 (씬에 추가하지 않음)
            var instance  = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var renderers = instance.GetComponentsInChildren<Renderer>();

            if (renderers.Length == 0)
            {
                DestroyImmediate(instance);
                return Vector2Int.one;
            }

            Bounds combined = renderers[0].bounds;
            foreach (var r in renderers)
                combined.Encapsulate(r.bounds);

            DestroyImmediate(instance);

            float sizeX = combined.size.x;
            float sizeZ = combined.size.z;

            int cellX = Mathf.Max(1, Mathf.RoundToInt(sizeX / CellSize));
            int cellZ = Mathf.Max(1, Mathf.RoundToInt(sizeZ / CellSize));

            // 4×4 이상은 4로 제한
            cellX = Mathf.Min(cellX, 4);
            cellZ = Mathf.Min(cellZ, 4);

            return new Vector2Int(cellX, cellZ);
        }

        // ── BuildingData 채우기 ────────────────────────────────────

        private static void PopulateData(BuildingData data, PrefabInfo info)
        {
            data.BuildingName = info.label;
            data.Category     = info.category;
            data.Description  = $"Pandazole {info.assetName}";
            data.SizeX        = info.cellSizeX;
            data.SizeZ        = info.cellSizeZ;

            int area = info.cellSizeX * info.cellSizeZ;

            // 카테고리별 기본 수치
            switch (info.category)
            {
                case BuildingCategory.Residential:
                    data.BuildCost              = 1000 * area;
                    data.MaintenanceCostPerMonth = 30  * area;
                    data.ResidentCapacity        = 4   * area;
                    data.JobCapacity             = 0;
                    data.PowerConsumption        = 5   * area;
                    data.WaterConsumption        = 8   * area;
                    break;

                case BuildingCategory.Commercial:
                    data.BuildCost              = 2000 * area;
                    data.MaintenanceCostPerMonth = 60  * area;
                    data.ResidentCapacity        = 0;
                    data.JobCapacity             = 5   * area;
                    data.PowerConsumption        = 15  * area;
                    data.WaterConsumption        = 5   * area;
                    break;

                case BuildingCategory.Industrial:
                    data.BuildCost              = 3000 * area;
                    data.MaintenanceCostPerMonth = 100 * area;
                    data.ResidentCapacity        = 0;
                    data.JobCapacity             = 10  * area;
                    data.PowerConsumption        = 30  * area;
                    data.WaterConsumption        = 20  * area;
                    break;

                default:
                    data.BuildCost              = 1000 * area;
                    data.MaintenanceCostPerMonth = 50  * area;
                    break;
            }

            data.RequiresPower = true;
            data.RequiresWater = true;

            // 프리팹 레퍼런스
            data.Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(info.prefabPath);
        }

        // ── BuildingDatabase 추가 ─────────────────────────────────

        private static void AddToDatabase(BuildingDatabase database, BuildingData data)
        {
            // BuildingDatabase의 _allBuildings 리스트를 SerializedObject로 접근
            var so   = new SerializedObject(database);
            var list = so.FindProperty("_buildings");
            if (list == null) return;

            // 중복 체크
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == data)
                    return;
            }

            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = data;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── 내부 데이터 구조 ──────────────────────────────────────

        private class PrefabInfo
        {
            public string          prefabPath;
            public string          assetName;
            public string          label;
            public BuildingCategory category;
            public int             cellSizeX;
            public int             cellSizeZ;
        }
    }
}

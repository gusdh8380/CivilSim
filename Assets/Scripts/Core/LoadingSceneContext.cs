namespace CivilSim.Core
{
    /// <summary>
    /// 로딩 씬에서 실제 목표 씬 이름을 전달받기 위한 컨텍스트.
    /// </summary>
    public static class LoadingSceneContext
    {
        private static string _targetSceneName;

        public static void RequestTargetScene(string sceneName)
        {
            _targetSceneName = string.IsNullOrWhiteSpace(sceneName) ? null : sceneName.Trim();
        }

        public static bool ConsumeTargetScene(out string sceneName)
        {
            sceneName = _targetSceneName;
            _targetSceneName = null;
            return !string.IsNullOrWhiteSpace(sceneName);
        }
    }
}

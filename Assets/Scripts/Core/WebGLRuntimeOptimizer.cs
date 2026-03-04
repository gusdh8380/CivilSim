using UnityEngine;

namespace CivilSim.Core
{
    /// <summary>
    /// Applies a baseline runtime profile for WebGL builds.
    /// </summary>
    public static class WebGLRuntimeOptimizer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
#endif
        }
    }
}

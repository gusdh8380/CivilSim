using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
#if UNITY_2021_2_OR_NEWER
using UnityEditor.Build;
#endif

namespace CivilSim.Editor
{
    /// <summary>
    /// WebGL release build helper:
    /// - apply release settings
    /// - build from enabled scenes
    /// - generate static-hosting header file for .br assets
    /// </summary>
    public static class WebGLReleaseBuilder
    {
        private const int TargetWidth = 1920;
        private const int TargetHeight = 1080;
        private const string DefaultOutputDir = "Builds/WebGL";

        [MenuItem("Tools/CivilSim/WebGL/Apply Release Settings (1920x1080)")]
        public static void ApplyReleaseSettingsMenu()
        {
            ApplyReleaseSettings();
            Debug.Log("[WebGLReleaseBuilder] WebGL release settings applied");
        }

        [MenuItem("Tools/CivilSim/WebGL/Build Release (1920x1080)")]
        public static void BuildReleaseMenu()
        {
            BuildRelease(DefaultOutputDir);
        }

        /// <summary>
        /// CI/CLI entry point.
        /// </summary>
        public static void BuildReleaseCI()
        {
            BuildRelease(DefaultOutputDir);
        }

        private static void BuildRelease(string outputDir)
        {
            ApplyReleaseSettings();

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
                throw new InvalidOperationException("No enabled scenes found in Build Settings");

            Directory.CreateDirectory(outputDir);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
                throw new Exception($"WebGL build failed: {summary.result}");

            ApplyResponsiveDesktopCanvasPatch(outputDir);
            WriteStaticHostingHeaders(outputDir);
            Debug.Log($"[WebGLReleaseBuilder] Build succeeded: {summary.outputPath} ({summary.totalSize / (1024f * 1024f):F2} MB)");
            EditorUtility.RevealInFinder(summary.outputPath);
        }

        private static void ApplyResponsiveDesktopCanvasPatch(string outputDir)
        {
            string indexPath = Path.Combine(outputDir, "index.html");
            if (!File.Exists(indexPath))
                return;

            string html = File.ReadAllText(indexPath);
            const string oldBlock =
@"      } else {
        // Desktop style: Render the game canvas in a window that can be maximized to fullscreen:
        canvas.style.width = ""1920px"";
        canvas.style.height = ""1080px"";
      }";
            const string newBlock =
@"      } else {
        // Desktop style: keep 16:9 and fit into browser viewport.
        const container = document.querySelector(""#unity-container"");
        const targetAspect = 16 / 9;
        function resizeDesktopCanvas() {
          const w = window.innerWidth;
          const h = window.innerHeight;
          let cw = w;
          let ch = Math.round(cw / targetAspect);
          if (ch > h) {
            ch = h;
            cw = Math.round(ch * targetAspect);
          }
          container.style.position = ""fixed"";
          container.style.left = ""50%"";
          container.style.top = ""50%"";
          container.style.transform = ""translate(-50%, -50%)"";
          canvas.style.width = cw + ""px"";
          canvas.style.height = ch + ""px"";
        }
        window.addEventListener(""resize"", resizeDesktopCanvas);
        resizeDesktopCanvas();
      }";

            if (html.Contains(oldBlock))
            {
                html = html.Replace(oldBlock, newBlock);
                File.WriteAllText(indexPath, html);
                Debug.Log("[WebGLReleaseBuilder] Applied responsive desktop canvas patch");
            }
        }

        private static void WriteStaticHostingHeaders(string outputDir)
        {
            // _headers is supported by static hosts like Netlify and Cloudflare Pages.
            string headersPath = Path.Combine(outputDir, "_headers");
            const string headers =
@"/*
  Cache-Control: public, max-age=0, must-revalidate

/Build/*.wasm.br
  Content-Type: application/wasm
  Content-Encoding: br

/Build/*.js.br
  Content-Type: application/javascript
  Content-Encoding: br

/Build/*.data.br
  Content-Type: application/octet-stream
  Content-Encoding: br
";

            File.WriteAllText(headersPath, headers);
        }

        private static void ApplyReleaseSettings()
        {
            PlayerSettings.defaultWebScreenWidth = TargetWidth;
            PlayerSettings.defaultWebScreenHeight = TargetHeight;
            PlayerSettings.runInBackground = true;

            // Build-size and runtime stability defaults for production hosting.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.nameFilesAsHashes = true;

            // Memory defaults (MB): keep initial reasonably small with room to grow.
            PlayerSettings.WebGL.initialMemorySize = 256;
            PlayerSettings.WebGL.maximumMemorySize = 2048;

            PlayerSettings.stripEngineCode = true;

#if UNITY_2021_2_OR_NEWER
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium);
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.WebGL, Il2CppCompilerConfiguration.Master);
#else
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WebGL, ManagedStrippingLevel.Medium);
            PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup.WebGL, Il2CppCompilerConfiguration.Master);
#endif

            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.connectProfiler = false;
            EditorUserBuildSettings.allowDebugging = false;

            AssetDatabase.SaveAssets();
        }
    }
}

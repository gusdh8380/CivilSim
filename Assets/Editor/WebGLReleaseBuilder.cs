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
            const string oldResponsiveBlock =
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
            const string newBlock =
@"      } else {
        // Desktop style: keep 16:9 while reserving area for project info.
        const container = document.querySelector(""#unity-container"");
        const infoPanel = document.querySelector(""#civilsim-project-info"");
        const footer = document.querySelector(""#unity-footer"");
        if (footer) footer.style.display = ""none"";

        const targetAspect = 16 / 9;
        function resizeDesktopCanvas() {
          const margin = 16;
          const infoGap = 12;
          const infoH = infoPanel ? infoPanel.offsetHeight : 0;
          const availW = Math.max(320, window.innerWidth - margin * 2);
          const availH = Math.max(240, window.innerHeight - margin * 2 - infoH - infoGap);

          let cw = availW;
          let ch = Math.round(cw / targetAspect);
          if (ch > availH) {
            ch = availH;
            cw = Math.round(ch * targetAspect);
          }

          const topArea = margin + Math.max(0, (availH - ch) * 0.5);
          container.style.position = ""fixed"";
          container.style.left = ""50%"";
          container.style.top = (topArea + ch * 0.5) + ""px"";
          container.style.transform = ""translate(-50%, -50%)"";
          container.style.width = cw + ""px"";
          container.style.height = ch + ""px"";
          canvas.style.width = cw + ""px"";
          canvas.style.height = ch + ""px"";
        }
        window.addEventListener(""resize"", resizeDesktopCanvas);
        resizeDesktopCanvas();
      }";

            bool changed = false;

            if (html.Contains(oldBlock))
            {
                html = html.Replace(oldBlock, newBlock);
                changed = true;
            }
            else if (html.Contains(oldResponsiveBlock))
            {
                html = html.Replace(oldResponsiveBlock, newBlock);
                changed = true;
            }

            if (!html.Contains("id=\"civilsim-project-info\""))
            {
                const string infoBlock =
@"
    <section id=""civilsim-project-info"">
      <h1>CivilSim</h1>
      <p>Mini city management prototype. Build roads, zones, public services, and utilities while balancing policy and monthly budget.</p>
    </section>
";
                if (html.Contains("    <script>"))
                {
                    html = html.Replace("    <script>", infoBlock + "    <script>");
                    changed = true;
                }
            }

            if (changed)
            {
                File.WriteAllText(indexPath, html);
                Debug.Log("[WebGLReleaseBuilder] Applied web index patch");
            }

            string stylePath = Path.Combine(outputDir, "TemplateData/style.css");
            if (!File.Exists(stylePath))
                return;

            string css = File.ReadAllText(stylePath);
            const string marker = "/* CIVILSIM_WEB_THEME */";
            if (css.Contains(marker))
                return;

            const string themedCss =
@"

/* CIVILSIM_WEB_THEME */
body {
  padding: 0;
  margin: 0;
  background: radial-gradient(120% 140% at 15% 10%, #284a59 0%, #162738 45%, #0d1623 100%);
  color: #e8f5ff;
  font-family: Arial, sans-serif;
  overflow: hidden;
}
#unity-container {
  border-radius: 18px;
  overflow: hidden;
  border: 2px solid rgba(188, 233, 255, 0.28);
  box-shadow: 0 20px 60px rgba(6, 15, 25, 0.5), 0 0 0 1px rgba(188, 233, 255, 0.12);
  background: #0f1b2a;
}
#unity-canvas {
  display: block;
}
#unity-footer {
  display: none;
}
#civilsim-project-info {
  position: fixed;
  left: 50%;
  bottom: 16px;
  transform: translateX(-50%);
  width: min(1100px, calc(100vw - 24px));
  padding: 12px 16px;
  border-radius: 14px;
  background: linear-gradient(135deg, rgba(12, 30, 44, 0.88), rgba(27, 64, 66, 0.78));
  border: 1px solid rgba(188, 233, 255, 0.34);
  box-shadow: 0 10px 30px rgba(7, 16, 27, 0.35);
  color: #e9f7ff;
  backdrop-filter: blur(6px);
}
#civilsim-project-info h1 {
  margin: 0 0 6px;
  font-size: 18px;
  line-height: 1.2;
}
#civilsim-project-info p {
  margin: 0;
  font-size: 13px;
  line-height: 1.45;
  opacity: 0.96;
}
@media (max-width: 900px) {
  #civilsim-project-info {
    display: none;
  }
}
";
            File.WriteAllText(stylePath, css + themedCss);
            Debug.Log("[WebGLReleaseBuilder] Applied web style patch");
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

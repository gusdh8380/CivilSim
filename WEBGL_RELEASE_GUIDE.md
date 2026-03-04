# WebGL Release Guide

## 1) Unity release build (1920x1080)

1. Open Unity project with `6000.3.10f1`.
2. Run menu:
   - `Tools/CivilSim/WebGL/Apply Release Settings (1920x1080)`
   - `Tools/CivilSim/WebGL/Build Release (1920x1080)`
3. Build output:
   - `Builds/WebGL`

Notes:
- Build settings include `Entry -> LoadingScene -> Game Play`.
- Release settings use Brotli compression and hash file names.
- Build output includes `_headers` for static hosting header rules.

## 2) Release profile applied by script

- Resolution: `1920x1080`
- `runInBackground = true`
- WebGL compression: `Brotli`
- Data caching: `On`
- Exception support: `None`
- Decompression fallback: `Off` (server must serve compressed files correctly)
- Initial memory: `256 MB`
- Maximum memory: `2048 MB`
- IL2CPP compiler config: `Master`
- Managed stripping: `Medium`

## 3) Runtime defaults (WebGL only)

- `vSyncCount = 0`
- `Application.targetFrameRate = 60`

## 4) Server config required (important)

When `decompressionFallback = Off`, static hosting must return correct encoding headers.

Set response headers:
- `.wasm.br` -> `Content-Type: application/wasm`, `Content-Encoding: br`
- `.js.br` -> `Content-Type: application/javascript`, `Content-Encoding: br`
- `.data.br` -> `Content-Type: application/octet-stream`, `Content-Encoding: br`

`Builds/WebGL/_headers` is generated automatically for hosts that support this file.
If your host cannot set these headers, set `decompressionFallback = On` and rebuild.

## 5) Deployment checklist

1. Open `Builds/WebGL/index.html` locally to verify first load.
2. Verify start flow: `Entry -> LoadingScene -> Game Play`.
3. Verify save/load and month tick on deployed URL.
4. Check browser console for errors on first run.
5. Test on Chrome and Edge (desktop).

## 6) Recommended deployment targets

- Netlify:
  - Publish directory: `Builds/WebGL`
  - `_headers` is applied automatically.
- Cloudflare Pages:
  - Build command: none
  - Output directory: `Builds/WebGL`
  - `_headers` is applied automatically.
- GitHub Pages:
  - Static hosting works, but custom compression headers are limited.
  - Prefer Netlify or Cloudflare Pages for Brotli `.br` serving.

### Netlify CLI deploy (token auth)

1. Create a personal access token in Netlify.
2. Export environment variables:

```bash
export NETLIFY_AUTH_TOKEN="<your-token>"
export NETLIFY_SITE_ID="<your-site-id>"
```

3. Deploy:

```bash
npx --yes netlify-cli deploy \
  --dir=Builds/WebGL \
  --prod \
  --site="$NETLIFY_SITE_ID" \
  --auth="$NETLIFY_AUTH_TOKEN"
```

## 7) CLI build (optional)

```bash
/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit \
  -projectPath /Users/jeonhyeono/PJ/CivilSim \
  -executeMethod CivilSim.Editor.WebGLReleaseBuilder.BuildReleaseCI \
  -logFile /Users/jeonhyeono/PJ/CivilSim/Logs/webgl_build.log
```

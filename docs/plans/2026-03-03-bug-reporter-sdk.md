# Bug Reporter SDK Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 实现通用 Unity Bug Reporter SDK（`com.joycastle.bugreporter`），任何 Unity 项目两步接入，采集器可插拔扩展。

**Architecture:** Unity Package 结构，核心是 `IInfoCollector` 采集器接口模式。SDK 入口 `BugReporterSDK` 管理采集器注册和生命周期，`ReportUploader` 用 UnityWebRequest 上报，`BuildInfoInjector` 打包时自动注入 git 信息。

**Tech Stack:** Unity 2022.3 / C# / UnityWebRequest / ScriptableObject / IPreprocessBuildWithReport

**项目路径:** `/Users/xiaowangzi/AutoFixBugFrameWork/AutoFixBugFrameWork`

**设计文档:** `/Users/xiaowangzi/Match_Story/Merge_Match/docs/plans/2026-03-03-ai-auto-fix-design.md` Section 3.1

---

### Task 0: 初始化 Git 仓库 & SDK Package 骨架

**Files:**
- Create: `Assets/Packages/com.joycastle.bugreporter/package.json`
- Create: `Assets/Packages/com.joycastle.bugreporter/Runtime/com.joycastle.bugreporter.asmdef`
- Create: `Assets/Packages/com.joycastle.bugreporter/Editor/com.joycastle.bugreporter.Editor.asmdef`

**Step 1: 初始化 git 仓库**

```bash
cd /Users/xiaowangzi/AutoFixBugFrameWork/AutoFixBugFrameWork
git init
```

**Step 2: 创建 .gitignore**

创建 Unity 标准 `.gitignore`（忽略 Library/、Temp/、Logs/、UserSettings/、*.csproj、*.sln 等）。

**Step 3: 创建 SDK Package 目录结构**

```bash
mkdir -p Assets/Packages/com.joycastle.bugreporter/Runtime/Core
mkdir -p Assets/Packages/com.joycastle.bugreporter/Runtime/Collectors
mkdir -p Assets/Packages/com.joycastle.bugreporter/Runtime/UI
mkdir -p Assets/Packages/com.joycastle.bugreporter/Runtime/Config
mkdir -p Assets/Packages/com.joycastle.bugreporter/Editor
mkdir -p Assets/Packages/com.joycastle.bugreporter/Resources
```

**Step 4: 创建 package.json**

```json
{
  "name": "com.joycastle.bugreporter",
  "version": "0.1.0",
  "displayName": "Bug Reporter SDK",
  "description": "通用游戏内 Bug 反馈采集与上报 SDK",
  "unity": "2021.3",
  "author": {
    "name": "JoyCastle"
  },
  "keywords": ["bug", "reporter", "feedback", "QA"]
}
```

**Step 5: 创建 Runtime asmdef**

```json
{
  "name": "com.joycastle.bugreporter",
  "rootNamespace": "JoyCastle.BugReporter",
  "references": [],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false
}
```

**Step 6: 创建 Editor asmdef**

```json
{
  "name": "com.joycastle.bugreporter.Editor",
  "rootNamespace": "JoyCastle.BugReporter.Editor",
  "references": ["com.joycastle.bugreporter"],
  "includePlatforms": ["Editor"],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false
}
```

**Step 7: Commit**

```bash
git add -A
git commit -m "chore: init Unity project with Bug Reporter SDK package skeleton"
```

---

### Task 1: 采集器接口 & 数据模型

**Files:**
- Create: `Runtime/Collectors/IInfoCollector.cs`
- Create: `Runtime/Core/CollectResult.cs`
- Create: `Runtime/Core/BugReport.cs`

> 以下所有相对路径基于 `Assets/Packages/com.joycastle.bugreporter/`

**Step 1: 创建 IInfoCollector 接口**

```csharp
namespace JoyCastle.BugReporter {
    public interface IInfoCollector {
        string Key { get; }
        bool IsEnabled { get; }
        CollectResult Collect();
    }
}
```

**Step 2: 创建 CollectResult**

```csharp
using System.Collections.Generic;

namespace JoyCastle.BugReporter {
    public class CollectResult {
        public Dictionary<string, string> Fields { get; set; }
        public Dictionary<string, byte[]> Files { get; set; }

        public CollectResult() {
            Fields = new Dictionary<string, string>();
            Files = new Dictionary<string, byte[]>();
        }
    }
}
```

**Step 3: 创建 BugReport**

```csharp
using System.Collections.Generic;

namespace JoyCastle.BugReporter {
    public class BugReport {
        public string AppId { get; set; }
        public string Description { get; set; }
        public Dictionary<string, string> Fields { get; set; } = new();
        public Dictionary<string, byte[]> Files { get; set; } = new();
    }
}
```

**Step 4: 在 Unity Editor 中确认无编译错误**

打开 Unity，等待编译，确认 Console 无错误。

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: add IInfoCollector interface and data models"
```

---

### Task 2: 内置采集器 — DeviceCollector

**Files:**
- Create: `Runtime/Collectors/DeviceCollector.cs`

**Step 1: 实现 DeviceCollector**

```csharp
using UnityEngine;

namespace JoyCastle.BugReporter {
    public class DeviceCollector : IInfoCollector {
        public string Key => "device";
        public bool IsEnabled => true;

        public CollectResult Collect() {
            return new CollectResult {
                Fields = new() {
                    ["deviceModel"] = SystemInfo.deviceModel,
                    ["osVersion"] = SystemInfo.operatingSystem,
                    ["memorySize"] = SystemInfo.systemMemorySize.ToString(),
                    ["processorType"] = SystemInfo.processorType,
                    ["graphicsDeviceName"] = SystemInfo.graphicsDeviceName,
                }
            };
        }
    }
}
```

**Step 2: 确认无编译错误**

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: add DeviceCollector"
```

---

### Task 3: 内置采集器 — LogCollector

**Files:**
- Create: `Runtime/Collectors/LogCollector.cs`

**Step 1: 实现 LogCollector**

两种日志来源：
1. **运行时日志**：`Application.logMessageReceived` 环形缓冲区（SDK 内置，通用）
2. **日志文件**：项目方通过 `AddLogFilePath()` 指定磁盘上的日志文件路径（可选）

采集时两者合并上报。

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace JoyCastle.BugReporter {
    public class LogCollector : IInfoCollector {
        public string Key => "log";
        public bool IsEnabled { get; set; } = true;

        private readonly int _maxLines;
        private readonly Queue<string> _logBuffer;
        private readonly List<string> _logFilePaths = new();
        private readonly int _maxFileReadBytes;
        private readonly bool _enableRuntimeLog;

        /// <param name="maxLines">运行时日志缓存行数</param>
        /// <param name="enableRuntimeLog">是否监听 Application.logMessageReceived</param>
        /// <param name="maxFileReadBytes">每个日志文件最大读取字节数（默认 64KB）</param>
        public LogCollector(int maxLines = 100, bool enableRuntimeLog = true,
            int maxFileReadBytes = 65536) {
            _maxLines = maxLines;
            _maxFileReadBytes = maxFileReadBytes;
            _enableRuntimeLog = enableRuntimeLog;
            _logBuffer = new Queue<string>(_maxLines);
            if (_enableRuntimeLog) {
                Application.logMessageReceived += OnLogReceived;
            }
        }

        /// <summary>
        /// 项目方调用：添加自定义日志文件路径。
        /// 上报时 SDK 会读取该文件最后 N 字节内容一并上报。
        /// </summary>
        public void AddLogFilePath(string path) {
            if (!string.IsNullOrEmpty(path) && !_logFilePaths.Contains(path)) {
                _logFilePaths.Add(path);
            }
        }

        private void OnLogReceived(string message, string stackTrace, LogType type) {
            var line = $"[{DateTime.Now:HH:mm:ss}][{type}] {message}";
            if (type == LogType.Exception || type == LogType.Error) {
                line += $"\n{stackTrace}";
            }
            if (_logBuffer.Count >= _maxLines) {
                _logBuffer.Dequeue();
            }
            _logBuffer.Enqueue(line);
        }

        public CollectResult Collect() {
            var result = new CollectResult();

            // 1. 运行时日志（如果启用）
            if (_enableRuntimeLog) {
                var sb = new StringBuilder();
                foreach (var line in _logBuffer) {
                    sb.AppendLine(line);
                }
                result.Fields["runtimeLog"] = sb.ToString();
            }

            // 2. 日志文件（逐个读取尾部内容）
            for (var i = 0; i < _logFilePaths.Count; i++) {
                var path = _logFilePaths[i];
                var content = ReadLogFileTail(path);
                if (content != null) {
                    var fieldKey = _logFilePaths.Count == 1
                        ? "logFile"
                        : $"logFile_{i}";
                    result.Fields[fieldKey] = content;
                }
            }

            return result;
        }

        private string ReadLogFileTail(string path) {
            try {
                if (!File.Exists(path)) return null;
                var fileInfo = new FileInfo(path);
                using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var readBytes = Math.Min(_maxFileReadBytes, fileInfo.Length);
                if (readBytes <= 0) return null;
                stream.Seek(-readBytes, SeekOrigin.End);
                var buffer = new byte[readBytes];
                stream.Read(buffer, 0, buffer.Length);
                return Encoding.UTF8.GetString(buffer);
            } catch (Exception e) {
                Debug.LogWarning($"[BugReporter] Failed to read log file '{path}': {e.Message}");
                return null;
            }
        }

        public void Dispose() {
            if (_enableRuntimeLog) {
                Application.logMessageReceived -= OnLogReceived;
            }
        }
    }
}
```

**Step 2: 确认无编译错误**

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: add LogCollector with runtime buffer and custom log file paths"
```

---

### Task 4: 内置采集器 — FpsCollector

**Files:**
- Create: `Runtime/Collectors/FpsCollector.cs`

**Step 1: 实现 FpsCollector**

```csharp
using UnityEngine;

namespace JoyCastle.BugReporter {
    public class FpsCollector : IInfoCollector {
        public string Key => "fps";
        public bool IsEnabled { get; set; } = true;

        private float _deltaTime;
        private float _fps;

        public void Update() {
            _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;
            _fps = 1.0f / _deltaTime;
        }

        public CollectResult Collect() {
            return new CollectResult {
                Fields = new() {
                    ["fps"] = Mathf.RoundToInt(_fps).ToString()
                }
            };
        }
    }
}
```

**Step 2: 确认无编译错误**

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: add FpsCollector"
```

---

### Task 5: 内置采集器 — ScreenshotCollector

**Files:**
- Create: `Runtime/Collectors/ScreenshotCollector.cs`

**Step 1: 实现 ScreenshotCollector**

```csharp
using System.Collections;
using UnityEngine;

namespace JoyCastle.BugReporter {
    public class ScreenshotCollector : IInfoCollector {
        public string Key => "screenshot";
        public bool IsEnabled { get; set; } = true;

        private byte[] _lastScreenshot;

        public IEnumerator CaptureScreenshot() {
            yield return new WaitForEndOfFrame();
            var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            tex.Apply();
            _lastScreenshot = tex.EncodeToPNG();
            Object.Destroy(tex);
        }

        public CollectResult Collect() {
            var result = new CollectResult();
            if (_lastScreenshot != null) {
                result.Files["screenshot"] = _lastScreenshot;
            }
            return result;
        }
    }
}
```

**Step 2: 确认无编译错误**

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: add ScreenshotCollector"
```

---

### Task 6: 内置采集器 — BuildInfoCollector + Editor 注入脚本

**Files:**
- Create: `Runtime/Collectors/BuildInfoCollector.cs`
- Create: `Editor/BuildInfoInjector.cs`

**Step 1: 实现 BuildInfoCollector（Runtime 读取）**

```csharp
using UnityEngine;

namespace JoyCastle.BugReporter {
    public class BuildInfoCollector : IInfoCollector {
        public string Key => "buildinfo";
        public bool IsEnabled => true;

        public CollectResult Collect() {
            var fields = new System.Collections.Generic.Dictionary<string, string> {
                ["versionName"] = Application.version,
                ["platform"] = Application.platform.ToString(),
            };

            // 读取打包时注入的 BuildInfo.json
            var buildInfoAsset = Resources.Load<TextAsset>("BugReporter/BuildInfo");
            if (buildInfoAsset != null) {
                var info = JsonUtility.FromJson<BuildInfoData>(buildInfoAsset.text);
                fields["gitBranch"] = info.gitBranch ?? "";
                fields["gitCommit"] = info.gitCommit ?? "";
                fields["buildNumber"] = info.buildNumber ?? "";
                fields["buildTime"] = info.buildTime ?? "";
            }

            return new CollectResult { Fields = fields };
        }

        [System.Serializable]
        private class BuildInfoData {
            public string gitBranch;
            public string gitCommit;
            public string buildNumber;
            public string buildTime;
        }
    }
}
```

**Step 2: 实现 BuildInfoInjector（Editor 打包时注入）**

```csharp
using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace JoyCastle.BugReporter.Editor {
    public class BuildInfoInjector : IPreprocessBuildWithReport {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) {
            var info = new BuildInfoData {
                gitBranch = GetGitBranch(),
                gitCommit = GetGitCommit(),
                buildNumber = GetBuildNumber(),
                buildTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            };

            var dir = Path.Combine(Application.dataPath,
                "Packages/com.joycastle.bugreporter/Resources/BugReporter");
            Directory.CreateDirectory(dir);

            var json = JsonUtility.ToJson(info, true);
            File.WriteAllText(Path.Combine(dir, "BuildInfo.json"), json);
            AssetDatabase.Refresh();

            Debug.Log($"[BugReporter] BuildInfo injected: branch={info.gitBranch}, commit={info.gitCommit}");
        }

        private static string GetGitBranch() {
            // 优先读 Jenkins 环境变量
            var env = Environment.GetEnvironmentVariable("GIT_BRANCH");
            if (!string.IsNullOrEmpty(env)) return env;
            // fallback: git 命令
            return RunGit("rev-parse --abbrev-ref HEAD");
        }

        private static string GetGitCommit() {
            var env = Environment.GetEnvironmentVariable("GIT_COMMIT");
            if (!string.IsNullOrEmpty(env)) return env;
            return RunGit("rev-parse --short HEAD");
        }

        private static string GetBuildNumber() {
            return Environment.GetEnvironmentVariable("BUILD_NUMBER") ?? "0";
        }

        private static string RunGit(string args) {
            try {
                var psi = new ProcessStartInfo("git", args) {
                    WorkingDirectory = Application.dataPath,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var process = Process.Start(psi);
                var output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                return process.ExitCode == 0 ? output : "unknown";
            } catch {
                return "unknown";
            }
        }

        [Serializable]
        private class BuildInfoData {
            public string gitBranch;
            public string gitCommit;
            public string buildNumber;
            public string buildTime;
        }
    }
}
```

**Step 3: 确认无编译错误**

**Step 4: Commit**

```bash
git add -A
git commit -m "feat: add BuildInfoCollector and BuildInfoInjector"
```

---

### Task 7: BugReporterConfig (ScriptableObject)

**Files:**
- Create: `Runtime/Config/BugReporterConfig.cs`

**Step 1: 实现配置**

```csharp
using UnityEngine;

namespace JoyCastle.BugReporter {
    [CreateAssetMenu(
        fileName = "BugReporterConfig",
        menuName = "BugReporter/Config")]
    public class BugReporterConfig : ScriptableObject {
        [Header("服务端")]
        [Tooltip("Bug 上报服务器地址")]
        public string serverUrl = "";

        [Tooltip("项目标识符")]
        public string appId = "";

        [Header("触发方式")]
        public bool enableShake = true;
        public float shakeThreshold = 2.5f;

        [Header("采集器配置")]
        public bool enableLogCollector = true;
        public bool enableRuntimeLog = true;  // 是否采集 Application.logMessageReceived
        public int maxLogLines = 100;
        public bool enableScreenshot = true;
        public bool enableFpsCollector = true;

        [Header("日志文件（可选）")]
        [Tooltip("项目方指定的日志文件路径，留空则只采集运行时日志")]
        public string[] logFilePaths = new string[0];

        [Header("上报配置")]
        [Tooltip("HTTP 请求超时时间(秒)")]
        public int uploadTimeout = 30;
    }
}
```

**Step 2: 确认无编译错误**

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: add BugReporterConfig ScriptableObject"
```

---

### Task 8: ReportUploader (HTTP 上报)

**Files:**
- Create: `Runtime/Core/ReportUploader.cs`

**Step 1: 实现 ReportUploader**

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace JoyCastle.BugReporter {
    public class ReportUploader {
        private readonly string _serverUrl;
        private readonly int _timeout;

        public ReportUploader(string serverUrl, int timeout = 30) {
            _serverUrl = serverUrl;
            _timeout = timeout;
        }

        public IEnumerator Upload(BugReport report, Action<bool, string> onComplete = null) {
            var form = new List<IMultipartFormSection> {
                new MultipartFormDataSection("appId", report.AppId ?? ""),
                new MultipartFormDataSection("description", report.Description ?? ""),
            };

            // 所有采集器的文本字段
            if (report.Fields != null) {
                foreach (var kv in report.Fields) {
                    form.Add(new MultipartFormDataSection(kv.Key, kv.Value ?? ""));
                }
            }

            // 所有采集器的文件字段
            if (report.Files != null) {
                foreach (var kv in report.Files) {
                    var ext = "bin";
                    var mime = "application/octet-stream";
                    // 简单判断 PNG
                    if (kv.Value.Length > 4 && kv.Value[0] == 0x89
                        && kv.Value[1] == 0x50) {
                        ext = "png";
                        mime = "image/png";
                    }
                    form.Add(new MultipartFormFileSection(
                        kv.Key, kv.Value, $"{kv.Key}.{ext}", mime));
                }
            }

            using var request = UnityWebRequest.Post(_serverUrl, form);
            request.timeout = _timeout;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success) {
                Debug.Log($"[BugReporter] Upload success: {request.downloadHandler.text}");
                onComplete?.Invoke(true, request.downloadHandler.text);
            } else {
                Debug.LogWarning($"[BugReporter] Upload failed: {request.error}");
                onComplete?.Invoke(false, request.error);
            }
        }
    }
}
```

**Step 2: 确认无编译错误**

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: add ReportUploader with UnityWebRequest"
```

---

### Task 9: BugReporterSDK 入口类

**Files:**
- Create: `Runtime/BugReporterSDK.cs`

**Step 1: 实现 SDK 入口**

这是核心类，管理采集器注册、生命周期和上报流程。

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace JoyCastle.BugReporter {
    public class BugReporterSDK : MonoBehaviour {
        private static BugReporterSDK _instance;
        private static BugReporterConfig _config;
        private static readonly List<IInfoCollector> s_collectors = new();
        private static bool s_initialized;

        private ReportUploader _uploader;
        private FpsCollector _fpsCollector;
        private ScreenshotCollector _screenshotCollector;
        private LogCollector _logCollector;

        // ── 公开 API ──

        public static void Init(BugReporterConfig config) {
            if (s_initialized) {
                Debug.LogWarning("[BugReporter] Already initialized.");
                return;
            }

            _config = config;
            s_collectors.Clear();

            // 创建持久化 GameObject
            var go = new GameObject("[BugReporterSDK]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<BugReporterSDK>();
            _instance.Setup();

            s_initialized = true;
            Debug.Log("[BugReporter] Initialized.");
        }

        public static void RegisterCollector(IInfoCollector collector) {
            if (collector == null) return;
            s_collectors.Add(collector);
        }

        public static void ShowReportUI() {
            EnsureInitialized();
            // TODO: Task 10 实现 UI
        }

        public static void SubmitSilently(string description = "") {
            EnsureInitialized();
            _instance.StartCoroutine(_instance.DoSubmit(description));
        }

        // ── 内部逻辑 ──

        private void Setup() {
            _uploader = new ReportUploader(_config.serverUrl, _config.uploadTimeout);

            // 注册内置采集器
            s_collectors.Add(new DeviceCollector());

            if (_config.enableLogCollector) {
                _logCollector = new LogCollector(
                    _config.maxLogLines,
                    _config.enableRuntimeLog);
                // 注册项目方配置的日志文件路径
                if (_config.logFilePaths != null) {
                    foreach (var path in _config.logFilePaths) {
                        _logCollector.AddLogFilePath(path);
                    }
                }
                s_collectors.Add(_logCollector);
            }

            if (_config.enableFpsCollector) {
                _fpsCollector = new FpsCollector();
                s_collectors.Add(_fpsCollector);
            }

            if (_config.enableScreenshot) {
                _screenshotCollector = new ScreenshotCollector();
                s_collectors.Add(_screenshotCollector);
            }

            s_collectors.Add(new BuildInfoCollector());
        }

        private void Update() {
            _fpsCollector?.Update();

            // 摇一摇检测
            if (_config.enableShake
                && Input.acceleration.sqrMagnitude > _config.shakeThreshold * _config.shakeThreshold) {
                ShowReportUI();
            }
        }

        private IEnumerator DoSubmit(string description) {
            // 先截图（需要等到帧末）
            if (_screenshotCollector is { IsEnabled: true }) {
                yield return _screenshotCollector.CaptureScreenshot();
            }

            // 汇总所有采集器数据
            var report = new BugReport {
                AppId = _config.appId,
                Description = description,
            };

            foreach (var collector in s_collectors) {
                if (!collector.IsEnabled) continue;
                try {
                    var result = collector.Collect();
                    if (result.Fields != null) {
                        foreach (var kv in result.Fields) {
                            report.Fields[kv.Key] = kv.Value;
                        }
                    }
                    if (result.Files != null) {
                        foreach (var kv in result.Files) {
                            report.Files[kv.Key] = kv.Value;
                        }
                    }
                } catch (Exception e) {
                    Debug.LogWarning($"[BugReporter] Collector '{collector.Key}' failed: {e.Message}");
                }
            }

            // 上报
            yield return _uploader.Upload(report, (success, msg) => {
                Debug.Log(success
                    ? "[BugReporter] Report submitted."
                    : $"[BugReporter] Report failed: {msg}");
            });
        }

        private static void EnsureInitialized() {
            if (!s_initialized) {
                throw new InvalidOperationException(
                    "[BugReporter] SDK not initialized. Call BugReporterSDK.Init() first.");
            }
        }

        private void OnDestroy() {
            _logCollector?.Dispose();
            s_collectors.Clear();
            s_initialized = false;
        }
    }
}
```

**Step 2: 确认无编译错误**

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: add BugReporterSDK entry class with collector management"
```

---

### Task 10: 反馈 UI 面板

**Files:**
- Create: `Runtime/UI/BugReportPanel.cs`

**Step 1: 实现 BugReportPanel**

使用 UGUI 代码动态创建 UI（不依赖 prefab，避免序列化兼容问题），包含：描述输入框 + 提交按钮 + 取消按钮。

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace JoyCastle.BugReporter {
    public class BugReportPanel : MonoBehaviour {
        private InputField _inputField;
        private GameObject _panelRoot;

        public void Show() {
            if (_panelRoot != null) return;
            BuildUI();
            _panelRoot.SetActive(true);
        }

        public void Hide() {
            if (_panelRoot != null) {
                Destroy(_panelRoot);
                _panelRoot = null;
            }
        }

        private void BuildUI() {
            // Canvas
            var canvasGo = new GameObject("BugReportCanvas");
            canvasGo.transform.SetParent(transform);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();
            _panelRoot = canvasGo;

            // 半透明背景
            var bg = CreateImage(canvasGo.transform, "Background",
                new Color(0, 0, 0, 0.6f));
            bg.rectTransform.anchorMin = Vector2.zero;
            bg.rectTransform.anchorMax = Vector2.one;
            bg.rectTransform.sizeDelta = Vector2.zero;

            // 内容面板
            var panel = CreateImage(canvasGo.transform, "Panel",
                new Color(1, 1, 1, 0.95f));
            panel.rectTransform.anchorMin = new Vector2(0.1f, 0.25f);
            panel.rectTransform.anchorMax = new Vector2(0.9f, 0.75f);
            panel.rectTransform.sizeDelta = Vector2.zero;

            // 标题
            var title = CreateText(panel.transform, "Title", "Bug 反馈", 24,
                TextAnchor.UpperCenter, new Vector2(0, -10), new Vector2(0, 40));
            title.rectTransform.anchorMin = new Vector2(0, 1);
            title.rectTransform.anchorMax = new Vector2(1, 1);

            // 输入框
            var inputGo = new GameObject("InputField");
            inputGo.transform.SetParent(panel.transform, false);
            var inputBg = inputGo.AddComponent<Image>();
            inputBg.color = new Color(0.95f, 0.95f, 0.95f, 1f);
            var inputRt = inputGo.GetComponent<RectTransform>();
            inputRt.anchorMin = new Vector2(0.05f, 0.3f);
            inputRt.anchorMax = new Vector2(0.95f, 0.85f);
            inputRt.sizeDelta = Vector2.zero;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(inputGo.transform, false);
            var inputText = textGo.AddComponent<Text>();
            inputText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            inputText.fontSize = 18;
            inputText.color = Color.black;
            inputText.supportRichText = false;
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(10, 5);
            textRt.offsetMax = new Vector2(-10, -5);

            var placeholderGo = new GameObject("Placeholder");
            placeholderGo.transform.SetParent(inputGo.transform, false);
            var placeholder = placeholderGo.AddComponent<Text>();
            placeholder.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            placeholder.fontSize = 18;
            placeholder.fontStyle = FontStyle.Italic;
            placeholder.color = new Color(0.5f, 0.5f, 0.5f, 0.8f);
            placeholder.text = "请描述你遇到的问题...";
            var phRt = placeholderGo.GetComponent<RectTransform>();
            phRt.anchorMin = Vector2.zero;
            phRt.anchorMax = Vector2.one;
            phRt.offsetMin = new Vector2(10, 5);
            phRt.offsetMax = new Vector2(-10, -5);

            _inputField = inputGo.AddComponent<InputField>();
            _inputField.textComponent = inputText;
            _inputField.placeholder = placeholder;
            _inputField.lineType = InputField.LineType.MultiLineNewline;

            // 提交按钮
            CreateButton(panel.transform, "Submit", "提交",
                new Vector2(0.55f, 0.05f), new Vector2(0.95f, 0.2f),
                new Color(0.2f, 0.6f, 1f, 1f), OnSubmit);

            // 取消按钮
            CreateButton(panel.transform, "Cancel", "取消",
                new Vector2(0.05f, 0.05f), new Vector2(0.45f, 0.2f),
                new Color(0.7f, 0.7f, 0.7f, 1f), OnCancel);
        }

        private void OnSubmit() {
            var desc = _inputField != null ? _inputField.text : "";
            Hide();
            BugReporterSDK.SubmitSilently(desc);
        }

        private void OnCancel() {
            Hide();
        }

        // ── UI 工具方法 ──

        private static Image CreateImage(Transform parent, string name, Color color) {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private static Text CreateText(Transform parent, string name, string text,
            int fontSize, TextAnchor alignment, Vector2 anchoredPos, Vector2 sizeDelta) {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = fontSize;
            t.alignment = alignment;
            t.color = Color.black;
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            return t;
        }

        private static void CreateButton(Transform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax, Color color, UnityEngine.Events.UnityAction onClick) {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.sizeDelta = Vector2.zero;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            var text = CreateText(go.transform, "Label", label, 20,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero);
            text.color = Color.white;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.sizeDelta = Vector2.zero;
        }
    }
}
```

**Step 2: 在 BugReporterSDK.cs 中接入 UI**

将 `ShowReportUI()` 方法的 `// TODO` 替换为：

```csharp
public static void ShowReportUI() {
    EnsureInitialized();
    if (_instance._panel == null) {
        _instance._panel = _instance.gameObject.AddComponent<BugReportPanel>();
    }
    _instance._panel.Show();
}
```

并在 `BugReporterSDK` 类中添加字段：
```csharp
private BugReportPanel _panel;
```

**Step 3: 确认无编译错误**

**Step 4: Commit**

```bash
git add -A
git commit -m "feat: add BugReportPanel UI (code-driven, no prefab)"
```

---

### Task 11: 集成测试场景

**Files:**
- Modify: `Assets/Scenes/SampleScene.unity`（或新建测试场景）
- Create: `Assets/Scripts/BugReporterTestInit.cs`

**Step 1: 创建测试初始化脚本**

```csharp
using JoyCastle.BugReporter;
using UnityEngine;

public class BugReporterTestInit : MonoBehaviour {
    [SerializeField] private BugReporterConfig config;

    private void Awake() {
        if (config == null) {
            // 代码创建默认配置
            config = ScriptableObject.CreateInstance<BugReporterConfig>();
            config.serverUrl = "http://localhost:8000/api/bug-report";
            config.appId = "test-app";
        }
        BugReporterSDK.Init(config);

        // 注册一个测试用的自定义采集器
        BugReporterSDK.RegisterCollector(new TestGameCollector());
    }

    private void OnGUI() {
        if (GUI.Button(new Rect(10, 10, 200, 60), "Show Bug Report UI")) {
            BugReporterSDK.ShowReportUI();
        }
        if (GUI.Button(new Rect(10, 80, 200, 60), "Submit Silently")) {
            BugReporterSDK.SubmitSilently("Silent test report");
        }
    }

    // 演示项目方自定义采集器
    private class TestGameCollector : IInfoCollector {
        public string Key => "game";
        public bool IsEnabled => true;
        public CollectResult Collect() {
            return new CollectResult {
                Fields = new() {
                    ["userId"] = "test_user_123",
                    ["serverId"] = "dev-01",
                }
            };
        }
    }
}
```

**Step 2: 在 Unity Editor 中将 BugReporterTestInit 挂到场景中的空 GameObject 上**

**Step 3: 运行测试**

Play Mode 中：
1. 点击 "Show Bug Report UI" → 应弹出反馈面板
2. 输入描述，点击提交 → Console 应显示 Upload 日志（服务端未启动时会显示连接失败，属正常）
3. 点击 "Submit Silently" → 静默上报

**Step 4: Commit**

```bash
git add -A
git commit -m "feat: add integration test scene with BugReporterTestInit"
```

---

### Task 12: 简易 Mock 服务器（验证上报数据）

**Files:**
- Create: `Tools/mock_server.py`

**Step 1: 创建 Python mock 服务器**

```python
"""
简易 Mock 服务器，用于验证 Bug Reporter SDK 的上报数据。
运行: python Tools/mock_server.py
"""
from http.server import HTTPServer, BaseHTTPRequestHandler
import cgi
import json
import os
from datetime import datetime


class BugReportHandler(BaseHTTPRequestHandler):
    def do_POST(self):
        if self.path != "/api/bug-report":
            self.send_response(404)
            self.end_headers()
            return

        content_type = self.headers.get("Content-Type", "")
        if "multipart/form-data" not in content_type:
            self.send_response(400)
            self.end_headers()
            self.wfile.write(b"Expected multipart/form-data")
            return

        form = cgi.FieldStorage(
            fp=self.rfile,
            headers=self.headers,
            environ={"REQUEST_METHOD": "POST",
                     "CONTENT_TYPE": content_type},
        )

        print(f"\n{'='*60}")
        print(f"[{datetime.now():%H:%M:%S}] Bug Report Received")
        print(f"{'='*60}")

        fields = {}
        files = []
        for key in form.keys():
            item = form[key]
            if item.filename:
                save_dir = "uploads"
                os.makedirs(save_dir, exist_ok=True)
                path = os.path.join(save_dir, item.filename)
                with open(path, "wb") as f:
                    f.write(item.file.read())
                files.append(f"{key}: {item.filename} (saved to {path})")
            else:
                fields[key] = item.value

        for k, v in sorted(fields.items()):
            display = v[:200] + "..." if len(v) > 200 else v
            print(f"  {k}: {display}")
        for f in files:
            print(f"  [FILE] {f}")
        print(f"{'='*60}\n")

        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.end_headers()
        self.wfile.write(json.dumps({"status": "ok"}).encode())


if __name__ == "__main__":
    port = 8000
    server = HTTPServer(("0.0.0.0", port), BugReportHandler)
    print(f"Mock server running on http://localhost:{port}/api/bug-report")
    print("Waiting for bug reports...\n")
    server.serve_forever()
```

**Step 2: 运行 mock 服务器**

```bash
cd /Users/xiaowangzi/AutoFixBugFrameWork/AutoFixBugFrameWork
python Tools/mock_server.py
```

**Step 3: 在 Unity Editor Play Mode 中点击提交，确认 mock 服务器打印出完整的采集数据**

预期输出应包含：appId, description, deviceModel, osVersion, memorySize, fps, gameLog, versionName, platform, userId 等字段，以及 screenshot 文件。

**Step 4: Commit**

```bash
git add -A
git commit -m "feat: add mock server for testing bug report upload"
```

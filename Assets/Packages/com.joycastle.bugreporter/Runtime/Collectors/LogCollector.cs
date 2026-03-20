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
        private readonly bool _enableRuntimeLog;

        /// <param name="maxLines">运行时日志缓存行数</param>
        /// <param name="enableRuntimeLog">是否监听 Application.logMessageReceived</param>
        public LogCollector(int maxLines = 100, bool enableRuntimeLog = true) {
            _maxLines = maxLines;
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

            // 2. 日志文件（逐个读取，作为文件附件上传）
            for (var i = 0; i < _logFilePaths.Count; i++) {
                var path = _logFilePaths[i];
                var bytes = ReadLogFileBytes(path);
                if (bytes != null) {
                    var fileName = _logFilePaths.Count == 1
                        ? "logFile.log"
                        : $"logFile_{i}.log";
                    result.Files[fileName] = bytes;
                }
            }

            return result;
        }

        private byte[] ReadLogFileBytes(string path) {
            try {
                if (!File.Exists(path)) return null;
                return File.ReadAllBytes(path);
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

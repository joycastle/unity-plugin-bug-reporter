using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace JoyCastle.BugReporter {
    public class ReportUploader {
        private readonly string _serverUrl;
        private readonly string _webhookToken;
        private readonly int _timeout;

        public ReportUploader(string serverUrl, string webhookToken = "", int timeout = 30) {
            _serverUrl = serverUrl;
            _webhookToken = webhookToken;
            _timeout = timeout;
        }

        public IEnumerator Upload(BugReport report, Action<bool, string> onComplete = null) {
            var form = new List<IMultipartFormSection>();

            // ── 构建 data JSON ──
            var fields = report.Fields ?? new Dictionary<string, string>();
            var dataMap = new Dictionary<string, string>();

            // 映射字段
            dataMap["title"] = GetField(fields, "issueTitle", report.Description ?? "");
            dataMap["userId"] = GetField(fields, "userId", "");

            var deviceModel = GetField(fields, "deviceModel", "");
            var osVersion = GetField(fields, "osVersion", "");
            dataMap["device"] = !string.IsNullOrEmpty(deviceModel) && !string.IsNullOrEmpty(osVersion)
                ? $"{deviceModel} / {osVersion}"
                : deviceModel + osVersion;

            dataMap["version"] = GetField(fields, "versionName", "");
            dataMap["branch"] = GetField(fields, "gitBranch", "");
            dataMap["commit"] = GetField(fields, "gitCommit", "");
            dataMap["appId"] = report.AppId ?? "";

            // 其他未映射的字段也放进 data
            var mappedKeys = new HashSet<string> {
                "issueTitle", "userId", "deviceModel", "osVersion",
                "versionName", "gitBranch", "gitCommit"
            };
            foreach (var kv in fields) {
                if (!mappedKeys.Contains(kv.Key)) {
                    // 过滤一些不需要的log
                    if (kv.Key.Equals("runtimeLog")) {
                        continue;
                    }
                    dataMap[kv.Key] = kv.Value ?? "";
                }
            }

            // 序列化为 JSON
            var dataJson = DictToJson(dataMap);
            form.Add(new MultipartFormDataSection("data", dataJson, Encoding.UTF8, "application/json"));

            // ── 所有文件统一用 "files" 作为字段名 ──
            if (report.Files != null) {
                foreach (var kv in report.Files) {
                    var fileName = kv.Key;
                    var mime = DetectMime(kv.Value, fileName);

                    if (!fileName.Contains(".")) {
                        fileName += "." + DetectExt(kv.Value);
                    }

                    form.Add(new MultipartFormFileSection("files", kv.Value, fileName, mime));
                }
            }

            // ── 打印上报详情 ──
            var sb = new StringBuilder();
            sb.AppendLine("[BugReporter] ===== Upload Details =====");
            sb.AppendLine($"  URL: {_serverUrl}");
            sb.AppendLine($"  Token: {(string.IsNullOrEmpty(_webhookToken) ? "(none)" : _webhookToken.Substring(0, 8) + "...")}");
            sb.AppendLine($"  [data] {dataJson}");
            foreach (var section in form) {
                if (!string.IsNullOrEmpty(section.fileName)) {
                    var sizeMB = section.sectionData != null ? section.sectionData.Length / (1024f * 1024f) : 0;
                    sb.AppendLine($"  [File] {section.fileName} ({sizeMB:F2}MB)");
                }
            }
            sb.AppendLine("[BugReporter] ========================");
            Debug.Log(sb.ToString());

            // ── 发送请求 ──
            using var request = UnityWebRequest.Post(_serverUrl, form);
            request.timeout = _timeout;

            if (!string.IsNullOrEmpty(_webhookToken)) {
                request.SetRequestHeader("X-Webhook-Token", _webhookToken);
            }

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success) {
                Debug.Log($"[BugReporter] Upload success: {request.downloadHandler.text}");
                onComplete?.Invoke(true, request.downloadHandler.text);
            } else {
                Debug.LogWarning($"[BugReporter] Upload failed: {request.error}");
                onComplete?.Invoke(false, request.error);
            }
        }

        private static string GetField(Dictionary<string, string> fields, string key,
            string fallback = "") {
            return fields.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value)
                ? value
                : fallback;
        }

        /// <summary>
        /// 简单的 Dictionary → JSON 序列化，避免引入额外依赖。
        /// </summary>
        private static string DictToJson(Dictionary<string, string> dict) {
            var sb = new StringBuilder();
            sb.Append("{");
            var first = true;
            foreach (var kv in dict) {
                if (!first) sb.Append(",");
                sb.Append("\"");
                sb.Append(EscapeJson(kv.Key));
                sb.Append("\":\"");
                sb.Append(EscapeJson(kv.Value));
                sb.Append("\"");
                first = false;
            }
            sb.Append("}");
            return sb.ToString();
        }

        private static string EscapeJson(string s) {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r")
                    .Replace("\t", "\\t");
        }

        private static string DetectMime(byte[] data, string fileName) {
            if (data.Length > 4 && data[0] == 0x89 && data[1] == 0x50)
                return "image/png";
            if (data.Length > 2 && data[0] == 0xFF && data[1] == 0xD8)
                return "image/jpeg";
            if (data.Length > 8 && data[4] == 0x66 && data[5] == 0x74
                && data[6] == 0x79 && data[7] == 0x70)
                return "video/mp4";
            if (fileName.EndsWith(".log") || fileName.EndsWith(".txt"))
                return "text/plain";
            return "application/octet-stream";
        }

        private static string DetectExt(byte[] data) {
            if (data.Length > 4 && data[0] == 0x89 && data[1] == 0x50) return "png";
            if (data.Length > 2 && data[0] == 0xFF && data[1] == 0xD8) return "jpg";
            if (data.Length > 8 && data[4] == 0x66 && data[5] == 0x74
                && data[6] == 0x79 && data[7] == 0x70) return "mp4";
            return "bin";
        }
    }
}

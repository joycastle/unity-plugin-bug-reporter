using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace JoyCastle.BugReporter {
    /// <summary>
    /// Bug 反馈面板。
    /// 从 Resources/BugReporter/BugReportPanel prefab 加载 UI。
    /// 点击 CollectBtn 后执行采集，将采集到的 Fields 展示在滚动列表中，
    /// 截图展示在 _ScreenShotRawImage 上，然后上报。
    ///
    /// Prefab 节点约定：
    ///   - CollectBtn : Button，点击触发采集 + 上报
    ///   - CollectInfoPanel/Scroll View/Viewport/Content : 信息列表容器
    ///   - CollectInfoPanel/Scroll View/Viewport/Content/InfoItem : 模板（key + value）
    ///   - ScreenshotPanel/_ScreenShotRawImage : RawImage，展示截图
    /// </summary>
    public class BugReportPanel : MonoBehaviour {
        private const string DefaultPrefabPath = "BugReporter/BugReportPanel";

        private GameObject _panelInstance;
        private GameObject _customPrefab;

        // UI 引用
        private Button _collectBtn;
        private Transform _contentParent;
        private GameObject _infoItemTemplate;
        private RawImage _screenshotRawImage;

        /// <summary>
        /// 项目方可调用：指定自定义 prefab，不走 Resources 加载。
        /// </summary>
        public void SetPrefab(GameObject prefab) {
            _customPrefab = prefab;
        }

        public void Show() {
            if (_panelInstance != null) return;

            var prefab = _customPrefab != null
                ? _customPrefab
                : Resources.Load<GameObject>(DefaultPrefabPath);

            if (prefab == null) {
                Debug.LogError(
                    $"[BugReporter] Panel prefab not found at Resources/{DefaultPrefabPath}. " +
                    "Please create a prefab or call SetPrefab() to provide one.");
                return;
            }

            _panelInstance = Instantiate(prefab, transform);
            BindUI();
            _panelInstance.SetActive(true);
        }

        public void Hide() {
            if (_panelInstance != null) {
                Destroy(_panelInstance);
                _panelInstance = null;
                _collectBtn = null;
                _contentParent = null;
                _infoItemTemplate = null;
                _screenshotRawImage = null;
            }
        }

        private void BindUI() {
            var root = _panelInstance.transform;

            // CollectBtn
            var collectBtnTr = root.Find("CollectBtn");
            if (collectBtnTr != null) {
                _collectBtn = collectBtnTr.GetComponent<Button>();
                _collectBtn?.onClick.AddListener(OnCollectClicked);
            }

            // InfoItem 模板（在 Content 下）
            var contentTr = root.Find("CollectInfoPanel/Scroll View/Viewport/Content");
            if (contentTr != null) {
                _contentParent = contentTr;
                var itemTr = contentTr.Find("InfoItem");
                if (itemTr != null) {
                    _infoItemTemplate = itemTr.gameObject;
                    _infoItemTemplate.SetActive(false); // 隐藏模板
                }
            }

            // 截图 RawImage
            var rawImgTr = root.Find("ScreenshotPanel/_ScreenShotRawImage");
            if (rawImgTr != null) {
                _screenshotRawImage = rawImgTr.GetComponent<RawImage>();
            }
        }

        private void OnCollectClicked() {
            _collectBtn.interactable = false;
            StartCoroutine(DoCollectAndSubmit());
        }

        private IEnumerator DoCollectAndSubmit() {
            var collectors = BugReporterSDK.GetCollectors();
            var screenshotCollector = BugReporterSDK.GetScreenshotCollector();

            // 1. 先截图（需要等到帧末）
            if (screenshotCollector is { IsEnabled: true }) {
                yield return screenshotCollector.CaptureScreenshot();
            }

            // 2. 采集所有数据
            var allFields = new Dictionary<string, string>();
            var allFiles = new Dictionary<string, byte[]>();

            foreach (var collector in collectors) {
                if (!collector.IsEnabled) continue;
                try {
                    var result = collector.Collect();
                    if (result.Fields != null) {
                        foreach (var kv in result.Fields)
                            allFields[kv.Key] = kv.Value;
                    }
                    if (result.Files != null) {
                        foreach (var kv in result.Files)
                            allFiles[kv.Key] = kv.Value;
                    }
                } catch (Exception e) {
                    Debug.LogWarning($"[BugReporter] Collector '{collector.Key}' failed: {e.Message}");
                }
            }

            // 3. 展示采集到的 Fields 到滚动列表
            PopulateInfoList(allFields);

            // 4. 展示截图
            if (allFiles.TryGetValue("screenshot", out var pngBytes) && pngBytes != null) {
                ShowScreenshot(pngBytes);
            }

            // 5. 上报
            var report = new BugReport {
                AppId = BugReporterSDK.GetConfig().appId,
                Description = "",
                Fields = allFields,
                Files = allFiles,
            };

            yield return BugReporterSDK.GetUploader().Upload(report, (success, msg) => {
                Debug.Log(success
                    ? "[BugReporter] Report submitted."
                    : $"[BugReporter] Report failed: {msg}");
            });

            if (_collectBtn != null) {
                _collectBtn.interactable = true;
            }
        }

        private void PopulateInfoList(Dictionary<string, string> fields) {
            if (_contentParent == null || _infoItemTemplate == null) return;

            // 清除之前生成的 item（保留模板）
            for (var i = _contentParent.childCount - 1; i >= 0; i--) {
                var child = _contentParent.GetChild(i).gameObject;
                if (child != _infoItemTemplate) {
                    Destroy(child);
                }
            }

            // 为每个字段创建一行
            foreach (var kv in fields) {
                var item = Instantiate(_infoItemTemplate, _contentParent);
                item.SetActive(true);

                var keyText = item.transform.Find("key")?.GetComponent<Text>();
                var valueText = item.transform.Find("value")?.GetComponent<Text>();

                if (keyText != null) keyText.text = kv.Key;
                if (valueText != null) {
                    // 长文本截断显示
                    valueText.text = kv.Value != null && kv.Value.Length > 200
                        ? kv.Value.Substring(0, 200) + "..."
                        : kv.Value ?? "";
                }
            }
        }

        private void ShowScreenshot(byte[] pngBytes) {
            if (_screenshotRawImage == null) return;

            var tex = new Texture2D(2, 2);
            if (tex.LoadImage(pngBytes)) {
                _screenshotRawImage.texture = tex;
                _screenshotRawImage.SetNativeSize();
                // 保持宽高比适配
                FitRawImageToParent(_screenshotRawImage);
            }
        }

        private static void FitRawImageToParent(RawImage rawImage) {
            var rt = rawImage.GetComponent<RectTransform>();
            var parentRt = rt.parent as RectTransform;
            if (parentRt == null || rawImage.texture == null) return;

            var parentSize = parentRt.rect.size;
            var texW = (float)rawImage.texture.width;
            var texH = (float)rawImage.texture.height;
            var scale = Mathf.Min(parentSize.x / texW, parentSize.y / texH);

            rt.sizeDelta = new Vector2(texW * scale, texH * scale);
        }
    }
}

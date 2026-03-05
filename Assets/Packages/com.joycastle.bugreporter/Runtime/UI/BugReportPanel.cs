using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace JoyCastle.BugReporter {
    /// <summary>
    /// Bug 反馈面板。
    /// 从 Resources/BugReporter/BugReportPanel prefab 加载 UI。
    /// 打开时立即采集并展示所有信息；点击 GetBtn 截图并刷新预览；点击 CollectBtn 上报。
    ///
    /// Prefab 节点约定：
    ///   - CollectBtn : Button，点击上报
    ///   - CollectInfoPanel/Scroll View/Viewport/Content : 信息列表容器
    ///   - CollectInfoPanel/Scroll View/Viewport/Content/InfoItem : 模板（key + value）
    ///   - ScreenshotPanel/_ScreenShotRawImage : RawImage，展示截图
    ///   - ScreenshotPanel/_ScreenShotRawImage/GetBtn : Button，点击截图
    /// </summary>
    public class BugReportPanel : MonoBehaviour {
        private const string DefaultPrefabPath = "BugReporter/BugReportPanel";

        private GameObject _panelInstance;
        private GameObject _customPrefab;

        // UI 引用
        private Button _collectBtn;
        private Button _getBtn;
        private Button _selectVideoBtn;
        private Button _foldBtn;
        private Text _foldBtnText;
        private Text _videoKeyText;
        private Transform _contentParent;
        private GameObject _infoItemTemplate;
        private GameObject _issueTitleItem;
        private InputField _issueTitleInput;
        private GameObject _videoItem;
        private GameObject _foldItem;
        private RawImage _screenshotRawImage;

        // 采集信息展开/收起状态
        private bool _infoExpanded;
        private readonly List<GameObject> _infoItems = new();

        // 采集缓存（打开时采集一次，上报时直接用）
        private Dictionary<string, string> _cachedFields;
        private Dictionary<string, byte[]> _cachedFiles;

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

            // 打开时立即采集并展示信息
            CollectAndDisplay();
        }

        public void Hide() {
            if (_panelInstance != null) {
                Destroy(_panelInstance);
                _panelInstance = null;
                _collectBtn = null;
                _getBtn = null;
                _selectVideoBtn = null;
                _foldBtn = null;
                _foldBtnText = null;
                _videoKeyText = null;
                _contentParent = null;
                _infoItemTemplate = null;
                _issueTitleItem = null;
                _issueTitleInput = null;
                _videoItem = null;
                _foldItem = null;
                _screenshotRawImage = null;
                _infoExpanded = false;
                _infoItems.Clear();
                _cachedFields = null;
                _cachedFiles = null;
            }
        }

        private void BindUI() {
            var root = _panelInstance.transform;

            // CollectBtn — 上报按钮
            var collectBtnTr = root.Find("Panel/CollectBtn");
            if (collectBtnTr != null) {
                _collectBtn = collectBtnTr.GetComponent<Button>();
                _collectBtn?.onClick.AddListener(OnSubmitClicked);
            }

            // InfoItem 模板 和 InputItem_IssueTitle（在 Content 下）
            var contentTr = root.Find("Panel/CollectInfoPanel/Scroll View/Viewport/Content");
            if (contentTr != null) {
                _contentParent = contentTr;

                // IssueTitle 输入项
                var issueTitleTr = contentTr.Find("InputItem_IssueTitle");
                if (issueTitleTr != null) {
                    _issueTitleItem = issueTitleTr.gameObject;
                    _issueTitleInput = issueTitleTr.Find("InputField")?.GetComponent<InputField>();
                }

                // 视频选择项
                var videoTr = contentTr.Find("InputVideo_BugVideo");
                if (videoTr != null) {
                    _videoItem = videoTr.gameObject;
                    _videoKeyText = videoTr.Find("key")?.GetComponent<Text>();
                    var selectBtnTr = videoTr.Find("SelectVideoBtn");
                    if (selectBtnTr != null) {
                        _selectVideoBtn = selectBtnTr.GetComponent<Button>();
                        _selectVideoBtn?.onClick.AddListener(OnSelectVideoClicked);
                    }
                }

                // Fold 展开/收起按钮
                var foldTr = contentTr.Find("Fold");
                if (foldTr != null) {
                    _foldItem = foldTr.gameObject;
                    var foldBtnTr = foldTr.Find("FoldBtn");
                    if (foldBtnTr != null) {
                        _foldBtn = foldBtnTr.GetComponent<Button>();
                        _foldBtnText = foldBtnTr.Find("Text")?.GetComponent<Text>();
                        _foldBtn?.onClick.AddListener(OnFoldClicked);
                    }
                }

                // InfoItem 模板
                var itemTr = contentTr.Find("InfoItem");
                if (itemTr != null) {
                    _infoItemTemplate = itemTr.gameObject;
                    _infoItemTemplate.SetActive(false);
                }
            }

            // 截图 RawImage
            var rawImgTr = root.Find("Panel/ScreenshotPanel/_ScreenShotRawImage");
            if (rawImgTr != null) {
                _screenshotRawImage = rawImgTr.GetComponent<RawImage>();

                // GetBtn — 截图按钮（在 _ScreenShotRawImage 下）
                var getBtnTr = rawImgTr.Find("GetBtn");
                if (getBtnTr != null) {
                    _getBtn = getBtnTr.GetComponent<Button>();
                    _getBtn?.onClick.AddListener(OnGetScreenshotClicked);
                }
            }
        }

        // ── 打开时立即采集（不含截图） ──

        private void CollectAndDisplay() {
            _cachedFields = new Dictionary<string, string>();
            _cachedFiles = new Dictionary<string, byte[]>();

            var collectors = BugReporterSDK.GetCollectors();
            foreach (var collector in collectors) {
                if (!collector.IsEnabled) continue;
                // 截图采集器跳过（由 GetBtn 手动触发）
                if (collector is ScreenshotCollector) continue;
                // 视频采集器跳过（由 SelectVideoBtn 手动触发）
                if (collector is VideoCollector) continue;
                try {
                    var result = collector.Collect();
                    if (result.Fields != null) {
                        foreach (var kv in result.Fields)
                            _cachedFields[kv.Key] = kv.Value;
                    }
                    if (result.Files != null) {
                        foreach (var kv in result.Files)
                            _cachedFiles[kv.Key] = kv.Value;
                    }
                } catch (Exception e) {
                    Debug.LogWarning($"[BugReporter] Collector '{collector.Key}' failed: {e.Message}");
                }
            }

            PopulateInfoList(_cachedFields);
        }

        // ── GetBtn: 截图并刷新预览 ──

        private void OnGetScreenshotClicked() {
            _getBtn.interactable = false;

            // 先隐藏面板，截到干净的游戏画面
            _panelInstance.SetActive(false);
            StartCoroutine(DoCaptureScreenshot());
        }

        private IEnumerator DoCaptureScreenshot() {
            var screenshotCollector = BugReporterSDK.GetScreenshotCollector();
            if (screenshotCollector is { IsEnabled: true }) {
                yield return screenshotCollector.CaptureScreenshot();

                var result = screenshotCollector.Collect();
                if (result.Files != null &&
                    result.Files.TryGetValue("screenshot", out var pngBytes) &&
                    pngBytes != null) {
                    // 更新缓存
                    _cachedFiles["screenshot"] = pngBytes;
                }
            }

            // 重新显示面板
            _panelInstance.SetActive(true);

            // 刷新截图预览
            if (_cachedFiles != null &&
                _cachedFiles.TryGetValue("screenshot", out var png) && png != null) {
                ShowScreenshot(png);
            }

            if (_getBtn != null) {
                _getBtn.interactable = true;
            }
        }

        // ── FoldBtn: 展开/收起采集信息 ──

        private void OnFoldClicked() {
            _infoExpanded = !_infoExpanded;
            foreach (var item in _infoItems) {
                if (item != null) item.SetActive(_infoExpanded);
            }
            if (_foldBtnText != null) {
                _foldBtnText.text = _infoExpanded ? "收起" : "展开";
            }
        }

        // ── SelectVideoBtn: 选择视频 ──

        private void OnSelectVideoClicked() {
            var videoCollector = BugReporterSDK.GetVideoCollector();
            if (videoCollector == null) {
                Debug.LogWarning("[BugReporter] VideoCollector not enabled.");
                return;
            }

            _selectVideoBtn.interactable = false;
            videoCollector.PickVideo((success, msg) => {
                if (_videoKeyText != null) {
                    _videoKeyText.text = success ? msg : "录屏视频（未选择）";
                }
                if (_selectVideoBtn != null) {
                    _selectVideoBtn.interactable = true;
                }
            });
        }

        // ── CollectBtn: 上报 ──

        private void OnSubmitClicked() {
            _collectBtn.interactable = false;
            StartCoroutine(DoSubmit());
        }

        private IEnumerator DoSubmit() {
            // 读取用户输入的标题
            var issueTitle = _issueTitleInput != null ? _issueTitleInput.text : "";

            var report = new BugReport {
                AppId = BugReporterSDK.GetConfig().appId,
                Description = issueTitle,
                Fields = _cachedFields ?? new Dictionary<string, string>(),
                Files = _cachedFiles ?? new Dictionary<string, byte[]>(),
            };

            // 标题也作为字段上报
            if (!string.IsNullOrEmpty(issueTitle)) {
                report.Fields["issueTitle"] = issueTitle;
            }

            // 合并视频采集器的数据
            var videoCollector = BugReporterSDK.GetVideoCollector();
            if (videoCollector is { HasVideo: true }) {
                try {
                    var videoResult = videoCollector.Collect();
                    if (videoResult.Fields != null) {
                        foreach (var kv in videoResult.Fields)
                            report.Fields[kv.Key] = kv.Value;
                    }
                    if (videoResult.Files != null) {
                        foreach (var kv in videoResult.Files)
                            report.Files[kv.Key] = kv.Value;
                    }
                } catch (Exception e) {
                    Debug.LogWarning($"[BugReporter] VideoCollector failed: {e.Message}");
                }
            }

            yield return BugReporterSDK.GetUploader().Upload(report, (success, msg) => {
                Debug.Log(success
                    ? "[BugReporter] Report submitted."
                    : $"[BugReporter] Report failed: {msg}");
            });

            if (_collectBtn != null) {
                _collectBtn.interactable = true;
            }
        }

        // ── UI 填充 ──

        private void PopulateInfoList(Dictionary<string, string> fields) {
            if (_contentParent == null || _infoItemTemplate == null) return;

            // 清除之前生成的 item（保留模板、IssueTitle、VideoItem、Fold）
            for (var i = _contentParent.childCount - 1; i >= 0; i--) {
                var child = _contentParent.GetChild(i).gameObject;
                if (child != _infoItemTemplate && child != _issueTitleItem
                    && child != _videoItem && child != _foldItem) {
                    Destroy(child);
                }
            }

            _infoItems.Clear();

            foreach (var kv in fields) {
                var item = Instantiate(_infoItemTemplate, _contentParent);
                // 默认隐藏，等用户点展开才显示
                item.SetActive(_infoExpanded);

                var keyText = item.transform.Find("key")?.GetComponent<Text>();
                var valueText = item.transform.Find("value")?.GetComponent<Text>();

                if (keyText != null) keyText.text = kv.Key + ":";
                if (valueText != null) {
                    valueText.text = kv.Value != null && kv.Value.Length > 200
                        ? kv.Value.Substring(0, 200) + "..."
                        : kv.Value ?? "";
                }

                _infoItems.Add(item);
            }
        }

        private void ShowScreenshot(byte[] pngBytes) {
            if (_screenshotRawImage == null) return;

            var tex = new Texture2D(2, 2);
            if (tex.LoadImage(pngBytes)) {
                // 释放旧纹理
                if (_screenshotRawImage.texture != null &&
                    _screenshotRawImage.texture is Texture2D oldTex) {
                    Destroy(oldTex);
                }
                _screenshotRawImage.texture = tex;
            }
        }
    }
}

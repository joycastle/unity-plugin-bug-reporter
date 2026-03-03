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

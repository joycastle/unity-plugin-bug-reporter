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

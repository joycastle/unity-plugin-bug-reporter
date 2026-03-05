using JoyCastle.BugReporter;
using UnityEngine;

public class BugReporterTestInit : MonoBehaviour {
    [SerializeField] private BugReporterConfig config;

    private void Awake() {
        BugReporterSDK.Init(config);
        // 注册一个测试用的自定义采集器
        BugReporterSDK.RegisterCollector(new TestGameCollector());
    }

    private void OnGUI() {
        if (GUI.Button(new Rect(10, 10, 200, 60), "Show Bug Report UI")) {
            BugReporterSDK.ShowReportUI();
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

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

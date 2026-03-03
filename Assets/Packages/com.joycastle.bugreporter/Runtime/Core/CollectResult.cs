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

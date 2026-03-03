using System.Collections.Generic;

namespace JoyCastle.BugReporter {
    public class BugReport {
        public string AppId { get; set; }
        public string Description { get; set; }
        public Dictionary<string, string> Fields { get; set; } = new();
        public Dictionary<string, byte[]> Files { get; set; } = new();
    }
}

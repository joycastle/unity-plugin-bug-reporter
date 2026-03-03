namespace JoyCastle.BugReporter {
    public interface IInfoCollector {
        string Key { get; }
        bool IsEnabled { get; }
        CollectResult Collect();
    }
}

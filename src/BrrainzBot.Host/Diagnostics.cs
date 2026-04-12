namespace BrrainzBot.Host;

public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error
}

public sealed record DiagnosticMessage(DiagnosticSeverity Severity, string Code, string Message);

public sealed class DiagnosticReport
{
    private readonly List<DiagnosticMessage> _messages = [];

    public IReadOnlyList<DiagnosticMessage> Messages => _messages;
    public bool HasErrors => _messages.Any(m => m.Severity == DiagnosticSeverity.Error);

    public void Add(DiagnosticSeverity severity, string code, string message) => _messages.Add(new DiagnosticMessage(severity, code, message));

    public void AddInfo(string code, string message) => Add(DiagnosticSeverity.Info, code, message);
    public void AddWarning(string code, string message) => Add(DiagnosticSeverity.Warning, code, message);
    public void AddError(string code, string message) => Add(DiagnosticSeverity.Error, code, message);
}

namespace FUREQ.Workspace.Domain;

public sealed record WorkspaceTool(string ToolId, string DisplayName, string Description, string Version,
    string EntryPoint, string SideEffectClassification, int TimeoutSeconds, IReadOnlyList<string> Arguments);

public sealed record WorkspaceToolRunResult(string ToolId, int ExitCode, TimeSpan Duration);

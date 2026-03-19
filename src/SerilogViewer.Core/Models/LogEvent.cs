namespace SerilogViewer.Core.Models;

/// <summary>
/// Represents a single parsed log event from a Serilog log file.
/// </summary>
public sealed class LogEvent
{
    public DateTimeOffset Timestamp { get; set; }

    public string Level { get; set; } = "Information";

    public string RenderedMessage { get; set; } = string.Empty;

    public string? MessageTemplate { get; set; }

    public string? Exception { get; set; }

    public Dictionary<string, object?> Properties { get; set; } = new();

    /// <summary>
    /// The original raw JSON line for reference.
    /// </summary>
    public string? RawJson { get; set; }
}

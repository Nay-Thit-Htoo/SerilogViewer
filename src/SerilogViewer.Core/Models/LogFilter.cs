namespace SerilogViewer.Core.Models;

/// <summary>
/// Encapsulates filtering parameters for log queries.
/// </summary>
public sealed class LogFilter
{
    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public string? SearchKeyword { get; set; }

    public string? LogLevel { get; set; }
}

using SerilogViewer.Core.Models;

namespace SerilogViewer.Core.Interfaces;

/// <summary>
/// Abstraction for reading and filtering Serilog log files from a directory.
/// </summary>
public interface ILogReaderService
{
    /// <summary>
    /// Reads log events from the specified directory path, finding JSON files by date and optionally applying the given filter.
    /// </summary>
    Task<IReadOnlyList<LogEvent>> ReadLogsAsync(string directoryPath, LogFilter? filter = null, CancellationToken cancellationToken = default);
}

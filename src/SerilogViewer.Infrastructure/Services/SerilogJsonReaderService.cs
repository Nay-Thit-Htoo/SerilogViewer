using System.Text.Json;
using SerilogViewer.Core.Interfaces;
using SerilogViewer.Core.Models;

namespace SerilogViewer.Infrastructure.Services;

/// <summary>
/// Reads Serilog Compact Log Event Format (CLEF) or JSON files from a directory.
/// Each line in the file is a JSON object with keys like @t, @l, @m/@mt, @x.
/// </summary>
public sealed class SerilogJsonReaderService : ILogReaderService
{
    private static readonly Dictionary<string, string> LevelMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["VRB"] = "Verbose",
        ["DBG"] = "Debug",
        ["INF"] = "Information",
        ["WRN"] = "Warning",
        ["ERR"] = "Error",
        ["FTL"] = "Fatal"
    };

    public async Task<IReadOnlyList<LogEvent>> ReadLogsAsync(
        string directoryPath,
        LogFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<LogEvent>();

        if (!Directory.Exists(directoryPath))
            return results.AsReadOnly();

        var files = Directory.GetFiles(directoryPath, "*.json");

        foreach (var file in files)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            
            // Expected format: yyyyMMdd
            if (DateTime.TryParseExact(fileName, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var fileDate))
            {
                // Filter by file date based on UI date range
                if (filter?.StartDate.HasValue == true && fileDate.Date < filter.StartDate.Value.Date)
                    continue;

                if (filter?.EndDate.HasValue == true && fileDate.Date > filter.EndDate.Value.Date)
                    continue;

                using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);

                string? line;
                while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    try
                    {
                        var logEvent = ParseLine(line);
                        if (logEvent != null && PassesFilter(logEvent, filter))
                            results.Add(logEvent);
                    }
                    catch (JsonException)
                    {
                        // Skip malformed lines
                    }
                }
            }
        }

        // Sort by timestamp descending so newest logs are first
        return results.OrderByDescending(x => x.Timestamp).ToList().AsReadOnly();
    }

    private static LogEvent? ParseLine(string line)
    {
        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;

        if (!root.TryGetProperty("Timestamp", out var tsElement) && 
            !root.TryGetProperty("@t", out tsElement))
            return null;

        if (!DateTimeOffset.TryParse(tsElement.GetString(), out var timestamp))
            return null;

        var level = "Information";
        if (root.TryGetProperty("Level", out var lvlElement) || 
            root.TryGetProperty("@l", out lvlElement))
        {
            var rawLevel = lvlElement.GetString() ?? "Information";
            level = LevelMap.TryGetValue(rawLevel, out var mapped) ? mapped : rawLevel;
        }

        var message = string.Empty;
        string? messageTemplate = null;

        if (root.TryGetProperty("RenderedMessage", out var msgElement) ||
            root.TryGetProperty("Message", out msgElement) ||
            root.TryGetProperty("@m", out msgElement))
        {
            message = msgElement.GetString() ?? string.Empty;
        }

        if (root.TryGetProperty("MessageTemplate", out var mtElement) ||
            root.TryGetProperty("@mt", out mtElement))
        {
            messageTemplate = mtElement.GetString();
            if (string.IsNullOrEmpty(message))
                message = messageTemplate ?? string.Empty;
        }

        string? exception = null;
        if (root.TryGetProperty("Exception", out var exElement) ||
            root.TryGetProperty("@x", out exElement))
        {
            exception = exElement.GetString();
        }

        var properties = new Dictionary<string, object?>();

        if (root.TryGetProperty("Properties", out var propsElement) && propsElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in propsElement.EnumerateObject())
            {
                properties[prop.Name] = ExtractValue(prop.Value);
            }
        }
        else
        {
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Name.StartsWith("@") || prop.Name == "Timestamp" || prop.Name == "Level" || 
                    prop.Name == "MessageTemplate" || prop.Name == "RenderedMessage" || prop.Name == "Exception")
                    continue;

                properties[prop.Name] = ExtractValue(prop.Value);
            }
        }

        return new LogEvent
        {
            Timestamp = timestamp,
            Level = level,
            RenderedMessage = message,
            MessageTemplate = messageTemplate,
            Exception = exception,
            Properties = properties,
            RawJson = line
        };
    }

    private static object? ExtractValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }

    private static bool PassesFilter(LogEvent logEvent, LogFilter? filter)
    {
        if (filter is null)
            return true;

        if (filter.StartDate.HasValue && logEvent.Timestamp < new DateTimeOffset(filter.StartDate.Value))
            return false;

        if (filter.EndDate.HasValue && logEvent.Timestamp > new DateTimeOffset(filter.EndDate.Value.Date.AddDays(1)))
            return false;

        if (!string.IsNullOrWhiteSpace(filter.LogLevel) &&
            !string.Equals(logEvent.Level, filter.LogLevel, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrWhiteSpace(filter.SearchKeyword))
        {
            var keyword = filter.SearchKeyword;

            var inMessage = logEvent.RenderedMessage.Contains(keyword, StringComparison.OrdinalIgnoreCase);
            var inException = logEvent.Exception?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false;
            var inProperties = logEvent.Properties.Any(p =>
                p.Value?.ToString()?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false);

            if (!inMessage && !inException && !inProperties)
                return false;
        }

        return true;
    }
}

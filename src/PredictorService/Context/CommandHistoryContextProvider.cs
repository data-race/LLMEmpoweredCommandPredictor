using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LLMEmpoweredCommandPredictor.PredictorService.Context;

/// <summary>
/// Context provider that reads PowerShell command history from PSReadLine history file.
/// This is much simpler than maintaining our own history - we just read the actual PowerShell history!
/// </summary>
public class CommandHistoryContextProvider : IContextProvider
{
    private readonly ILogger<CommandHistoryContextProvider> _logger;
    private readonly string _historyFilePath;

    public string Name => "CommandHistoryProvider";
    public int Priority => 100; // High priority for command history
    public bool IsAvailable { get; private set; }

    public CommandHistoryContextProvider(ILogger<CommandHistoryContextProvider> logger)
    {
        _logger = logger;
        
        // Get the PowerShell history file path
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _historyFilePath = Path.Combine(appDataPath, "Microsoft", "Windows", "PowerShell", "PSReadLine", "ConsoleHost_history.txt");
        
        IsAvailable = false;
    }

    /// <summary>
    /// Initializes the context provider by checking if the history file exists.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing Command History Context Provider...");
        _logger.LogInformation("Looking for PowerShell history at: {HistoryPath}", _historyFilePath);

        try
        {
            await Task.Delay(1, cancellationToken); // Make it async

            IsAvailable = File.Exists(_historyFilePath);
            
            if (IsAvailable)
            {
                var lineCount = await CountHistoryLinesAsync(cancellationToken);
                _logger.LogInformation("Command History Context Provider initialized successfully. Found {Count} history entries", lineCount);
            }
            else
            {
                _logger.LogWarning("PowerShell history file not found at: {HistoryPath}", _historyFilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Command History Context Provider");
            IsAvailable = false;
            throw;
        }
    }

    /// <summary>
    /// Collects context information by reading the PowerShell history file.
    /// </summary>
    public async Task<PredictorContext> CollectContextAsync(string userInput, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Collecting context for user input: '{UserInput}'", userInput);

        if (!IsAvailable)
        {
            _logger.LogWarning("Command History Provider is not available");
            return CreateEmptyContext(userInput);
        }

        try
        {
            var historyEntries = await ReadHistoryFileAsync(cancellationToken);

            var context = new PredictorContext
            {
                UserInput = userInput ?? string.Empty,
                WorkingDirectory = Environment.CurrentDirectory,
                CommandHistory = historyEntries,
                Timestamp = DateTime.UtcNow,
                SessionId = Environment.MachineName + "_" + Environment.UserName,
                PowerShellVersion = "7.0+", // Could be detected more accurately
                OperatingSystem = Environment.OSVersion.ToString()
            };

            _logger.LogDebug("Context collected: {Summary}", context.GetSummary());
            
            // Debug output: show a few recent history entries
            if (historyEntries.Count > 0)
            {
                _logger.LogInformation("Recent PowerShell history (last {Count} commands):", Math.Min(5, historyEntries.Count));
                var recentEntries = historyEntries.TakeLast(5).ToList();
                for (int i = 0; i < recentEntries.Count; i++)
                {
                    _logger.LogInformation("  [{Index}] {Command}", i + 1, recentEntries[i].Command);
                }
            }
            
            return context;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting context for input: '{UserInput}'", userInput);
            return CreateEmptyContext(userInput);
        }
    }

    /// <summary>
    /// Updates context - for this provider, we don't need to do anything since PowerShell manages the history file.
    /// </summary>
    public async Task UpdateContextAsync(
        PredictorContext context, 
        string executedCommand, 
        bool wasSuccessful, 
        CancellationToken cancellationToken = default)
    {
        // No need to update anything - PowerShell will write to the history file automatically
        await Task.Delay(1, cancellationToken);
        
        _logger.LogDebug("Context update requested for command: '{Command}' - PowerShell handles this automatically", 
            executedCommand);
    }

    /// <summary>
    /// Cleans up resources.
    /// </summary>
    public async Task DisposeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Disposing Command History Context Provider...");
        await Task.Delay(1, cancellationToken);
        
        IsAvailable = false;
        _logger.LogInformation("Command History Context Provider disposed");
    }

    /// <summary>
    /// Reads the PowerShell history file and converts it to CommandHistoryEntry objects.
    /// </summary>
    private async Task<IReadOnlyList<CommandHistoryEntry>> ReadHistoryFileAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Read the last 50 lines of the history file (most recent commands)
            var lines = await ReadLastLinesAsync(_historyFilePath, 50, cancellationToken);
            
            var historyEntries = new List<CommandHistoryEntry>();
            var baseTime = DateTime.UtcNow.AddMinutes(-lines.Count);

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i].Trim();
                if (!string.IsNullOrEmpty(line))
                {
                    var entry = new CommandHistoryEntry(
                        line,
                        baseTime.AddMinutes(i), // Approximate timestamps
                        true // We don't know if they succeeded, assume they did
                    )
                    {
                        WorkingDirectory = Environment.CurrentDirectory // We don't know the actual working dir
                    };
                    
                    historyEntries.Add(entry);
                }
            }

            _logger.LogDebug("Read {Count} history entries from PowerShell history file", historyEntries.Count);
            return historyEntries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading PowerShell history file: {HistoryPath}", _historyFilePath);
            return Array.Empty<CommandHistoryEntry>();
        }
    }

    /// <summary>
    /// Reads the last N lines from a file efficiently.
    /// </summary>
    private async Task<List<string>> ReadLastLinesAsync(string filePath, int lineCount, CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fileStream);
        
        var allLines = new List<string>();
        string? line;
        
        while ((line = await reader.ReadLineAsync()) != null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            allLines.Add(line);
        }
        
        // Return the last N lines
        var startIndex = Math.Max(0, allLines.Count - lineCount);
        return allLines.Skip(startIndex).ToList();
    }

    /// <summary>
    /// Counts the number of lines in the history file.
    /// </summary>
    private async Task<int> CountHistoryLinesAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var fileStream = new FileStream(_historyFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fileStream);
            
            int count = 0;
            while (await reader.ReadLineAsync() != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                count++;
            }
            
            return count;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Creates an empty context when the provider is unavailable.
    /// </summary>
    private PredictorContext CreateEmptyContext(string userInput)
    {
        return new PredictorContext
        {
            UserInput = userInput ?? string.Empty,
            WorkingDirectory = Environment.CurrentDirectory,
            CommandHistory = Array.Empty<CommandHistoryEntry>(),
            Timestamp = DateTime.UtcNow,
            SessionId = "unavailable",
            PowerShellVersion = "unknown",
            OperatingSystem = Environment.OSVersion.ToString()
        };
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LLMEmpoweredCommandPredictor.PredictorService.Context;

/// <summary>
/// Context provider that reads PowerShell command history from our custom session history file.
/// This provides session-specific commands that are more recent and relevant than global history.
/// </summary>
public class SessionHistoryContextProvider : IContextProvider
{
    private readonly ILogger<SessionHistoryContextProvider> _logger;
    private readonly string _sessionHistoryFilePath;
    private static readonly Regex HistoryLineRegex = new(@"^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}) \[(.*)\]$", RegexOptions.Compiled);

    public string Name => "SessionHistoryProvider";
    public int Priority => 90; // Slightly lower than global history but still high
    public bool IsAvailable { get; private set; }

    public SessionHistoryContextProvider(ILogger<SessionHistoryContextProvider> logger)
    {
        _logger = logger;
        
        // Get the session history file path - same as our PowerShell script uses
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var llmPredictorPath = Path.Combine(appDataPath, "LLMCommandPredictor");
        _sessionHistoryFilePath = Path.Combine(llmPredictorPath, "PowerShellSessionHistory.txt");
        
        IsAvailable = false;
    }

    /// <summary>
    /// Initializes the context provider by checking if the session history file exists.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing Session History Context Provider...");
        _logger.LogInformation("Looking for session history at: {HistoryPath}", _sessionHistoryFilePath);

        try
        {
            await Task.Delay(1, cancellationToken); // Make it async

            IsAvailable = File.Exists(_sessionHistoryFilePath);
            
            if (IsAvailable)
            {
                var lineCount = await CountHistoryLinesAsync(cancellationToken);
                _logger.LogInformation("Session History Context Provider initialized successfully. Found {Count} session history entries", lineCount);
            }
            else
            {
                _logger.LogWarning("Session history file not found at: {HistoryPath}. Make sure the session history collection script is running.", _sessionHistoryFilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Session History Context Provider");
            IsAvailable = false;
            throw;
        }
    }

    /// <summary>
    /// Collects context information by reading the session history file.
    /// </summary>
    public async Task<PredictorContext> CollectContextAsync(string userInput, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Collecting session context for user input: '{UserInput}'", userInput);

        if (!IsAvailable)
        {
            _logger.LogWarning("Session History Provider is not available");
            return CreateEmptyContext(userInput);
        }

        try
        {
            var sessionHistoryEntries = await ReadSessionHistoryFileAsync(cancellationToken);

            var context = new PredictorContext
            {
                UserInput = userInput ?? string.Empty,
                WorkingDirectory = Environment.CurrentDirectory,
                SessionHistory = sessionHistoryEntries,
                Timestamp = DateTime.UtcNow,
                SessionId = Environment.MachineName + "_" + Environment.UserName,
                PowerShellVersion = "7.0+", // Could be detected more accurately
                OperatingSystem = Environment.OSVersion.ToString()
            };

            _logger.LogDebug("Session context collected: UserInput: '{UserInput}', SessionHistoryCount: {Count}", 
                userInput, sessionHistoryEntries.Count);
            
            // Debug output: show a few recent session history entries
            if (sessionHistoryEntries.Count > 0)
            {
                _logger.LogInformation("Recent session history (last {Count} commands):", Math.Min(3, sessionHistoryEntries.Count));
                var recentEntries = sessionHistoryEntries.TakeLast(3).ToList();
                for (int i = 0; i < recentEntries.Count; i++)
                {
                    _logger.LogInformation("  [{Index}] {Timestamp} - {Command}", i + 1, 
                        recentEntries[i].ExecutedAt.ToString("HH:mm:ss"), recentEntries[i].Command);
                }
            }
            
            return context;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting session context for input: '{UserInput}'", userInput);
            return CreateEmptyContext(userInput);
        }
    }

    /// <summary>
    /// Updates context - for this provider, we don't need to do anything since our PowerShell script manages the session history file.
    /// </summary>
    public async Task UpdateContextAsync(
        PredictorContext context, 
        string executedCommand, 
        bool wasSuccessful, 
        CancellationToken cancellationToken = default)
    {
        // No need to update anything - our PowerShell script will write to the session history file automatically
        await Task.Delay(1, cancellationToken);
        
        _logger.LogDebug("Session context update requested for command: '{Command}' - PowerShell script handles this automatically", 
            executedCommand);
    }

    /// <summary>
    /// Cleans up resources.
    /// </summary>
    public async Task DisposeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Disposing Session History Context Provider...");
        await Task.Delay(1, cancellationToken);
        
        IsAvailable = false;
        _logger.LogInformation("Session History Context Provider disposed");
    }

    /// <summary>
    /// Reads the session history file and converts it to CommandHistoryEntry objects.
    /// The session history file format is: "2025-09-09 23:00:34 [command text]"
    /// </summary>
    private async Task<IReadOnlyList<CommandHistoryEntry>> ReadSessionHistoryFileAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Read all lines from the session history file
            var lines = await File.ReadAllLinesAsync(_sessionHistoryFilePath, cancellationToken);
            
            var historyEntries = new List<CommandHistoryEntry>();

            foreach (var line in lines)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                {
                    continue; // Skip empty lines and comments
                }

                var match = HistoryLineRegex.Match(line.Trim());
                if (match.Success)
                {
                    var timestampStr = match.Groups[1].Value;
                    var command = match.Groups[2].Value;

                    if (DateTime.TryParseExact(timestampStr, "yyyy-MM-dd HH:mm:ss", 
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp))
                    {
                        var entry = new CommandHistoryEntry(
                            command,
                            timestamp,
                            true // We assume commands in session history were successful
                        )
                        {
                            WorkingDirectory = Environment.CurrentDirectory // We don't track working dir in session history yet
                        };
                        
                        historyEntries.Add(entry);
                    }
                    else
                    {
                        _logger.LogWarning("Could not parse timestamp from session history line: {Line}", line);
                    }
                }
                else if (!line.Contains("Session ended"))
                {
                    _logger.LogWarning("Could not parse session history line format: {Line}", line);
                }
            }

            // reverse the history to make sure the order
            historyEntries.Reverse();

            _logger.LogDebug("Read {Count} session history entries from file", historyEntries.Count);
            return historyEntries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading session history file: {HistoryPath}", _sessionHistoryFilePath);
            return Array.Empty<CommandHistoryEntry>();
        }
    }

    /// <summary>
    /// Counts the number of valid lines in the session history file.
    /// </summary>
    private async Task<int> CountHistoryLinesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var lines = await File.ReadAllLinesAsync(_sessionHistoryFilePath, cancellationToken);
            return lines.Count(line => !string.IsNullOrWhiteSpace(line) && 
                                     !line.StartsWith("#") && 
                                     HistoryLineRegex.IsMatch(line.Trim()));
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
            SessionHistory = Array.Empty<CommandHistoryEntry>(),
            Timestamp = DateTime.UtcNow,
            SessionId = "unavailable",
            PowerShellVersion = "unknown",
            OperatingSystem = Environment.OSVersion.ToString()
        };
    }
}

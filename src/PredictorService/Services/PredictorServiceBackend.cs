using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using LLMEmpoweredCommandPredictor.Protocol.Integration;
using LLMEmpoweredCommandPredictor.Protocol.Models;
using LLMEmpoweredCommandPredictor.Protocol.Contracts;
using LLMEmpoweredCommandPredictor.PredictorService.Context;
using LLMEmpoweredCommandPredictor.PredictorCache;
using System.Text.Json;

namespace LLMEmpoweredCommandPredictor.PredictorService.Services;

public class PredictorServiceBackend : ISuggestionService, IDisposable
{
    private readonly ILogger<PredictorServiceBackend> _logger;
    private readonly ContextManager _contextManager;
    private readonly AzureOpenAIService? _azureOpenAIService;
    private readonly PromptTemplateService? _promptTemplateService;
    private readonly InMemoryCache _cache;
    private readonly CacheKeyGenerator _keyGenerator;
    private readonly CommandValidationService _validationService;

    public PredictorServiceBackend(
        ILogger<PredictorServiceBackend> logger,
        ContextManager contextManager,
        InMemoryCache cache,
        CacheKeyGenerator keyGenerator,
        CommandValidationService validationService,
        AzureOpenAIService? azureOpenAIService = null,
        PromptTemplateService? promptTemplateService = null)
    {
        _logger = logger;
        _contextManager = contextManager;
        _cache = cache;
        _keyGenerator = keyGenerator;
        _validationService = validationService;
        _azureOpenAIService = azureOpenAIService;
        _promptTemplateService = promptTemplateService;
        
        _logger.LogInformation("PredictorServiceBackend: Initialized with cache support and suggestion generation");
    }


    /// <summary>
    /// Creates a SuggestionRequest from context for cache operations
    /// </summary>
    private SuggestionRequest CreateSuggestionRequestFromContext(object context, int maxSuggestions)
    {
        try
        {
            // Handle SuggestionRequest directly
            if (context is SuggestionRequest request)
            {
                return request;
            }

            // Try to extract UserInput from context
            var userInput = GetUserInputFromContext(context);
            return new SuggestionRequest(userInput, maxSuggestions: maxSuggestions);
        }
        catch
        {
            return new SuggestionRequest("", maxSuggestions: maxSuggestions);
        }
    }

    /// <summary>
    /// Extracts user input from the context object
    /// </summary>
    private string GetUserInputFromContext(object context)
    {
        try
        {
            // Try to get UserInput property via reflection
            var userInputProperty = context.GetType().GetProperty("UserInput");
            if (userInputProperty != null)
            {
                return userInputProperty.GetValue(context)?.ToString() ?? "";
            }

            // Fallback to string representation
            return context.ToString() ?? "";
        }
        catch
        {
            return "";
        }
    }


    #region ISuggestionService Implementation
    
    /// <inheritdoc />
    public async Task<SuggestionResponse> GetSuggestionsAsync(
        SuggestionRequest request, 
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            return new SuggestionResponse(
                suggestions: new List<ProtocolSuggestion>(),
                source: "error",
                warningMessage: "Invalid request"
            );
        }

        try
        {
            // 1. Generate cache key and prefix keys
            var cacheKey = _keyGenerator.GenerateCacheKey(request);
            
            // 2. Check exact cache match first
            var cachedResponse = await _cache.GetAsync(cacheKey, cancellationToken);
            if (cachedResponse != null)
            {
                try
                {
                    // Try to parse the simplified JSON format
                    var cachedSuggestionResponse = ParseCachedResponse(cachedResponse);
                    if (cachedSuggestionResponse != null)
                    {
                        // Update cache metadata
                        cachedSuggestionResponse.IsFromCache = true;
                        cachedSuggestionResponse.CachedTimestamp = DateTime.UtcNow;
                        cachedSuggestionResponse.GenerationTimeMs = 1.0; // Very fast cache retrieval
                        
                        _logger.LogInformation("PredictorServiceBackend: Exact cache HIT for '{UserInput}' - returning {Count} suggestions: [{Suggestions}]", 
                            request.UserInput, cachedSuggestionResponse.Suggestions?.Count ?? 0, 
                            string.Join(", ", cachedSuggestionResponse.Suggestions?.Select(s => $"'{s.SuggestionText}'") ?? new List<string>()));
                        return cachedSuggestionResponse;
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning("PredictorServiceBackend: Failed to deserialize cached response: {Error}", ex.Message);
                    // Remove corrupted cache entry
                    await _cache.RemoveAsync(cacheKey, cancellationToken);
                }
            }

            // 3. Try prefix-based cache lookup
            var prefixKeys = _keyGenerator.FindMatchingPrefixKeys(request);
                if (prefixKeys.Count > 0)
                {
                    // Filter out generic partial input keys
                    var filteredPrefixKeys = prefixKeys.Where(k => !IsGenericPartialInput(k)).ToList();
                    if (filteredPrefixKeys.Count > 0)
                    {
                        var prefixCachedResponse = await _cache.GetByPrefixAsync(filteredPrefixKeys, cancellationToken);
                        if (prefixCachedResponse != null)
                        {
                            try
                            {
                                var prefixSuggestionResponse = ParseCachedResponse(prefixCachedResponse);
                                if (prefixSuggestionResponse != null)
                                {
                                    // Update cache metadata
                                    prefixSuggestionResponse.IsFromCache = true;
                                    prefixSuggestionResponse.CachedTimestamp = DateTime.UtcNow;
                                    prefixSuggestionResponse.GenerationTimeMs = 2.0; // Slightly slower prefix retrieval
                                
                                    _logger.LogInformation("PredictorServiceBackend: Prefix cache HIT for '{UserInput}' - returning {Count} suggestions: [{Suggestions}]", 
                                        request.UserInput, prefixSuggestionResponse.Suggestions?.Count ?? 0,
                                        string.Join(", ", prefixSuggestionResponse.Suggestions?.Select(s => $"'{s.SuggestionText}'") ?? new List<string>()));
                                    return prefixSuggestionResponse;
                                }
                            }
                            catch (JsonException ex)
                            {
                                _logger.LogWarning("PredictorServiceBackend: Failed to deserialize prefix cached response: {Error}", ex.Message);
                            }
                        }
                    }
                }

            _logger.LogInformation("PredictorServiceBackend: Cache MISS for '{UserInput}', generating new suggestions", request.UserInput);

            // 3. Cache miss - generate suggestions for partial input
            var generatedSuggestions = GenerateSuggestionsForPartialInput(request.UserInput);
            
            if (generatedSuggestions.Count > 0)
            {
                var response = new SuggestionResponse(
                    suggestions: generatedSuggestions,
                    source: "generated",
                    isFromCache: false,
                    generationTimeMs: 10.0
                );
                
                // Only cache suggestions for longer, more specific inputs
                // Don't cache generic suggestions for very short partial inputs like "gi", "do", etc.
                if (request.UserInput.Length >= 4 && !IsGenericPartialInput(request.UserInput))
                {
                    try
                    {
                        var jsonResponse = System.Text.Json.JsonSerializer.Serialize(response);
                        await _cache.SetAsync(cacheKey, jsonResponse, cancellationToken);
                        _logger.LogDebug("PredictorServiceBackend: Cached suggestions for specific input: '{UserInput}'", request.UserInput);
                    }
                    catch (Exception cacheEx)
                    {
                        _logger.LogWarning("PredictorServiceBackend: Failed to cache generated suggestions: {Error}", cacheEx.Message);
                    }
                }
                else
                {
                    _logger.LogDebug("PredictorServiceBackend: Skipping cache for generic partial input: '{UserInput}'", request.UserInput);
                }
                
                return response;
            }
            else
            {
                return new SuggestionResponse(
                    suggestions: new List<ProtocolSuggestion>(),
                    source: "generated",
                    warningMessage: "No suggestions available for this input"
                );
            }
        }
        catch (OperationCanceledException)
        {
            return new SuggestionResponse(
                suggestions: new List<ProtocolSuggestion>(),
                source: "cancelled",
                warningMessage: "Request was cancelled"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PredictorServiceBackend: Error getting suggestions");
            return new SuggestionResponse(
                suggestions: new List<ProtocolSuggestion>(),
                source: "error",
                warningMessage: $"Service error: {ex.Message}"
            );
        }
    }

    /// <inheritdoc />
    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await Task.FromResult(true);
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task ClearCacheAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _cache.ClearAsync(cancellationToken);
            _logger.LogInformation("PredictorServiceBackend: Cache cleared");
        }
        catch (Exception ex)
        {
            _logger.LogWarning("PredictorServiceBackend: Failed to clear cache: {Error}", ex.Message);
        }
    }

    /// <inheritdoc />
    Task<ServiceStatus> ISuggestionService.GetStatusAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(new ServiceStatus(
            isRunning: true,
            uptime: TimeSpan.FromMilliseconds(Environment.TickCount64),
            cachedSuggestionsCount: _cache.GetStatistics().TotalEntries,
            totalRequestsProcessed: _cache.GetStatistics().TotalRequests,
            averageResponseTimeMs: _cache.GetStatistics().CacheHits > 0 ? 5.0 : 150.0,
            lastCacheUpdate: _cache.GetStatistics().LastAccess,
            version: "1.0.0",
            memoryUsageMb: _cache.GetStatistics().MemoryUsageBytes / (1024.0 * 1024.0)
        ));
    }

    /// <inheritdoc />
    public async Task TriggerCacheRefreshAsync(
        SuggestionRequest request, 
        CancellationToken cancellationToken = default)
    {
        // Cache-only mode - no refresh capability
        _logger.LogInformation("PredictorServiceBackend: Cache refresh requested but running in cache-only mode");
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task SaveCommandAsync(string commandLine, bool success, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            _logger.LogDebug("PredictorServiceBackend: Ignoring empty command line");
            return;
        }

        try
        {
            _logger.LogInformation("PredictorServiceBackend: Saving command to cache: '{CommandLine}' (success: {Success})", 
                commandLine, success);

            // Generate cache key for the command
            var request = new SuggestionRequest(commandLine, maxSuggestions: 5);
            var cacheKey = _keyGenerator.GenerateCacheKey(request);
            
            // Check if we already have this command cached to avoid overwriting
            var existingCache = await _cache.GetAsync(cacheKey, cancellationToken);
            if (existingCache != null && !success)
            {
                // Don't overwrite successful cached commands with failed ones
                _logger.LogDebug("PredictorServiceBackend: Command already cached, skipping failed execution: '{CommandLine}'", commandLine);
                return;
            }
            
            // For failed commands, only cache if we don't have a successful version
            if (!success && existingCache == null)
            {
                _logger.LogDebug("PredictorServiceBackend: Skipping failed command (no successful version exists): '{CommandLine}'", commandLine);
                return;
            }
            
            // Create suggestions based on the executed command
            var suggestions = GenerateSuggestionsForCommand(commandLine);
            if (suggestions.Count > 0)
            {
                // Convert suggestions to JSON format for cache storage
                var suggestionResponse = new SuggestionResponse(
                    suggestions: suggestions,
                    source: "user_command",
                    isFromCache: false,
                    generationTimeMs: 1.0
                );

                var jsonResponse = System.Text.Json.JsonSerializer.Serialize(suggestionResponse);
                
                // Save to cache
                await _cache.SetAsync(cacheKey, jsonResponse, cancellationToken);
                
                _logger.LogInformation("PredictorServiceBackend: Successfully cached {Count} suggestions for command: '{CommandLine}'", 
                    suggestions.Count, commandLine);
            }
            else
            {
                _logger.LogDebug("PredictorServiceBackend: No suggestions generated for command: '{CommandLine}'", commandLine);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("PredictorServiceBackend: Failed to save command to cache: {Error}", ex.Message);
        }
    }

    #endregion

    /// <summary>
    /// Generates suggestions based on partial user input.
    /// This handles partial commands and provides relevant completions.
    /// </summary>
    private List<ProtocolSuggestion> GenerateSuggestionsForPartialInput(string userInput)
    {
        var suggestions = new List<ProtocolSuggestion>();
        var input = userInput.Trim().ToLowerInvariant();

        try
        {
            // Handle partial Git commands
            if (input.StartsWith("git") && input.Length <= 4)
            {
                suggestions.AddRange(new[]
                {
                    new ProtocolSuggestion("git status"),
                    new ProtocolSuggestion("git add ."),
                    new ProtocolSuggestion("git commit -m \"\""),
                    new ProtocolSuggestion("git push"),
                    new ProtocolSuggestion("git pull")
                });
            }
            else if (input.StartsWith("git "))
            {
                // Use existing logic for complete git commands
                return GenerateSuggestionsForCommand(userInput);
            }
            // Handle partial "gi" -> Git suggestions
            else if (input.StartsWith("gi") && input.Length <= 2)
            {
                suggestions.AddRange(new[]
                {
                    new ProtocolSuggestion("git status"),
                    new ProtocolSuggestion("git add ."),
                    new ProtocolSuggestion("git commit -m \"\""),
                    new ProtocolSuggestion("git push"),
                    new ProtocolSuggestion("git pull")
                });
            }
            // Handle partial PowerShell Get- commands
            else if (input.StartsWith("get") && input.Length <= 4)
            {
                suggestions.AddRange(new[]
                {
                    new ProtocolSuggestion("Get-Process"),
                    new ProtocolSuggestion("Get-Service"),
                    new ProtocolSuggestion("Get-ChildItem"),
                    new ProtocolSuggestion("Get-Content"),
                    new ProtocolSuggestion("Get-Location")
                });
            }
            else if (input.StartsWith("get-"))
            {
                // Use existing logic for complete Get- commands
                return GenerateSuggestionsForCommand(userInput);
            }
            // Handle partial Docker commands
            else if (input.StartsWith("doc") && input.Length <= 6)
            {
                suggestions.AddRange(new[]
                {
                    new ProtocolSuggestion("docker ps"),
                    new ProtocolSuggestion("docker images"),
                    new ProtocolSuggestion("docker run"),
                    new ProtocolSuggestion("docker stop"),
                    new ProtocolSuggestion("docker build")
                });
            }
            else if (input.StartsWith("docker "))
            {
                // Use existing logic for complete docker commands
                return GenerateSuggestionsForCommand(userInput);
            }
            // Handle partial dotnet commands
            else if (input.StartsWith("dot") && input.Length <= 6)
            {
                suggestions.AddRange(new[]
                {
                    new ProtocolSuggestion("dotnet build"),
                    new ProtocolSuggestion("dotnet run"),
                    new ProtocolSuggestion("dotnet test"),
                    new ProtocolSuggestion("dotnet restore"),
                    new ProtocolSuggestion("dotnet clean")
                });
            }
            else if (input.StartsWith("dotnet "))
            {
                // Use existing logic for complete dotnet commands
                return GenerateSuggestionsForCommand(userInput);
            }
            // Handle other common partial inputs
            else if (input.StartsWith("ls") || input.StartsWith("dir"))
            {
                suggestions.AddRange(new[]
                {
                    new ProtocolSuggestion("Get-ChildItem"),
                    new ProtocolSuggestion("ls -la"),
                    new ProtocolSuggestion("dir"),
                    new ProtocolSuggestion("Get-ChildItem -Recurse"),
                    new ProtocolSuggestion("Get-ChildItem | Where-Object")
                });
            }
            else
            {
                // For unrecognized input, try the existing command generation logic
                var commandSuggestions = GenerateSuggestionsForCommand(userInput);
                if (commandSuggestions.Count > 0)
                {
                    return commandSuggestions;
                }
                
                // Generic fallback suggestions
                suggestions.AddRange(new[]
                {
                    new ProtocolSuggestion("Get-Process"),
                    new ProtocolSuggestion("Get-Service"),
                    new ProtocolSuggestion("git status"),
                    new ProtocolSuggestion("dotnet build"),
                    new ProtocolSuggestion("Get-ChildItem")
                });
            }

            _logger.LogDebug("PredictorServiceBackend: Generated {Count} suggestions for partial input: '{UserInput}'", 
                suggestions.Count, userInput);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PredictorServiceBackend: Error generating suggestions for partial input: '{UserInput}'", userInput);
        }

        return suggestions;
    }

    /// <summary>
    /// Generates suggestions based on a successfully executed command.
    /// This learns from user patterns and creates relevant suggestions.
    /// </summary>
    private List<ProtocolSuggestion> GenerateSuggestionsForCommand(string commandLine)
    {
        var suggestions = new List<ProtocolSuggestion>();
        var command = commandLine.Trim().ToLowerInvariant();
        var originalCommand = commandLine.Trim(); // Keep original casing

        try
        {
            // Always include the original command as the first suggestion
            suggestions.Add(new ProtocolSuggestion(originalCommand));

            // Git command patterns
            if (command.StartsWith("git "))
            {
                var gitCommand = command.Substring(4).Trim();
                
                switch (gitCommand.Split(' ')[0])
                {
                    case "status":
                        AddUniqueSuggestions(suggestions, new[]
                        {
                            "git add .",
                            "git commit -m \"\"",
                            "git diff",
                            "git log --oneline"
                        });
                        break;
                        
                    case "add":
                        AddUniqueSuggestions(suggestions, new[]
                        {
                            "git add .",
                            "git add -A",
                            "git commit -m \"\"",
                            "git status",
                            "git diff --cached"
                        });
                        break;
                        
                    case "commit":
                        AddUniqueSuggestions(suggestions, new[]
                        {
                            "git push",
                            "git status",
                            "git log --oneline",
                            "git show"
                        });
                        break;
                        
                    case "push":
                        AddUniqueSuggestions(suggestions, new[]
                        {
                            "git push origin main",
                            "git status",
                            "git pull",
                            "git log --oneline"
                        });
                        break;
                        
                    case "pull":
                        AddUniqueSuggestions(suggestions, new[]
                        {
                            "git status",
                            "git log --oneline",
                            "git diff",
                            "git push"
                        });
                        break;
                        
                    case "checkout":
                        AddUniqueSuggestions(suggestions, new[]
                        {
                            "git status",
                            "git branch",
                            "git log --oneline",
                            "git diff",
                            "git pull"
                        });
                        break;
                        
                    default:
                        // Generic git suggestions
                        AddUniqueSuggestions(suggestions, new[]
                        {
                            "git status",
                            "git add .",
                            "git commit -m \"\"",
                            "git push",
                            "git pull"
                        });
                        break;
                }
            }
            // Docker command patterns
            else if (command.StartsWith("docker "))
            {
                suggestions.AddRange(new[]
                {
                    new ProtocolSuggestion("docker ps"),
                    new ProtocolSuggestion("docker images"),
                    new ProtocolSuggestion("docker run"),
                    new ProtocolSuggestion("docker stop"),
                    new ProtocolSuggestion("docker build")
                });
            }
            // PowerShell Get- commands
            else if (command.StartsWith("get-"))
            {
                suggestions.AddRange(new[]
                {
                    new ProtocolSuggestion("Get-Process"),
                    new ProtocolSuggestion("Get-Service"),
                    new ProtocolSuggestion("Get-ChildItem"),
                    new ProtocolSuggestion("Get-Content"),
                    new ProtocolSuggestion("Get-Location")
                });
            }
            // dotnet commands
            else if (command.StartsWith("dotnet "))
            {
                suggestions.AddRange(new[]
                {
                    new ProtocolSuggestion("dotnet build"),
                    new ProtocolSuggestion("dotnet run"),
                    new ProtocolSuggestion("dotnet test"),
                    new ProtocolSuggestion("dotnet publish"),
                    new ProtocolSuggestion("dotnet restore")
                });
            }

            _logger.LogDebug("PredictorServiceBackend: Generated {Count} suggestions for command: '{CommandLine}'", 
                suggestions.Count, commandLine);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("PredictorServiceBackend: Error generating suggestions for command '{CommandLine}': {Error}", 
                commandLine, ex.Message);
        }

        return suggestions;
    }

    /// <summary>
    /// Parses the simplified cached JSON format and converts it to SuggestionResponse.
    /// Returns ALL suggestions from the cached array.
    /// </summary>
    private SuggestionResponse? ParseCachedResponse(string cachedJson)
    {
        try
        {
            // Parse the JSON format
            using var document = JsonDocument.Parse(cachedJson);
            var root = document.RootElement;

            if (!root.TryGetProperty("Suggestions", out var suggestionsElement))
                return null;

            var suggestions = new List<ProtocolSuggestion>();
            
            // Parse suggestions from the array - handle both string array and object array formats
            foreach (var suggestionElement in suggestionsElement.EnumerateArray())
            {
                string? suggestionText = null;
                
                if (suggestionElement.ValueKind == JsonValueKind.String)
                {
                    // Simple string format: ["cmd1", "cmd2"]
                    suggestionText = suggestionElement.GetString();
                }
                else if (suggestionElement.ValueKind == JsonValueKind.Object)
                {
                    // Object format: [{"SuggestionText":"cmd1","ToolTip":"..."}]
                    if (suggestionElement.TryGetProperty("SuggestionText", out var textElement))
                    {
                        suggestionText = textElement.GetString();
                    }
                }
                
                if (!string.IsNullOrEmpty(suggestionText))
                {
                    suggestions.Add(new ProtocolSuggestion(suggestionText));
                }
            }

            // If no valid suggestions found, return null
            if (suggestions.Count == 0)
            {
                _logger.LogWarning("PredictorServiceBackend: No valid suggestions found in cached response");
                return null;
            }

            _logger.LogDebug("PredictorServiceBackend: Parsed {Count} suggestions from cache", suggestions.Count);

            // Extract other properties if they exist
            var source = root.TryGetProperty("Source", out var sourceElement) ? sourceElement.GetString() ?? "cache" : "cache";
            var generationTimeMs = root.TryGetProperty("GenerationTimeMs", out var timeElement) ? timeElement.GetDouble() : 1.0;

            return new SuggestionResponse(
                suggestions: suggestions,
                source: source,
                generationTimeMs: generationTimeMs,
                isFromCache: false, // Will be set to true by caller
                warningMessage: null
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning("PredictorServiceBackend: Error parsing cached response: {Error}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Adds suggestions to the list only if they don't already exist (case-insensitive)
    /// </summary>
    private void AddUniqueSuggestions(List<ProtocolSuggestion> suggestions, string[] newSuggestions)
    {
        var existingTexts = new HashSet<string>(suggestions.Select(s => s.SuggestionText), StringComparer.OrdinalIgnoreCase);
        
        foreach (var suggestionText in newSuggestions)
        {
            if (!existingTexts.Contains(suggestionText))
            {
                suggestions.Add(new ProtocolSuggestion(suggestionText));
                existingTexts.Add(suggestionText);
            }
        }
    }

    /// <summary>
    /// Determines if an input is a generic partial input that should not be cached.
    /// </summary>
    private static bool IsGenericPartialInput(string input)
    {
        var normalized = input.ToLowerInvariant().Trim();
        
        // Don't cache very short generic prefixes that generate broad suggestions
        var genericPrefixes = new[] { "gi", "git", "do", "doc", "dot", "get", "ge" };
        return genericPrefixes.Contains(normalized);
    }

    /// <summary>
    /// Validates a command and returns validation results with feedback.
    /// </summary>
    /// <param name="commandLine">The command to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result with feedback on command correctness</returns>
    public Task<CommandValidationResponse> ValidateCommandAsync(string commandLine, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("PredictorServiceBackend: Validating command: '{Command}'", commandLine);
            
            // Use the validation service to check the command
            var result = _validationService.ValidateCommand(commandLine);
            
            _logger.LogDebug("PredictorServiceBackend: Command validation completed. IsValid: {IsValid}, Level: {Level}", 
                result.IsValid, result.ValidationLevel);
            
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PredictorServiceBackend: Error validating command '{Command}'", commandLine);
            
            var errorResult = new CommandValidationResponse
            {
                IsValid = false,
                ValidationLevel = ValidationLevel.Error,
                Message = $"Validation service error: {ex.Message}"
            };
            
            return Task.FromResult(errorResult);
        }
    }

    public void Dispose()
    {
        // Nothing to dispose
    }
}

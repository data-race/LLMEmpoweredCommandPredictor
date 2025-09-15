using System.Collections.Generic;
using System.Threading;
using System.Management.Automation.Subsystem.Prediction;
using System;
using System.Linq;
using LLMEmpoweredCommandPredictor.Protocol.Integration;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace LLMEmpoweredCommandPredictor;

/// <summary>
/// Default implementation of the ILLMSuggestionProvider interface.
/// </summary>
public class LLMSuggestionProvider : ILLMSuggestionProvider
{
    private readonly PluginHelper _pluginHelper;

    public LLMSuggestionProvider()
    {
        _pluginHelper = new PluginHelper();
    }

    /// <summary>
    /// Gets predictive suggestions based on the provided context.
    /// </summary>
    /// <param name="context">The context information used for generating suggestions.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A list of predictive suggestions.</returns>
    public List<PredictiveSuggestion> GetSuggestions(LLMSuggestionContext context, CancellationToken cancellationToken)
    {
        try
        {
            // Try to get suggestions from cache via named pipe
            var cacheSuggestions = QueryCacheViaNamedPipe(context.UserInput, 5, cancellationToken);
            
            return cacheSuggestions.Select(s => new PredictiveSuggestion(s.Command, s.Description)).ToList();
        }
        catch
        {
            // Fallback if named pipe fails
            try
            {
                return _pluginHelper.GetSuggestions(context, 5, cancellationToken).ToList();
            }
            catch
            {
                return new List<PredictiveSuggestion>{
                    new(string.Concat(context.UserInput, " (fallback)"))
                };
            }
        }
    }

    /// <summary>
    /// Queries the cache via named pipe for command suggestions.
    /// </summary>
    /// <param name="userInput">The user input to use as prefix for search</param>
    /// <param name="maxResults">Maximum number of results to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of cache suggestions</returns>
    private List<CacheSuggestion> QueryCacheViaNamedPipe(string userInput, int maxResults, CancellationToken cancellationToken)
    {
        const string pipeName = "LLMCommandPredictorCache";
        const int timeoutMs = 1000; // 1 second timeout
        
        try
        {
            using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
            
            // Try to connect with timeout
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeoutMs);
            
            client.Connect(timeoutMs);
            
            if (!client.IsConnected)
            {
                return new List<CacheSuggestion>();
            }

            // Prepare request
            var request = new CacheQueryRequest
            {
                Prefix = userInput?.Trim() ?? string.Empty,
                MaxResults = maxResults
            };
            
            var requestJson = JsonSerializer.Serialize(request);
            var requestBytes = Encoding.UTF8.GetBytes(requestJson);
            
            // Send request
            client.Write(requestBytes, 0, requestBytes.Length);
            client.Flush();
            
            // Read response
            var buffer = new byte[4096];
            var bytesRead = client.Read(buffer, 0, buffer.Length);
            
            if (bytesRead > 0)
            {
                var responseJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                var response = JsonSerializer.Deserialize<CacheQueryResponse>(responseJson);
                
                if (response?.Success == true && response.Suggestions != null)
                {
                    return response.Suggestions;
                }
            }
            
            return new List<CacheSuggestion>();
        }
        catch (TimeoutException)
        {
            // Service might not be running
            return new List<CacheSuggestion>();
        }
        catch (Exception)
        {
            // Any other error (service not available, etc.)
            return new List<CacheSuggestion>();
        }
    }

    /// <summary>
    /// Represents a cache query request.
    /// </summary>
    private class CacheQueryRequest
    {
        public string Prefix { get; set; } = string.Empty;
        public int MaxResults { get; set; } = 10;
    }

    /// <summary>
    /// Represents a cache query response.
    /// </summary>
    private class CacheQueryResponse
    {
        public bool Success { get; set; }
        public string Error { get; set; } = string.Empty;
        public List<CacheSuggestion> Suggestions { get; set; } = new List<CacheSuggestion>();
        public int TotalCacheSize { get; set; }
    }

    /// <summary>
    /// Represents a suggestion from the cache.
    /// </summary>
    private class CacheSuggestion
    {
        public string Command { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public float Confidence { get; set; }
        public DateTime Generated { get; set; }
    }
}

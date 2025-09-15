# Direct IPC connectivity test
Write-Host "Testing direct IPC connection to PredictorService..." -ForegroundColor Green

try {
    # Add the protocol assembly to the session
    Add-Type -Path "src\Protocol\bin\Debug\net6.0\LLMEmpoweredCommandPredictor.Protocol.dll"
    
    # Create a client with debug logging enabled
    $settings = New-Object LLMEmpoweredCommandPredictor.Protocol.Models.ConnectionSettings
    $settings.EnableDebugLogging = $true
    $settings.TimeoutMs = 1000  # Increase timeout for testing
    $settings.ConnectionTimeoutMs = 2000
    
    $client = New-Object LLMEmpoweredCommandPredictor.Protocol.Client.SuggestionServiceClient($settings)
    
    Write-Host "✓ Client created" -ForegroundColor Green
    
    # Test with a simple request
    $request = New-Object LLMEmpoweredCommandPredictor.Protocol.Models.SuggestionRequest("git", 5)
    
    Write-Host "Testing suggestion request for 'git'..." -ForegroundColor Yellow
    
    $response = $client.GetSuggestionsAsync($request, [System.Threading.CancellationToken]::None).GetAwaiter().GetResult()
    
    if ($response -and $response.Suggestions) {
        Write-Host "✓ IPC connection successful! Received $($response.Suggestions.Count) suggestions:" -ForegroundColor Green
        foreach ($suggestion in $response.Suggestions) {
            Write-Host "  - $($suggestion.SuggestionText)" -ForegroundColor White
        }
    } else {
        Write-Host "✗ IPC connection failed - no response or suggestions" -ForegroundColor Red
    }
    
    $client.Dispose()
    
} catch {
    Write-Host "✗ IPC connection failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Make sure PredictorService is running in the background" -ForegroundColor Yellow
    if ($_.Exception.InnerException) {
        Write-Host "Inner exception: $($_.Exception.InnerException.Message)" -ForegroundColor Red
    }
}
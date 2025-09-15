# Debug cache lookup with exact keys
Write-Host "Debugging cache key generation and lookup..." -ForegroundColor Green

try {
    # Load dependencies
    $protocolPath = "src\Protocol\bin\Debug\net6.0"
    $pluginPath = "src\PredictorPlugin\bin\Debug\LLMEmpoweredCommandPredictor\net6.0"
    
    Add-Type -Path (Join-Path $pluginPath "StreamJsonRpc.dll")
    Add-Type -Path (Join-Path $protocolPath "LLMEmpoweredCommandPredictor.Protocol.dll")
    
    # Test what cache key is generated for "git"
    Write-Host "Testing cache key generation for 'git':" -ForegroundColor Yellow
    
    # Create a suggestion request for "git"
    $request = New-Object LLMEmpoweredCommandPredictor.Protocol.Models.SuggestionRequest("git", 5)
    Write-Host "  Request created: UserInput='$($request.UserInput)'" -ForegroundColor White
    
    # Test the IPC call with debugging
    $settings = New-Object LLMEmpoweredCommandPredictor.Protocol.Models.ConnectionSettings
    $settings.EnableDebugLogging = $true
    $settings.TimeoutMs = 5000
    
    $client = New-Object LLMEmpoweredCommandPredictor.Protocol.Client.SuggestionServiceClient($settings)
    
    Write-Host "Making IPC request..." -ForegroundColor Yellow
    $response = $client.GetSuggestionsAsync($request, [System.Threading.CancellationToken]::None).GetAwaiter().GetResult()
    
    if ($response) {
        Write-Host "✓ Response received:" -ForegroundColor Green
        Write-Host "  - Suggestions count: $($response.Suggestions.Count)" -ForegroundColor White
        Write-Host "  - Source: $($response.Source)" -ForegroundColor White
        Write-Host "  - Warning: $($response.WarningMessage)" -ForegroundColor White
        Write-Host "  - IsFromCache: $($response.IsFromCache)" -ForegroundColor White
        
        if ($response.Suggestions.Count -gt 0) {
            Write-Host "  - Suggestions:" -ForegroundColor Green
            foreach ($suggestion in $response.Suggestions) {
                Write-Host "    * $($suggestion.SuggestionText)" -ForegroundColor Cyan
            }
        } else {
            Write-Host "  ❌ No suggestions returned - this confirms the cache lookup issue!" -ForegroundColor Red
        }
    } else {
        Write-Host "✗ No response received" -ForegroundColor Red
    }
    
    $client.Dispose()
    
} catch {
    Write-Host "✗ Error: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.InnerException) {
        Write-Host "Inner: $($_.Exception.InnerException.Message)" -ForegroundColor Red
    }
}
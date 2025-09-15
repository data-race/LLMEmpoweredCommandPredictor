# Simple IPC test to check if we can get Cathy's git commands
Write-Host "Testing IPC connection to get Cathy's cached git commands..." -ForegroundColor Green

try {
    # Load dependencies
    $protocolPath = "src\Protocol\bin\Debug\net6.0"
    $pluginPath = "src\PredictorPlugin\bin\Debug\LLMEmpoweredCommandPredictor\net6.0"
    
    Add-Type -Path (Join-Path $pluginPath "StreamJsonRpc.dll")
    Add-Type -Path (Join-Path $protocolPath "LLMEmpoweredCommandPredictor.Protocol.dll")
    
    # Quick test
    $settings = New-Object LLMEmpoweredCommandPredictor.Protocol.Models.ConnectionSettings
    $settings.TimeoutMs = 1000
    $client = New-Object LLMEmpoweredCommandPredictor.Protocol.Client.SuggestionServiceClient($settings)
    
    $request = New-Object LLMEmpoweredCommandPredictor.Protocol.Models.SuggestionRequest("git", 5)
    $response = $client.GetSuggestionsAsync($request, [System.Threading.CancellationToken]::None).GetAwaiter().GetResult()
    
    if ($response -and $response.Suggestions.Count -gt 0) {
        Write-Host "🎉 SUCCESS! Got $($response.Suggestions.Count) suggestions from Cathy's cache:" -ForegroundColor Green
        foreach ($suggestion in $response.Suggestions) {
            Write-Host "  * $($suggestion.SuggestionText)" -ForegroundColor Cyan
        }
    } else {
        Write-Host "No suggestions received" -ForegroundColor Yellow
    }
    
    $client.Dispose()
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}
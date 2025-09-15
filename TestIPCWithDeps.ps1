# Test with proper assembly loading
Write-Host "Testing IPC connection with dependency loading..." -ForegroundColor Green

try {
    # Load required dependencies first
    $protocolPath = "src\Protocol\bin\Debug\net6.0"
    $pluginPath = "src\PredictorPlugin\bin\Debug\LLMEmpoweredCommandPredictor\net6.0"
    
    Write-Host "Loading dependencies..." -ForegroundColor Yellow
    
    # Load StreamJsonRpc
    $streamJsonRpcPath = Join-Path $pluginPath "StreamJsonRpc.dll"
    if (Test-Path $streamJsonRpcPath) {
        Add-Type -Path $streamJsonRpcPath
        Write-Host "✓ StreamJsonRpc loaded" -ForegroundColor Green
    } else {
        Write-Host "✗ StreamJsonRpc not found at: $streamJsonRpcPath" -ForegroundColor Red
        return
    }
    
    # Load Protocol assembly
    $protocolDllPath = Join-Path $protocolPath "LLMEmpoweredCommandPredictor.Protocol.dll"
    Add-Type -Path $protocolDllPath
    Write-Host "✓ Protocol assembly loaded" -ForegroundColor Green
    
    # Create client
    $settings = New-Object LLMEmpoweredCommandPredictor.Protocol.Models.ConnectionSettings
    $settings.EnableDebugLogging = $true
    $settings.TimeoutMs = 2000  
    $settings.ConnectionTimeoutMs = 3000
    
    $client = New-Object LLMEmpoweredCommandPredictor.Protocol.Client.SuggestionServiceClient($settings)
    Write-Host "✓ Client created successfully" -ForegroundColor Green
    
    # Test request for git commands (should match Cathy's cache)
    $request = New-Object LLMEmpoweredCommandPredictor.Protocol.Models.SuggestionRequest("git", 5)
    
    Write-Host "Testing suggestion request for 'git' (should return Cathy's cached commands)..." -ForegroundColor Yellow
    
    $response = $client.GetSuggestionsAsync($request, [System.Threading.CancellationToken]::None).GetAwaiter().GetResult()
    
    if ($response -and $response.Suggestions -and $response.Suggestions.Count -gt 0) {
        Write-Host "✓ SUCCESS! IPC connection working! Received $($response.Suggestions.Count) suggestions from Cathy's cache:" -ForegroundColor Green
        $index = 1
        foreach ($suggestion in $response.Suggestions) {
            Write-Host "  $index. $($suggestion.SuggestionText)" -ForegroundColor White
            $index++
        }
        Write-Host "`n🎉 The cache is working! These are Cathy's pre-populated git commands." -ForegroundColor Green
    } else {
        Write-Host "✗ IPC connected but no suggestions returned" -ForegroundColor Red
        if ($response) {
            Write-Host "Response received but empty suggestions list" -ForegroundColor Yellow
        }
    }
    
    $client.Dispose()
    
} catch {
    Write-Host "✗ Test failed: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.InnerException) {
        Write-Host "Inner exception: $($_.Exception.InnerException.Message)" -ForegroundColor Red
    }
    Write-Host "Stack trace: $($_.Exception.StackTrace)" -ForegroundColor Gray
}
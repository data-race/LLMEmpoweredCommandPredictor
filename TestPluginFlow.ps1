#!/usr/bin/env pwsh

Write-Host "=== Testing Plugin Flow ===" -ForegroundColor Green

# 1. Import the module
Write-Host "1. Loading Plugin module..." -ForegroundColor Yellow
try {
    Import-Module ./src/PredictorPlugin/bin/Debug/LLMEmpoweredCommandPredictor/net6.0/LLMEmpoweredCommandPredictor.dll -Force
    Write-Host "   ✓ Plugin module loaded successfully" -ForegroundColor Green
}
catch {
    Write-Host "   ✗ Failed to load Plugin module: $_" -ForegroundColor Red
    exit 1
}

# 2. Enable the predictor
Write-Host "2. Enabling predictor..." -ForegroundColor Yellow
try {
    Enable-ExperimentalFeature PSSubsystemPluginModel -WarningAction SilentlyContinue
    Set-PSReadLineOption -PredictionSource HistoryAndPlugin -WarningAction SilentlyContinue
    Write-Host "   ✓ Predictor enabled" -ForegroundColor Green
}
catch {
    Write-Host "   ✗ Failed to enable predictor: $_" -ForegroundColor Red
}

# 3. Test direct Plugin functionality
Write-Host "3. Testing Plugin directly..." -ForegroundColor Yellow
try {
    # Create a test context
    $context = [LLMEmpoweredCommandPredictor.LLMSuggestionContext]::new()
    $context.UserInput = "git"
    
    # Create provider and get suggestions
    $provider = [LLMEmpoweredCommandPredictor.LLMSuggestionProvider]::new()
    $suggestions = $provider.GetSuggestions($context, [System.Threading.CancellationToken]::None)
    
    Write-Host "   Input: 'git'" -ForegroundColor Cyan
    Write-Host "   Suggestions count: $($suggestions.Count)" -ForegroundColor Cyan
    
    if ($suggestions.Count -gt 0) {
        Write-Host "   ✓ Plugin returned suggestions:" -ForegroundColor Green
        foreach ($suggestion in $suggestions | Select-Object -First 3) {
            Write-Host "     - $($suggestion.SuggestionText)" -ForegroundColor White
        }
    }
    else {
        Write-Host "   ✗ Plugin returned no suggestions" -ForegroundColor Red
    }
}
catch {
    Write-Host "   ✗ Plugin test failed: $_" -ForegroundColor Red
    Write-Host "   Exception details: $($_.Exception.GetType().FullName)" -ForegroundColor Red
}

# 4. Test with different inputs
Write-Host "4. Testing different inputs..." -ForegroundColor Yellow
$testInputs = @("git", "docker", "get-", "ls")

foreach ($input in $testInputs) {
    try {
        $context = [LLMEmpoweredCommandPredictor.LLMSuggestionContext]::new()
        $context.UserInput = $input
        
        $provider = [LLMEmpoweredCommandPredictor.LLMSuggestionProvider]::new()
        $suggestions = $provider.GetSuggestions($context, [System.Threading.CancellationToken]::None)
        
        $status = if ($suggestions.Count -gt 0) { "✓" } else { "✗" }
        $color = if ($suggestions.Count -gt 0) { "Green" } else { "Red" }
        
        Write-Host "   Input: '$input' -> $($suggestions.Count) suggestions $status" -ForegroundColor $color
    }
    catch {
        Write-Host "   Input: '$input' -> ERROR: $_" -ForegroundColor Red
    }
}

Write-Host "=== Test Complete ===" -ForegroundColor Green

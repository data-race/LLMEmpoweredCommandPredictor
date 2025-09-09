# PowerShell Protocol + Cache Final Test

Write-Host "=== LLM Empowered Command Predictor Final Test ===" -ForegroundColor Green
Write-Host ""

# Load module
$modulePath = "./src/PredictorPlugin/bin/Release/LLMEmpoweredCommandPredictor/net6.0/LLMEmpoweredCommandPredictor.psd1"

Write-Host "Loading module..." -ForegroundColor Yellow
Import-Module $modulePath -Force

$module = Get-Module LLMEmpoweredCommandPredictor
if ($module) {
    Write-Host "✓ Module loaded successfully!" -ForegroundColor Green
    Write-Host "  Name: $($module.Name)" -ForegroundColor White
    Write-Host "  Version: $($module.Version)" -ForegroundColor White
    Write-Host "  Type: $($module.ModuleType)" -ForegroundColor White
    
    # Configure PSReadLine
    Write-Host ""
    Write-Host "Configuring PSReadLine prediction..." -ForegroundColor Yellow
    Set-PSReadLineOption -PredictionSource HistoryAndPlugin
    
    $predictionSource = Get-PSReadLineOption | Select-Object -ExpandProperty PredictionSource
    Write-Host "✓ Prediction source set to: $predictionSource" -ForegroundColor Green
    
    Write-Host ""
    Write-Host "=== Test Ready! Now you can test Protocol + Cache integration ===" -ForegroundColor Green
    Write-Host ""
    Write-Host "Test methods:" -ForegroundColor Cyan
    Write-Host "1. Enter command prefix in PowerShell, e.g.: Get-Pro" -ForegroundColor White
    Write-Host "2. Wait for gray suggestion text to appear" -ForegroundColor White
    Write-Host "3. Press right arrow key (→) to accept suggestion" -ForegroundColor White
    Write-Host "4. Repeat same command to test cache effect" -ForegroundColor White
    Write-Host ""
    Write-Host "Suggested test commands:" -ForegroundColor Cyan
    Write-Host "  Get-Process" -ForegroundColor White
    Write-Host "  Get-Service" -ForegroundColor White
    Write-Host "  Get-ChildItem" -ForegroundColor White
    Write-Host ""
    Write-Host "Data flow verification:" -ForegroundColor Yellow
    Write-Host "User Input → PredictorPlugin → Protocol IPC → CachedServiceBridge → Suggestion Display" -ForegroundColor White
    Write-Host ""
    Write-Host "Now start entering commands in PowerShell to test!" -ForegroundColor Green
    
}
else {
    Write-Host "❌ Module loading failed" -ForegroundColor Red
}

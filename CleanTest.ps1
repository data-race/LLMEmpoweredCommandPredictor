# Clean Test Environment Script
Write-Host "=== Setting up Clean Test Environment ===" -ForegroundColor Cyan

# 1. Check current PSReadLine settings
Write-Host "1. Current PSReadLine prediction settings:" -ForegroundColor Yellow
Get-PSReadLineOption | Select-Object PredictionSource, PredictionViewStyle | Format-Table

# 2. Disable all predictions first
Write-Host "2. Disabling all predictions..." -ForegroundColor Yellow
Set-PSReadLineOption -PredictionSource None
Write-Host "   ✅ All predictions disabled" -ForegroundColor Green

# 3. Check if your module is loaded
Write-Host "3. Checking for your module..." -ForegroundColor Yellow
$yourModule = Get-Module LLMEmpoweredCommandPredictor -ErrorAction SilentlyContinue
if ($yourModule) {
    Write-Host "   ⚠️  Your module is loaded: $($yourModule.Name)" -ForegroundColor Yellow
    Write-Host "   To unload: Remove-Module LLMEmpoweredCommandPredictor" -ForegroundColor Cyan
}
else {
    Write-Host "   ✅ Your module is not loaded" -ForegroundColor Green
}

# 4. Show all loaded modules that might provide suggestions
Write-Host "4. Checking for other prediction modules..." -ForegroundColor Yellow
$predictionModules = Get-Module | Where-Object { 
    $_.ExportedCmdlets.Keys -match "Prediction" -or 
    $_.ExportedFunctions.Keys -match "Prediction" -or
    $_.Name -match "Predictor" -or
    $_.Name -match "Completion"
}

if ($predictionModules) {
    Write-Host "   Modules that might provide predictions:" -ForegroundColor Yellow
    $predictionModules | ForEach-Object { Write-Host "     - $($_.Name)" -ForegroundColor Cyan }
}
else {
    Write-Host "   ✅ No prediction modules detected" -ForegroundColor Green
}

Write-Host "=== Clean Environment Ready ===" -ForegroundColor Green
Write-Host "Now you can safely test your module without interference!" -ForegroundColor Green
Write-Host "" 
Write-Host "To test your module:" -ForegroundColor Cyan
Write-Host "1. Import-Module './src/PredictorPlugin/bin/Release/LLMEmpoweredCommandPredictor/net6.0/LLMEmpoweredCommandPredictor.psd1'"
Write-Host "2. Set-PSReadLineOption -PredictionSource HistoryAndPlugin"
Write-Host "3. Start typing commands to see YOUR suggestions"

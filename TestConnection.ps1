Write-Host "Testing Protocol connection..."

Import-Module "./src/PredictorPlugin/bin/Release/LLMEmpoweredCommandPredictor/net6.0/LLMEmpoweredCommandPredictor.psd1" -Force

if (Get-Module LLMEmpoweredCommandPredictor) {
    Write-Host "Module loaded successfully" -ForegroundColor Green
    Set-PSReadLineOption -PredictionSource HistoryAndPlugin
    Write-Host "Ready for testing. Type commands to see predictions." -ForegroundColor Green
}
else {
    Write-Host "Module failed to load" -ForegroundColor Red
}

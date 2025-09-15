# Test script to show where cache logs are written
# This script shows you exactly where to find the logs

Write-Host "LLM Command Predictor - Cache Logging Test" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Green

# Show where the log file will be created
$tempPath = [System.IO.Path]::GetTempPath()
$logFile = Join-Path $tempPath "LLMCommandPredictor.log"

Write-Host "Log file location:" -ForegroundColor Yellow
Write-Host "  $logFile" -ForegroundColor Cyan

# Check if log file exists
if (Test-Path $logFile) {
    Write-Host "`nExisting log file found. Contents:" -ForegroundColor Yellow
    Get-Content $logFile -Tail 10 | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
} else {
    Write-Host "`nNo existing log file found." -ForegroundColor Yellow
}

Write-Host "`nTo monitor logs in real-time, run:" -ForegroundColor Yellow
Write-Host "  Get-Content '$logFile' -Wait -Tail 10" -ForegroundColor Cyan

Write-Host "`nTo clear the log file, run:" -ForegroundColor Yellow
Write-Host "  Remove-Item '$logFile' -Force" -ForegroundColor Cyan

Write-Host "`nNow import the module and use PowerShell - logs will appear in the file above!" -ForegroundColor Green

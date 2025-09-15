# Test script to debug PowerShell plugin suggestions
Write-Host "Testing PowerShell Plugin Debug Logging..." -ForegroundColor Green

# Enable verbose logging for the plugin
$env:DOTNET_ENVIRONMENT = "Development"

# Try to get predictions manually
Write-Host "`nTesting 'git' input..." -ForegroundColor Yellow

# Type git and press space to trigger predictions
Write-Host "Type 'git ' (with space) in PowerShell to see if suggestions appear"
Write-Host "Check the service logs for plugin debug messages"

# Show current prediction settings
Write-Host "`nCurrent PowerShell prediction settings:" -ForegroundColor Cyan
Get-PSReadLineOption | Select-Object PredictionSource, PredictionViewStyle

Write-Host "`nIf no suggestions appear, check:"
Write-Host "1. Plugin is loaded: Get-PSReadLineOption"
Write-Host "2. Service is running: Check the dotnet run terminal"
Write-Host "3. Plugin logs: Look for 'PowerShell Plugin:' messages in service output"
Write-Host "4. Try: Set-PSReadLineOption -PredictionSource PluginAndHistory"

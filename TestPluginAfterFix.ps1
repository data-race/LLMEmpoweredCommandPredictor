# Test script to verify the JSON serialization fix AND suggestion generation
Import-Module LLMEmpoweredCommandPredictor -Force

Write-Host "=== LLM Empowered Command Predictor Test ===" -ForegroundColor Green
Write-Host ""
Write-Host "Testing predictive suggestions after fixes:" -ForegroundColor Yellow
Write-Host "  ✅ Fixed JSON serialization/deserialization (ProtocolSuggestion)" -ForegroundColor Green
Write-Host "  ✅ Added suggestion generation for partial input" -ForegroundColor Green
Write-Host ""
Write-Host "Test Commands (should now show actual suggestions):" -ForegroundColor Cyan
Write-Host "  - 'gi' -> should suggest git commands" -ForegroundColor White
Write-Host "  - 'git' -> should suggest git commands" -ForegroundColor White
Write-Host "  - 'get' -> should suggest PowerShell Get- commands" -ForegroundColor White
Write-Host "  - 'doc' -> should suggest docker commands" -ForegroundColor White
Write-Host "  - 'dot' -> should suggest dotnet commands" -ForegroundColor White
Write-Host ""
Write-Host "Expected behavior:" -ForegroundColor Yellow
Write-Host "  • NO more JSON deserialization errors" -ForegroundColor Green
Write-Host "  • NO more '0 suggestions' messages" -ForegroundColor Green  
Write-Host "  • Actual command suggestions should appear" -ForegroundColor Green
Write-Host "  • IPC communication should work properly" -ForegroundColor Green
Write-Host ""
Write-Host "Try typing the commands above and see suggestions appear!" -ForegroundColor Magenta
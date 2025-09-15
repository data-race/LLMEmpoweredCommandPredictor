# Debug test script to verify plugin is working with the service
Import-Module LLMEmpoweredCommandPredictor -Force

Write-Host "=== DEBUGGING PLUGIN CONNECTION ===" -ForegroundColor Cyan
Write-Host ""

# Check if module is loaded
$module = Get-Module LLMEmpoweredCommandPredictor
if ($module) {
    Write-Host "✅ Module loaded successfully" -ForegroundColor Green
    Write-Host "   Version: $($module.Version)" -ForegroundColor White
    Write-Host "   Path: $($module.ModuleBase)" -ForegroundColor White
} else {
    Write-Host "❌ Module not loaded" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "=== TESTING SERVICE CONNECTION ===" -ForegroundColor Cyan

# Check if service is responsive by looking for the predictor
$predictors = Get-PSSubsystem -Kind CommandPredictor
$ourPredictor = $predictors | Where-Object { $_.Name -like "*LLM*" }

if ($ourPredictor) {
    Write-Host "✅ LLM Predictor found in system" -ForegroundColor Green
    Write-Host "   Name: $($ourPredictor.Name)" -ForegroundColor White
    Write-Host "   Description: $($ourPredictor.Description)" -ForegroundColor White
} else {
    Write-Host "❌ LLM Predictor not registered" -ForegroundColor Red
    Write-Host "Available predictors:" -ForegroundColor Yellow
    $predictors | ForEach-Object { Write-Host "  - $($_.Name)" -ForegroundColor White }
}

Write-Host ""
Write-Host "=== NEXT STEPS ===" -ForegroundColor Magenta
Write-Host "1. Try typing 'gi' and pressing TAB or arrow keys" -ForegroundColor White
Write-Host "2. Look for suggestions to appear" -ForegroundColor White
Write-Host "3. If still showing 0 suggestions, the service may not be responding" -ForegroundColor White
Write-Host ""
Write-Host "Service should be running on background job. Check with: Get-Job" -ForegroundColor Yellow
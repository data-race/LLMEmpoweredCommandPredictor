# Test IPC connectivity from PowerShell Plugin to PredictorService
Write-Host "Testing LLM Empowered Command Predictor Plugin IPC connection..." -ForegroundColor Green

# Import the plugin module
$pluginPath = "src\PredictorPlugin\bin\Debug\LLMEmpoweredCommandPredictor\net6.0\LLMEmpoweredCommandPredictor.dll"
$fullPluginPath = Join-Path $PWD $pluginPath

Write-Host "Plugin Path: $fullPluginPath" -ForegroundColor Yellow

if (Test-Path $fullPluginPath) {
    Write-Host "✓ Plugin DLL found" -ForegroundColor Green
    
    try {
        # Import the plugin module
        Import-Module $fullPluginPath -Force
        Write-Host "✓ Plugin module imported successfully" -ForegroundColor Green
        
        # Test if we can create a predictor instance
        Write-Host "Testing predictor functionality with 'git' command..." -ForegroundColor Yellow
        
        # This should trigger the plugin and show if IPC is working
        Write-Host "Type 'git ' and press TAB to test suggestions..." -ForegroundColor Cyan
        
        # List loaded predictors
        $predictors = [System.Management.Automation.Subsystem.SubsystemManager]::GetSubsystem([System.Management.Automation.Subsystem.SubsystemKind]::CommandPredictor)
        if ($predictors) {
            Write-Host "✓ Command predictors available:" -ForegroundColor Green
            foreach ($predictor in $predictors) {
                Write-Host "  - $($predictor.Name) (Id: $($predictor.Id))" -ForegroundColor White
            }
        } else {
            Write-Host "✗ No command predictors found" -ForegroundColor Red
        }
        
    } catch {
        Write-Host "✗ Error importing plugin: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "Full error: $_" -ForegroundColor Red
    }
    
} else {
    Write-Host "✗ Plugin DLL not found at: $fullPluginPath" -ForegroundColor Red
    Write-Host "Please build the plugin first: dotnet build src/PredictorPlugin" -ForegroundColor Yellow
}

Write-Host "`nTesting complete. If PredictorService is running, you should see suggestions when typing 'git ' commands." -ForegroundColor Green
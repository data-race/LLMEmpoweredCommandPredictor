# Test named pipe availability
Write-Host "Testing named pipe availability..." -ForegroundColor Green

$pipeName = "LLMEmpoweredCommandPredictor.SuggestionService"

try {
    Write-Host "Attempting to connect to pipe: $pipeName" -ForegroundColor Yellow
    
    # Create a simple pipe client to test connectivity
    $pipe = New-Object System.IO.Pipes.NamedPipeClientStream(".", $pipeName, [System.IO.Pipes.PipeDirection]::InOut)
    
    # Try to connect with timeout
    $timeout = 2000
    $pipe.Connect($timeout)
    
    if ($pipe.IsConnected) {
        Write-Host "✓ SUCCESS! Named pipe is available and connected" -ForegroundColor Green
        Write-Host "✓ PredictorService is listening on the correct pipe" -ForegroundColor Green
        $pipe.Close()
    } else {
        Write-Host "✗ Failed to connect to named pipe" -ForegroundColor Red
    }
    
    $pipe.Dispose()
    
} catch [System.TimeoutException] {
    Write-Host "✗ Connection timeout - PredictorService may not be running or listening" -ForegroundColor Red
    Write-Host "Make sure PredictorService is running: dotnet run (in src/PredictorService)" -ForegroundColor Yellow
} catch [System.IO.IOException] {
    Write-Host "✗ IO Exception: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "This might indicate a permission issue or the pipe is not available" -ForegroundColor Yellow
} catch {
    Write-Host "✗ Unexpected error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Error type: $($_.Exception.GetType().Name)" -ForegroundColor Yellow
}
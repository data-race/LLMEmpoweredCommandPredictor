# Development Environment Setup Script
# This script sets up the required environment variables for Azure OpenAI integration

Write-Host "🚀 Setting up LLM Empowered Command Predictor development environment..." -ForegroundColor Green
Write-Host ""

# Azure OpenAI Configuration
Write-Host "📝 Configuring Azure OpenAI settings..." -ForegroundColor Yellow

$env:AZURE_OPENAI_ENDPOINT = "https://yongyu-chatgpt-test1.openai.azure.com/"
$env:AZURE_OPENAI_DEPLOYMENT = "gpt-4.1"
$env:AZURE_OPENAI_KEY = "c961a6ea9de24724b09ed800a204f59c"

Write-Host "✅ Environment variables configured:" -ForegroundColor Green
Write-Host "   AZURE_OPENAI_ENDPOINT = $env:AZURE_OPENAI_ENDPOINT" -ForegroundColor Cyan
Write-Host "   AZURE_OPENAI_DEPLOYMENT = $env:AZURE_OPENAI_DEPLOYMENT" -ForegroundColor Cyan
Write-Host "   AZURE_OPENAI_KEY = [CONFIGURED]" -ForegroundColor Cyan
Write-Host ""

Write-Host "🎯 Ready to run! Use these commands:" -ForegroundColor Green
Write-Host "   cd src/PredictorService" -ForegroundColor White
Write-Host "   dotnet run" -ForegroundColor White
Write-Host ""
Write-Host "   Or to run the PowerShell plugin:" -ForegroundColor White
Write-Host "   cd src/PredictorPlugin" -ForegroundColor White
Write-Host "   dotnet build" -ForegroundColor White
Write-Host ""

Write-Host "⚠️  Note: These environment variables are set for the current PowerShell session only." -ForegroundColor Yellow
Write-Host "   Run this script again if you start a new PowerShell session." -ForegroundColor Yellow
Write-Host ""
# Quick Development Setup

## 🚀 Getting Started

### 1. Run the Setup Script
```powershell
# From the project root directory
.\setup-dev.ps1
```

### 2. Start the Predictor Service
```powershell
# Navigate to PredictorService directory and start as background job
cd src/PredictorService
Start-Job -ScriptBlock { dotnet run -c Release } -Name "PredictorService"
```

### 3. Build the PowerShell Plugin
```powershell
cd src/PredictorPlugin
dotnet build
```

### 4. Test the PowerShell Plugin (Optional)
To test the command predictor in your current PowerShell session:

```powershell
# First, disable any existing prediction
Set-PSReadLineOption -PredictionSource None

# Import the built module (use Release build for better performance)
cd src/PredictorPlugin
Import-Module ".\bin\Release\LLMEmpoweredCommandPredictor\net6.0\LLMEmpoweredCommandPredictor.dll" -Verbose

# Enable plugin-based predictions with list view
Set-PSReadLineOption -PredictionSource HistoryAndPlugin -PredictionViewStyle ListView
```

Now you can test by typing commands like `git`, `Get-`, `docker`, etc. and see suggestions appear!

## 🔧 Manual Setup (Alternative)

If you prefer to set environment variables manually:

```powershell
$env:AZURE_OPENAI_ENDPOINT = "https://yongyu-chatgpt-test1.openai.azure.com/"
$env:AZURE_OPENAI_DEPLOYMENT = "gpt-4.1"
$env:AZURE_OPENAI_KEY = "your-api-key-here"
```

## 📋 Environment Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `AZURE_OPENAI_ENDPOINT` | Azure OpenAI service endpoint | `https://your-service.openai.azure.com/` |
| `AZURE_OPENAI_DEPLOYMENT` | Model deployment name | `gpt-4.1` |
| `AZURE_OPENAI_KEY` | Azure OpenAI API key | `your-32-character-key` |

## ⚠️ Important Notes

- Environment variables are set for the current PowerShell session only
- Run `setup-dev.ps1` again if you start a new PowerShell session
- Never commit API keys to source control
- For production deployment, use proper secret management

## 🔄 Updating API Key

To use a different API key, either:
1. Edit `setup-dev.ps1` and run it again
2. Set the variable manually: `$env:AZURE_OPENAI_KEY = "new-key"`
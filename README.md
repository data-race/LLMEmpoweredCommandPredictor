## How to test locally

### Prerequisites
You need to have the following prerequisites installed:

- .NET SDK (version 6.0 or later)
- PowerShell 7 or later
- PSReadline module

Enable Powershell experimental feature:
``` powershell
Enable-ExperimentalFeature PSSubsystemPluginModel
```

### Launch background service

``` powershell
# 1. set llm endpoint, model and key in env vars
$env:AZURE_OPENAI_ENDPOINT="https://zihan-openai.openai.azure.com/" 
$env:AZURE_OPENAI_KEY="<replace-it>" 
$env:AZURE_OPENAI_DEPLOYMENT="zihan-gpt4o-deployment"

# 2. run the service
# make sure you are under path: $PROJ\src\PredictorService
dotnet build
dotnet run
```

### Launch the plugin

``` powershell
# 1. start session history collector. Make sure you are under path: $PROJ\src\PredictorService
.\SessionHistoryCollection.ps1

# 2. build plugin and import
cd .\src\PredictorPlugin\
dotnet build -c Release
Import-Module ".\bin\Release\LLMEmpoweredCommandPredictor\net6.0\LLMEmpoweredCommandPredictor.dll" -Verbose
Set-PSReadLineOption -PredictionSource HistoryAndPlugin 
```
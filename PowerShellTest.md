# PowerShell Protocol + Cache Real Test

## Prerequisites

1. PowerShell 7 or higher
2. .NET SDK 6.0 or higher
3. PSReadline module

## Test Steps

### Step 1: Enable PowerShell Experimental Feature

```powershell
Enable-ExperimentalFeature PSSubsystemPluginModel
```

### Step 2: Build Protocol Project

```powershell
dotnet build src/Protocol/ -c Release
```

### Step 3: Build PredictorPlugin Project

```powershell
dotnet build src/PredictorPlugin/ -c Release
```

### Step 4: Load Module in New PowerShell Terminal

```powershell
# Import Protocol module (if needed)
Import-Module ".\src\Protocol\bin\Release\net6.0\LLMEmpoweredCommandPredictor.Protocol.dll" -Verbose

# Import PredictorPlugin module
Import-Module ".\src\PredictorPlugin\bin\Release\net6.0\LLMEmpoweredCommandPredictor.dll" -Verbose

# Enable plugin prediction
Set-PSReadLineOption -PredictionSource HistoryAndPlugin
```

### Step 5: Test Command Suggestions

Now you can start entering commands, PowerShell will display real-time suggestions:

```powershell
# Try these commands
Get-Process
Get-Service
ls
docker
git
```

## Verify Protocol + Cache Integration

Your input commands go through the following flow:

1. **User Input** → PowerShell receives
2. **PredictorPlugin** → Calls Protocol's PluginHelper
3. **Protocol IPC** → Client/Server communication
4. **Cache Check** → CachedServiceBridge searches cache
5. **Suggestion Return** → Display in PowerShell

## Expected Effects

- First command input: Slower (generate new suggestions)
- Repeated command input: Fast (cache hit)
- Suggestions display in gray text below command line
- Press → key to accept suggestions

## Troubleshooting

If you don't see suggestions:

1. Confirm experimental feature is enabled:
   ```powershell
   Get-ExperimentalFeature PSSubsystemPluginModel
   ```

2. Check if modules are loaded correctly:
   ```powershell
   Get-Module LLMEmpoweredCommandPredictor*
   ```

3. Verify PSReadLine settings:
   ```powershell
   Get-PSReadLineOption | Select-Object PredictionSource
   ```
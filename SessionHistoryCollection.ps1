# Define the file paths
$appDataPath = [Environment]::GetFolderPath([Environment+SpecialFolder]::ApplicationData)
$llmPredictorPath = Join-Path $appDataPath "LLMCommandPredictor"

# Ensure the LLMCommandPredictor directory exists
if (-not (Test-Path $llmPredictorPath)) {
    New-Item -Path $llmPredictorPath -ItemType Directory -Force | Out-Null
}

$sessionHistoryFilePath = Join-Path $llmPredictorPath "PowerShellSessionHistory.txt"
$healthLogFilePath = Join-Path $llmPredictorPath "PowerShellSessionHistory_Health.log"
$historySignFilePath = Join-Path $llmPredictorPath "Get-History-Sign.txt"

# Ensure the files exist and clear them at the start of the session.
if (Test-Path -Path $sessionHistoryFilePath) {
    Clear-Content -Path $sessionHistoryFilePath
} else {
    # Create the file if it doesn't exist
    New-Item -Path $sessionHistoryFilePath -ItemType File -Force | Out-Null
}

if (Test-Path -Path $healthLogFilePath) {
    Clear-Content -Path $healthLogFilePath
} else {
    # Create the health log file if it doesn't exist
    New-Item -Path $healthLogFilePath -ItemType File -Force | Out-Null
}

# Log script start
$startTime = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
"$startTime - Script started (simplified version)" | Out-File -FilePath $healthLogFilePath -Append -Encoding UTF8

# Function to trim health log to keep only last 200 lines
function Trim-HealthLog {
    param([string]$logPath)
    if (Test-Path $logPath) {
        $lines = Get-Content $logPath
        if ($lines.Count -gt 200) {
            $lines | Select-Object -Last 200 | Out-File $logPath -Encoding UTF8
        }
    }
}

# Create and start a timer to periodically save the last 50 history entries
$timer = New-Object System.Timers.Timer
$timer.Interval = 3000  # Check every 3 seconds
$timer.AutoReset = $true

# Log timer creation
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
"$timestamp - Timer created with interval: $($timer.Interval)ms" | Out-File -FilePath $healthLogFilePath -Append -Encoding UTF8

# Track timer runs
$script:timerRunCount = 0

# Register the timer event - simple approach: just overwrite with last 50 entries
$timerAction = Register-ObjectEvent -InputObject $timer -EventName Elapsed -Action {
    # Function to calculate hash of Get-History output (defined inside the action for scope access)
    function Get-HistoryHash {
        param($historyEntries)
        if (-not $historyEntries -or $historyEntries.Count -eq 0) {
            return ""
        }
        
        # Create a string representation of the history for hashing
        $historyString = ""
        foreach ($entry in $historyEntries) {
            if ($entry.CommandLine -ne $null -and $entry.CommandLine -ne "") {
                $historyString += $entry.CommandLine + "|"
            } elseif ($entry -ne $null) {
                $historyString += $entry.ToString() + "|"
            }
        }
        
        # Calculate SHA256 hash
        $stringAsStream = [System.IO.MemoryStream]::new()
        $writer = [System.IO.StreamWriter]::new($stringAsStream)
        $writer.write($historyString)
        $writer.Flush()
        $stringAsStream.Position = 0
        $hash = Get-FileHash -InputStream $stringAsStream -Algorithm SHA256
        $writer.Dispose()
        $stringAsStream.Dispose()
        
        return $hash.Hash
    }
    try {
        # Increment timer run count
        $script:timerRunCount++
        $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        
        # Get file paths (need to redefine in this scope)
        $appDataPath = [Environment]::GetFolderPath([Environment+SpecialFolder]::ApplicationData)
        $llmPredictorPath = Join-Path $appDataPath "LLMCommandPredictor"
        $sessionHistoryFilePath = Join-Path $llmPredictorPath "PowerShellSessionHistory.txt"
        $healthLogFilePath = Join-Path $llmPredictorPath "PowerShellSessionHistory_Health.log"
        $historySignFilePath = Join-Path $llmPredictorPath "Get-History-Sign.txt"
        
        # Log timer activity
        "$timestamp - Timer run #$($script:timerRunCount)" | Out-File -FilePath $healthLogFilePath -Append -Encoding UTF8
        
        # Trim health log every 10 timer runs to keep it manageable
        if ($script:timerRunCount % 10 -eq 0) {
            Trim-HealthLog -logPath $healthLogFilePath
        }
        
        try {
            # Get the last 50 history entries (or all if less than 50)
            $history = Get-History
            "$timestamp - Get-History returned $($history.Count) total entries" | Out-File -FilePath $healthLogFilePath -Append -Encoding UTF8
            
            if ($history -and $history.Count -gt 0) {
                $last50 = $history | Select-Object -Last 50
                "$timestamp - Selected last $($last50.Count) entries" | Out-File -FilePath $healthLogFilePath -Append -Encoding UTF8
                
                # Calculate hash of current history
                $currentHash = Get-HistoryHash -historyEntries $last50
                "$timestamp - Current history hash: $currentHash" | Out-File -FilePath $healthLogFilePath -Append -Encoding UTF8
                
                # Read previous hash if it exists
                $previousHash = ""
                if (Test-Path $historySignFilePath) {
                    $previousHash = Get-Content $historySignFilePath -ErrorAction SilentlyContinue
                }
                "$timestamp - Previous history hash: $previousHash" | Out-File -FilePath $healthLogFilePath -Append -Encoding UTF8
                
                # Only update files if hash has changed (i.e., there are new commands)
                if ($currentHash -ne $previousHash) {
                    "$timestamp - History has changed, updating files" | Out-File -FilePath $healthLogFilePath -Append -Encoding UTF8
                    
                    # Clear the file and write all entries
                    $historyContent = @()
                    foreach ($entry in $last50) {
                        try {
                            if ($entry -and $entry.StartTime) {
                                $timestamp_entry = $entry.StartTime.ToString("yyyy-MM-dd HH:mm:ss")
                            } else {
                                $timestamp_entry = $timestamp
                            }
                            
                            if ($entry.CommandLine -ne $null -and $entry.CommandLine -ne "") {
                                $command = $entry.CommandLine
                            } elseif ($entry -ne $null) {
                                $command = $entry.ToString()
                            } else {
                                $command = "[Unknown Command]"
                            }
                            
                            $historyContent += "$timestamp_entry [$command]"
                        } catch {
                            "$timestamp - ERROR processing individual entry: $($_.Exception.Message)" | Out-File -FilePath $healthLogFilePath -Append -Encoding UTF8
                        }
                    }
                    
                    if ($historyContent.Count -gt 0) {
                        # Update the main history file
                        $historyContent | Out-File -FilePath $sessionHistoryFilePath -Encoding UTF8
                        "$timestamp - Successfully wrote $($historyContent.Count) entries to history file" | Out-File -FilePath $healthLogFilePath -Append -Encoding UTF8
                        
                        # Update the hash signature file
                        $currentHash | Out-File -FilePath $historySignFilePath -Encoding UTF8
                        "$timestamp - Updated hash signature file with new commands" | Out-File -FilePath $healthLogFilePath -Append -Encoding UTF8
                    } else {
                        "$timestamp - No valid history content to write" | Out-File -FilePath $healthLogFilePath -Append -Encoding UTF8
                    }
                } else {
                    "$timestamp - No changes in history, skipping file updates" | Out-File -FilePath $healthLogFilePath -Append -Encoding UTF8
                }
            } else {
                "$timestamp - No history entries found or history is null" | Out-File -FilePath $healthLogFilePath -Append -Encoding UTF8
            }
        } catch {
            "$timestamp - ERROR in history processing: $($_.Exception.Message)" | Out-File -FilePath $healthLogFilePath -Append -Encoding UTF8
        }
    } catch {
        $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        $healthLogFilePath = "$env:USERPROFILE\Documents\PowerShellSessionHistory_Health.log"
        "$timestamp - ERROR in timer (outer): $($_.Exception.Message)" | Out-File -FilePath $healthLogFilePath -Append -Encoding UTF8
        "$timestamp - ERROR details: $($_.Exception.GetType().FullName)" | Out-File -FilePath $healthLogFilePath -Append -Encoding UTF8
        "$timestamp - ERROR stack trace: $($_.ScriptStackTrace)" | Out-File -FilePath $healthLogFilePath -Append -Encoding UTF8
    }
}

$timer.Start()

# Log timer start
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
"$timestamp - Timer started successfully" | Out-File -FilePath $healthLogFilePath -Append -Encoding UTF8

# Store timer and job references for cleanup
$Global:HistoryCollectionTimer = $timer
$Global:HistoryCollectionJob = $timerAction

# Ensure cleanup when the session ends
$endSessionHandler = {
    # Function to calculate hash of Get-History output (defined inside the handler for scope access)
    function Get-HistoryHash {
        param($historyEntries)
        if (-not $historyEntries -or $historyEntries.Count -eq 0) {
            return ""
        }
        
        # Create a string representation of the history for hashing
        $historyString = ""
        foreach ($entry in $historyEntries) {
            if ($entry.CommandLine -ne $null -and $entry.CommandLine -ne "") {
                $historyString += $entry.CommandLine + "|"
            } elseif ($entry -ne $null) {
                $historyString += $entry.ToString() + "|"
            }
        }
        
        # Calculate SHA256 hash
        $stringAsStream = [System.IO.MemoryStream]::new()
        $writer = [System.IO.StreamWriter]::new($stringAsStream)
        $writer.write($historyString)
        $writer.Flush()
        $stringAsStream.Position = 0
        $hash = Get-FileHash -InputStream $stringAsStream -Algorithm SHA256
        $writer.Dispose()
        $stringAsStream.Dispose()
        
        return $hash.Hash
    }
    
    try {
        $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        $healthLogFilePath = "$env:USERPROFILE\Documents\PowerShellSessionHistory_Health.log"
        $sessionHistoryFilePath = "$env:USERPROFILE\Documents\PowerShellSessionHistory.txt"
        $historySignFilePath = "$env:USERPROFILE\Documents\Get-History-Sign.txt"
        
        "$timestamp - Session ending, starting cleanup" | Out-File -FilePath $healthLogFilePath -Append -Encoding UTF8
        
        # Save final history before cleanup
        $history = Get-History | Select-Object -Last 50
        if ($history.Count -gt 0) {
            $historyContent = @()
            foreach ($entry in $history) {
                $timestamp_entry = $entry.StartTime.ToString("yyyy-MM-dd HH:mm:ss")
                $command = if ($entry.CommandLine -ne $null) { $entry.CommandLine } else { $entry.ToString() }
                $historyContent += "$timestamp_entry [$command]"
            }
            $historyContent += "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') [# Session ended]"
            $historyContent | Out-File -FilePath $sessionHistoryFilePath -Encoding UTF8
            "$timestamp - Final save completed with $($history.Count) entries" | Out-File -FilePath $healthLogFilePath -Append -Encoding UTF8
            
            # Update hash signature with final state
            $finalHash = Get-HistoryHash -historyEntries $history
            $finalHash | Out-File -FilePath $historySignFilePath -Encoding UTF8
            "$timestamp - Updated final hash signature" | Out-File -FilePath $healthLogFilePath -Append -Encoding UTF8
        }
        
        # Cleanup resources
        if ($Global:HistoryCollectionTimer) {
            $Global:HistoryCollectionTimer.Stop()
            $Global:HistoryCollectionTimer.Dispose()
            "$timestamp - Timer stopped and disposed" | Out-File -FilePath $healthLogFilePath -Append -Encoding UTF8
        }
        
        if ($Global:HistoryCollectionJob) {
            Unregister-Event -SourceIdentifier $Global:HistoryCollectionJob.Name -ErrorAction SilentlyContinue
            "$timestamp - Timer event unregistered" | Out-File -FilePath $healthLogFilePath -Append -Encoding UTF8
        }
        
        "$timestamp - Session cleanup completed" | Out-File -FilePath $healthLogFilePath -Append -Encoding UTF8
    } catch {
        $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        $healthLogFilePath = "$env:USERPROFILE\Documents\PowerShellSessionHistory_Health.log"
        "$timestamp - ERROR during cleanup: $($_.Exception.Message)" | Out-File -FilePath $healthLogFilePath -Append -Encoding UTF8
    }
}

Register-EngineEvent PowerShell.Exiting -Action $endSessionHandler

# Inform user the listener is active
Write-Host "Session history listener is active. Commands will be saved when new commands are detected using hash comparison."
Write-Host "History file: $sessionHistoryFilePath"
Write-Host "Hash signature file: $historySignFilePath"
Write-Host "Health log: $healthLogFilePath"

# Log final setup completion
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
"$timestamp - Setup completed, script ready" | Out-File -FilePath $healthLogFilePath -Append -Encoding UTF8

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using LLMEmpoweredCommandPredictor.Protocol.Models;

namespace LLMEmpoweredCommandPredictor.PredictorService.Services;

/// <summary>
/// Service for validating PowerShell commands and providing correctness feedback.
/// Provides syntax checking, parameter validation, and safety warnings.
/// </summary>
public class CommandValidationService
{
    private readonly ILogger<CommandValidationService> _logger;
    
    // Common PowerShell commands and their basic syntax patterns
    private static readonly Dictionary<string, string[]> PowerShellCommands = new()
    {
        { "Get-Process", new[] { "-Name", "-Id", "-ComputerName", "-Module" } },
        { "Get-Service", new[] { "-Name", "-DisplayName", "-Status", "-ComputerName" } },
        { "Get-ChildItem", new[] { "-Path", "-Filter", "-Recurse", "-Force", "-Hidden" } },
        { "Set-Location", new[] { "-Path", "-LiteralPath", "-PassThru" } },
        { "Copy-Item", new[] { "-Path", "-Destination", "-Recurse", "-Force", "-Filter" } },
        { "Remove-Item", new[] { "-Path", "-Recurse", "-Force", "-Confirm" } },
        { "New-Item", new[] { "-Path", "-ItemType", "-Name", "-Value", "-Force" } },
        { "Test-Path", new[] { "-Path", "-PathType", "-IsValid" } },
        { "Write-Host", new[] { "-Object", "-ForegroundColor", "-BackgroundColor", "-NoNewline" } },
        { "Write-Output", new[] { "-InputObject", "-NoEnumerate" } }
    };
    
    // Common external tools and their parameters
    private static readonly Dictionary<string, string[]> ExternalTools = new()
    {
        { "git", new[] { "status", "add", "commit", "push", "pull", "clone", "branch", "checkout", "merge", "log", "diff", "reset", "remote" } },
        { "docker", new[] { "run", "build", "ps", "images", "stop", "start", "restart", "rm", "rmi", "pull", "push", "exec", "logs" } },
        { "dotnet", new[] { "build", "run", "test", "publish", "restore", "add", "remove", "new", "clean", "pack" } },
        { "npm", new[] { "install", "uninstall", "update", "run", "start", "test", "build", "init", "publish", "audit" } },
        { "code", new[] { ".", "-r", "-n", "-g", "--help", "--version", "--list-extensions" } }
    };
    
    // Dangerous commands that should trigger warnings
    private static readonly HashSet<string> DangerousCommands = new()
    {
        "Remove-Item", "rm", "del", "rmdir", "rd",
        "Format-Volume", "format",
        "Stop-Process", "kill", "taskkill",
        "Disable-Service", "Stop-Service",
        "Remove-Module", "Uninstall-Module",
        "Clear-Host", "cls", "clear"
    };

    public CommandValidationService(ILogger<CommandValidationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validates a command and returns validation results.
    /// </summary>
    /// <param name="commandLine">The command to validate</param>
    /// <returns>Validation result with feedback</returns>
    public CommandValidationResponse ValidateCommand(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return new CommandValidationResponse
            {
                IsValid = false,
                ValidationLevel = ValidationLevel.Error,
                Message = "Command cannot be empty"
            };
        }

        try
        {
            var result = new CommandValidationResponse { IsValid = true };
            var parts = ParseCommandLine(commandLine);
            
            if (parts.Length == 0)
            {
                return new CommandValidationResponse
                {
                    IsValid = false,
                    ValidationLevel = ValidationLevel.Error,
                    Message = "Invalid command format"
                };
            }

            var command = parts[0];
            var args = parts.Skip(1).ToArray();

            // 1. Check basic syntax
            ValidateBasicSyntax(commandLine, result);
            
            // 2. Check command existence and availability
            ValidateCommandExistence(command, result);
            
            // 3. Check parameters for known commands
            ValidateParameters(command, args, result);
            
            // 4. Check for dangerous operations
            ValidateSafety(command, args, result);
            
            // 5. Check file/path references
            ValidatePathReferences(args, result);

            _logger.LogDebug("Command validation completed for '{Command}': {IsValid} - {Message}", 
                commandLine, result.IsValid, result.Message);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating command: '{Command}'", commandLine);
            return new CommandValidationResponse
            {
                IsValid = false,
                ValidationLevel = ValidationLevel.Error,
                Message = $"Validation error: {ex.Message}"
            };
        }
    }

    private void ValidateBasicSyntax(string commandLine, CommandValidationResponse result)
    {
        // Check for unmatched quotes
        var singleQuotes = commandLine.Count(c => c == '\'');
        var doubleQuotes = commandLine.Count(c => c == '"');
        
        if (singleQuotes % 2 != 0)
        {
            result.Messages.Add(new ValidationMessage(ValidationLevel.Warning, "Unmatched single quotes detected"));
            if (result.ValidationLevel < ValidationLevel.Warning) result.ValidationLevel = ValidationLevel.Warning;
        }
        
        if (doubleQuotes % 2 != 0)
        {
            result.Messages.Add(new ValidationMessage(ValidationLevel.Warning, "Unmatched double quotes detected"));
            if (result.ValidationLevel < ValidationLevel.Warning) result.ValidationLevel = ValidationLevel.Warning;
        }

        // Check for suspicious patterns
        if (commandLine.Contains(";;"))
        {
            result.Messages.Add(new ValidationMessage(ValidationLevel.Warning, "Double semicolon detected - may cause syntax error"));
            if (result.ValidationLevel < ValidationLevel.Warning) result.ValidationLevel = ValidationLevel.Warning;
        }

        if (Regex.IsMatch(commandLine, @"\$\(\s*\)"))
        {
            result.Messages.Add(new ValidationMessage(ValidationLevel.Warning, "Empty subexpression $() detected"));
            if (result.ValidationLevel < ValidationLevel.Warning) result.ValidationLevel = ValidationLevel.Warning;
        }
    }

    private void ValidateCommandExistence(string command, CommandValidationResponse result)
    {
        // Check if it's a known PowerShell command
        if (PowerShellCommands.ContainsKey(command))
        {
            result.Messages.Add(new ValidationMessage(ValidationLevel.Info, $"✓ PowerShell command '{command}' recognized"));
            return;
        }

        // Check if it's a known external tool
        var lowerCommand = command.ToLowerInvariant();
        if (ExternalTools.ContainsKey(lowerCommand))
        {
            result.Messages.Add(new ValidationMessage(ValidationLevel.Info, $"✓ External tool '{command}' recognized"));
            return;
        }

        // Check common command variants
        if (command.StartsWith("Get-") || command.StartsWith("Set-") || 
            command.StartsWith("New-") || command.StartsWith("Remove-"))
        {
            result.Messages.Add(new ValidationMessage(ValidationLevel.Info, $"✓ PowerShell verb-noun pattern detected: '{command}'"));
            return;
        }

        // Unknown command
        result.Messages.Add(new ValidationMessage(ValidationLevel.Warning, $"⚠ Command '{command}' not recognized - verify it's installed and available"));
        if (result.ValidationLevel < ValidationLevel.Warning) result.ValidationLevel = ValidationLevel.Warning;
    }

    private void ValidateParameters(string command, string[] args, CommandValidationResponse result)
    {
        // Validate PowerShell commands
        if (PowerShellCommands.TryGetValue(command, out var validParams))
        {
            ValidatePowerShellParameters(command, args, validParams, result);
            return;
        }

        // Validate external tools
        var lowerCommand = command.ToLowerInvariant();
        if (ExternalTools.TryGetValue(lowerCommand, out var validSubcommands))
        {
            ValidateExternalToolParameters(command, args, validSubcommands, result);
        }
    }

    private void ValidatePowerShellParameters(string command, string[] args, string[] validParams, CommandValidationResponse result)
    {
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            
            // Check for parameter format
            if (arg.StartsWith("-"))
            {
                var paramName = arg.Split(':')[0]; // Handle -Parameter:Value format
                
                if (!validParams.Any(p => p.Equals(paramName, StringComparison.OrdinalIgnoreCase)))
                {
                    result.Messages.Add(new ValidationMessage(ValidationLevel.Warning, $"Parameter '{paramName}' not recognized for '{command}'"));
                    if (result.ValidationLevel < ValidationLevel.Warning) result.ValidationLevel = ValidationLevel.Warning;
                }
                else
                {
                    result.Messages.Add(new ValidationMessage(ValidationLevel.Info, $"✓ Parameter '{paramName}' is valid for '{command}'"));
                }
            }
        }
    }

    private void ValidateExternalToolParameters(string command, string[] args, string[] validSubcommands, CommandValidationResponse result)
    {
        if (args.Length == 0)
        {
            result.Messages.Add(new ValidationMessage(ValidationLevel.Info, $"ℹ {command} command without subcommand - may need additional arguments"));
            return;
        }

        var subcommand = args[0];
        if (validSubcommands.Contains(subcommand, StringComparer.OrdinalIgnoreCase))
        {
            result.Messages.Add(new ValidationMessage(ValidationLevel.Info, $"✓ Subcommand '{subcommand}' is valid for '{command}'"));
        }
        else
        {
            result.Messages.Add(new ValidationMessage(ValidationLevel.Warning, $"Subcommand '{subcommand}' not recognized for '{command}'"));
            if (result.ValidationLevel < ValidationLevel.Warning) result.ValidationLevel = ValidationLevel.Warning;
        }
    }

    private void ValidateSafety(string command, string[] args, CommandValidationResponse result)
    {
        if (DangerousCommands.Contains(command, StringComparer.OrdinalIgnoreCase))
        {
            result.Messages.Add(new ValidationMessage(ValidationLevel.Warning, $"⚠ CAUTION: '{command}' can modify/delete data"));
            if (result.ValidationLevel < ValidationLevel.Warning) result.ValidationLevel = ValidationLevel.Warning;
            
            // Specific warnings for dangerous operations
            if (command.Equals("Remove-Item", StringComparison.OrdinalIgnoreCase) && 
                args.Any(a => a.Equals("-Recurse", StringComparison.OrdinalIgnoreCase)))
            {
                result.Messages.Add(new ValidationMessage(ValidationLevel.Warning, "⚠ DANGER: Recursive deletion - will delete folders and all contents!"));
            }
            
            if (args.Any(a => a.Equals("-Force", StringComparison.OrdinalIgnoreCase)))
            {
                result.Messages.Add(new ValidationMessage(ValidationLevel.Warning, "⚠ DANGER: -Force parameter bypasses confirmations!"));
            }
        }

        // Check for pipe to dangerous commands
        if (command.Contains("|") && args.Any(a => DangerousCommands.Contains(a, StringComparer.OrdinalIgnoreCase)))
        {
            result.Messages.Add(new ValidationMessage(ValidationLevel.Warning, "⚠ Pipeline contains potentially dangerous operations"));
            if (result.ValidationLevel < ValidationLevel.Warning) result.ValidationLevel = ValidationLevel.Warning;
        }
    }

    private void ValidatePathReferences(string[] args, CommandValidationResponse result)
    {
        foreach (var arg in args)
        {
            // Skip parameters
            if (arg.StartsWith("-"))
                continue;

            // Check if it looks like a path
            if (arg.Contains('\\') || arg.Contains('/') || arg.Contains(':'))
            {
                try
                {
                    // Basic path validation
                    if (Path.IsPathRooted(arg))
                    {
                        if (Directory.Exists(arg))
                        {
                            result.Messages.Add(new ValidationMessage(ValidationLevel.Info, $"✓ Directory path exists: '{arg}'"));
                        }
                        else if (File.Exists(arg))
                        {
                            result.Messages.Add(new ValidationMessage(ValidationLevel.Info, $"✓ File path exists: '{arg}'"));
                        }
                        else
                        {
                            result.Messages.Add(new ValidationMessage(ValidationLevel.Warning, $"⚠ Path does not exist: '{arg}'"));
                            if (result.ValidationLevel < ValidationLevel.Warning) result.ValidationLevel = ValidationLevel.Warning;
                        }
                    }
                    else
                    {
                        result.Messages.Add(new ValidationMessage(ValidationLevel.Info, $"ℹ Relative path reference: '{arg}'"));
                    }
                }
                catch
                {
                    result.Messages.Add(new ValidationMessage(ValidationLevel.Warning, $"⚠ Invalid path format: '{arg}'"));
                    if (result.ValidationLevel < ValidationLevel.Warning) result.ValidationLevel = ValidationLevel.Warning;
                }
            }
        }
    }

    private static string[] ParseCommandLine(string commandLine)
    {
        // Simple command line parsing - split by spaces, respecting quotes
        var parts = new List<string>();
        var currentPart = "";
        var inQuotes = false;
        var quoteChar = '"';

        for (int i = 0; i < commandLine.Length; i++)
        {
            var c = commandLine[i];

            if ((c == '"' || c == '\'') && !inQuotes)
            {
                inQuotes = true;
                quoteChar = c;
            }
            else if (c == quoteChar && inQuotes)
            {
                inQuotes = false;
            }
            else if (c == ' ' && !inQuotes)
            {
                if (!string.IsNullOrEmpty(currentPart))
                {
                    parts.Add(currentPart);
                    currentPart = "";
                }
            }
            else
            {
                currentPart += c;
            }
        }

        if (!string.IsNullOrEmpty(currentPart))
        {
            parts.Add(currentPart);
        }

        return parts.ToArray();
    }
}
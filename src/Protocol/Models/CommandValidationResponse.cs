using System.Collections.Generic;

namespace LLMEmpoweredCommandPredictor.Protocol.Models;

/// <summary>
/// Response model for command validation results.
/// Contains detailed feedback about command correctness, syntax, and safety.
/// </summary>
public class CommandValidationResponse
{
    /// <summary>
    /// Whether the command is considered valid
    /// </summary>
    public bool IsValid { get; set; } = true;

    /// <summary>
    /// Overall validation level (Info, Warning, Error)
    /// </summary>
    public ValidationLevel ValidationLevel { get; set; } = ValidationLevel.Info;

    /// <summary>
    /// Primary validation message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Detailed validation messages with different severity levels
    /// </summary>
    public List<ValidationMessage> Messages { get; set; } = new();

    /// <summary>
    /// Parameterless constructor for JSON serialization
    /// </summary>
    public CommandValidationResponse()
    {
    }

    /// <summary>
    /// Constructor with primary message
    /// </summary>
    public CommandValidationResponse(bool isValid, string message, ValidationLevel level = ValidationLevel.Info)
    {
        IsValid = isValid;
        Message = message;
        ValidationLevel = level;
    }
}

/// <summary>
/// Individual validation message with severity level.
/// </summary>
public class ValidationMessage
{
    /// <summary>
    /// Severity level of this message
    /// </summary>
    public ValidationLevel Level { get; set; }

    /// <summary>
    /// Validation message text
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Parameterless constructor for JSON serialization
    /// </summary>
    public ValidationMessage()
    {
    }

    /// <summary>
    /// Constructor with level and text
    /// </summary>
    public ValidationMessage(ValidationLevel level, string text)
    {
        Level = level;
        Text = text;
    }
}

/// <summary>
/// Severity levels for validation messages.
/// </summary>
public enum ValidationLevel
{
    /// <summary>
    /// Informational message - command is correct
    /// </summary>
    Info = 0,

    /// <summary>
    /// Warning message - command may have issues but will likely work
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Error message - command has serious issues and may fail
    /// </summary>
    Error = 2
}
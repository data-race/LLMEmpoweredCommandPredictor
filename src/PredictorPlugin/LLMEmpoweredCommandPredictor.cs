using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Management.Automation;
using System.Management.Automation.Subsystem;
using System.Management.Automation.Subsystem.Prediction;
using LLMEmpoweredCommandPredictor;
using LLMEmpoweredCommandPredictor.PredictorCache;
using Microsoft.Extensions.Logging;

namespace PowerShell.Sample.LLMEmpoweredCommandPredictor
{
    public class LLMEmpoweredCommandPredictor : ICommandPredictor
    {
        private readonly Guid _guid;
        private readonly ILogger<LLMEmpoweredCommandPredictor> _logger;

        private ILLMSuggestionProvider _suggestionProvider;

        internal LLMEmpoweredCommandPredictor(string guid)
        {
            _guid = new Guid(guid);
            _logger = ConsoleLoggerFactory.CreateDebugLogger<LLMEmpoweredCommandPredictor>();
            _suggestionProvider = new LLMSuggestionProvider();
        }

        /// <summary>
        /// Gets the unique identifier for a subsystem implementation.
        /// </summary>
        public Guid Id => _guid;

        /// <summary>
        /// Gets the name of a subsystem implementation.
        /// </summary>
        public string Name => "AI-Predictor";

        /// <summary>
        /// Gets the description of a subsystem implementation.
        /// </summary>
        public string Description => "Command Predictor powered by LLM";

        /// <summary>
        /// Get the predictive suggestions. It indicates the start of a suggestion rendering session.
        /// </summary>
        /// <param name="client">Represents the client that initiates the call.</param>
        /// <param name="context">The <see cref="PredictionContext"/> object to be used for prediction.</param>
        /// <param name="cancellationToken">The cancellation token to cancel the prediction.</param>
        /// <returns>An instance of <see cref="SuggestionPackage"/>.</returns>
        public SuggestionPackage GetSuggestion(PredictionClient client, PredictionContext context, CancellationToken cancellationToken)
        {
            string input = context.InputAst.Extent.Text;
            
            if (string.IsNullOrWhiteSpace(input))
            {
                return default;
            }

            var suggestionContext = new LLMSuggestionContext
            {
                UserInput = input,
            };

            try
            {
                var suggestions = this._suggestionProvider.GetSuggestions(suggestionContext, cancellationToken);
                
                if (suggestions.Count == 0)
                {
                    return default;
                }
                
                var package = new SuggestionPackage(suggestions);
                return package;
            }
            catch (Exception ex)
            {
                _logger.LogError("PowerShell Plugin: Exception in GetSuggestion: {Error}", ex.Message);
                _logger.LogError("PowerShell Plugin: Exception StackTrace: {StackTrace}", ex.StackTrace);
                return default;
            }
        }

        #region "interface methods for processing feedback"

        /// <summary>
        /// Gets a value indicating whether the predictor accepts a specific kind of feedback.
        /// </summary>
        /// <param name="client">Represents the client that initiates the call.</param>
        /// <param name="feedback">A specific type of feedback.</param>
        /// <returns>True or false, to indicate whether the specific feedback is accepted.</returns>
        public bool CanAcceptFeedback(PredictionClient client, PredictorFeedbackKind feedback) => false;

        /// <summary>
        /// One or more suggestions provided by the predictor were displayed to the user.
        /// </summary>
        /// <param name="client">Represents the client that initiates the call.</param>
        /// <param name="session">The mini-session where the displayed suggestions came from.</param>
        /// <param name="countOrIndex">
        /// When the value is greater than 0, it's the number of displayed suggestions from the list
        /// returned in <paramref name="session"/>, starting from the index 0. When the value is
        /// less than or equal to 0, it means a single suggestion from the list got displayed, and
        /// the index is the absolute value.
        /// </param>
        public void OnSuggestionDisplayed(PredictionClient client, uint session, int countOrIndex) { }

        /// <summary>
        /// The suggestion provided by the predictor was accepted.
        /// </summary>
        /// <param name="client">Represents the client that initiates the call.</param>
        /// <param name="session">Represents the mini-session where the accepted suggestion came from.</param>
        /// <param name="acceptedSuggestion">The accepted suggestion text.</param>
        public void OnSuggestionAccepted(PredictionClient client, uint session, string acceptedSuggestion) 
        {
            _logger.LogInformation("PowerShell Plugin: Suggestion accepted: '{AcceptedSuggestion}' (session: {Session})", 
                acceptedSuggestion, session);
        }

        /// <summary>
        /// A command line was accepted to execute.
        /// The predictor can start processing early as needed with the latest history.
        /// </summary>
        /// <param name="client">Represents the client that initiates the call.</param>
        /// <param name="history">History command lines provided as references for prediction.</param>
        public void OnCommandLineAccepted(PredictionClient client, IReadOnlyList<string> history) 
        {
            if (history != null && history.Count > 0)
            {
                var latestCommand = history[history.Count - 1];
                _logger.LogInformation("PowerShell Plugin: Command line accepted: '{Command}' (client: {Client})", 
                    latestCommand, client.Name);
            }
        }

        /// <summary>
        /// A command line was done execution.
        /// </summary>
        /// <param name="client">Represents the client that initiates the call.</param>
        /// <param name="commandLine">The last accepted command line.</param>
        /// <param name="success">Shows whether the execution was successful.</param>
        public void OnCommandLineExecuted(PredictionClient client, string commandLine, bool success) 
        {
            _logger.LogInformation("PowerShell Plugin: Command executed: '{Command}' (success: {Success}, client: {Client})", 
                commandLine, success, client.Name);
            
            // Save the executed command to the cache (fire-and-forget)
            if (!string.IsNullOrWhiteSpace(commandLine))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _suggestionProvider.SaveCommandAsync(commandLine, success);
                        _logger.LogDebug("PowerShell Plugin: Command saved to cache: '{Command}'", commandLine);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("PowerShell Plugin: Failed to save command to cache: {Error}", ex.Message);
                    }
                });
            }
        }

        #endregion;
    }

    /// <summary>
    /// Register the predictor on module loading and unregister it on module un-loading.
    /// </summary>
    public class Init : IModuleAssemblyInitializer, IModuleAssemblyCleanup
    {
        private const string Identifier = "843b51d0-55c8-4c1a-8116-f0728d419306";

        /// <summary>
        /// Gets called when assembly is loaded.
        /// </summary>
        public void OnImport()
        {
            var predictor = new LLMEmpoweredCommandPredictor(Identifier);
            SubsystemManager.RegisterSubsystem(SubsystemKind.CommandPredictor, predictor);
        }

        /// <summary>
        /// Gets called when the binary module is unloaded.
        /// </summary>
        public void OnRemove(PSModuleInfo psModuleInfo)
        {
            SubsystemManager.UnregisterSubsystem(SubsystemKind.CommandPredictor, new Guid(Identifier));
        }
    }
}

using Sanet.MakaMek.Core.Models.Game;

namespace Sanet.MakaMek.Tools.BotContainer.Services;

/// <summary>
/// Creates <see cref="LlmDecisionEngineProvider"/> instances from the injectable
/// <see cref="BotAgentClient"/> and logger factory, so the composition root owns the collaborators.
/// </summary>
public sealed class LlmDecisionEngineProviderFactory : ILlmDecisionEngineProviderFactory
{
    private readonly BotAgentClient _botAgentClient;
    private readonly ILoggerFactory _loggerFactory;

    public LlmDecisionEngineProviderFactory(BotAgentClient botAgentClient, ILoggerFactory loggerFactory)
    {
        _botAgentClient = botAgentClient;
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc />
    public LlmDecisionEngineProvider Create(IClientGame clientGame, string mcpServerUrl) =>
        new(clientGame, _botAgentClient, mcpServerUrl, _loggerFactory);
}

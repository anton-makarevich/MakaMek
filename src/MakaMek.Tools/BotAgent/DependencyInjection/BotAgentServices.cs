using BotAgent.Configuration;
using BotAgent.Models.Agents;
using BotAgent.Orchestration;
using BotAgent.Services;
using BotAgent.Services.LlmProviders;
using Microsoft.Extensions.Options;
using Sanet.MakaMek.Core.Data.Serialization.Converters;

namespace BotAgent.DependencyInjection;

public static class BotAgentServices
{
    public static void AddBotAgentServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure logging
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddConsole();
            builder.AddDebug();
        });

        // Load API key from the environment variable if not in configuration
        var llmConfig = configuration.GetSection("LlmProvider");
        var apiKey = llmConfig["ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                configuration["LlmProvider:ApiKey"] = apiKey;
            }
        }

        // Configure options
        services.Configure<LlmProviderConfiguration>(configuration.GetSection("LlmProvider"));
        services.Configure<AgentConfiguration>(configuration.GetSection("Agent"));
        
        // Register LLM provider
        services.AddSingleton<LocalOpenAiLikeProvider>();
        services.AddSingleton<OpenAiProvider>();
        services.AddSingleton<ILlmProvider>(sp =>
        {
            var cfg = sp.GetRequiredService<IOptions<LlmProviderConfiguration>>().Value;
            return cfg.Type switch
            {
                "LocalOpenAI" => sp.GetRequiredService<LocalOpenAiLikeProvider>(),
                "OpenAI" => sp.GetRequiredService<OpenAiProvider>(),
                _ => throw new InvalidOperationException($"Unsupported LLM provider type '{cfg.Type}'.")
            };
        });

        // Register agents
        services.AddSingleton<DeploymentAgent>();
        services.AddSingleton<MovementAgent>();
        services.AddSingleton<WeaponsAttackAgent>();
        services.AddSingleton<EndPhaseAgent>();

        // Register orchestrator
        services.AddSingleton<AgentOrchestrator>();

        // Add controllers and API documentation
        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new GameCommandJsonConverter());
            });
        services.AddOpenApi();

        // Add CORS if needed
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });
    }
}

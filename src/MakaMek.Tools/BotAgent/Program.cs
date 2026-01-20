using BotAgent.Configuration;
using BotAgent.Models.Agents;
using BotAgent.Orchestration;
using BotAgent.Services;
using BotAgent.Services.LlmProviders;
using Sanet.MakaMek.Core.Data.Serialization.Converters;

var builder = WebApplication.CreateBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Load API key from environment variable if not in configuration
var llmConfig = builder.Configuration.GetSection("LlmProvider");
var apiKey = llmConfig["ApiKey"];
if (string.IsNullOrWhiteSpace(apiKey))
{
    apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    if (!string.IsNullOrWhiteSpace(apiKey))
    {
        builder.Configuration["LlmProvider:ApiKey"] = apiKey;
    }
}

// Configure options
builder.Services.Configure<LlmProviderConfiguration>(
    builder.Configuration.GetSection("LlmProvider"));
builder.Services.Configure<AgentConfiguration>(
    builder.Configuration.GetSection("Agent"));

// Register services
builder.Services.AddHttpClient<McpClientService>();
builder.Services.AddSingleton<ILlmProvider, LocalOpenAiLikeProvider>();

// Register agents
builder.Services.AddSingleton<DeploymentAgent>();
builder.Services.AddSingleton<MovementAgent>();
builder.Services.AddSingleton<WeaponsAttackAgent>();
builder.Services.AddSingleton<EndPhaseAgent>();

// Register orchestrator
builder.Services.AddSingleton<AgentOrchestrator>();

// Add controllers and API documentation
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new GameCommandJsonConverter());
    });
builder.Services.AddOpenApi();

// Add CORS if needed
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseHttpsRedirection();
app.MapControllers();

app.Logger.LogInformation("BotAgent application starting...");
app.Logger.LogInformation("LLM Provider: {Provider}, Model: {Model}",
    llmConfig["Type"],
    llmConfig["Model"]);

app.Run();

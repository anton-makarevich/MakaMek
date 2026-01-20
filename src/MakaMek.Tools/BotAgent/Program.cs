using BotAgent.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Configure all BotAgent services
builder.Services.AddBotAgentServices(builder.Configuration);

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
    builder.Configuration["LlmProvider:Type"],
    builder.Configuration["LlmProvider:Model"]);

app.Run();

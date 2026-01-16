using MakaMek.Tools.BotContainer.Services;

var builder = WebApplication.CreateBuilder(args);

// Register all services using the DI extension
builder.Services.AddBotContainerServices();

var app = builder.Build();

app.UseHttpsRedirection();

// Basic health check endpoint
app.MapGet("/health", () => "Integration Bot Container Running");

app.Run();

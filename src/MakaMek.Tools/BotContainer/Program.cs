using Sanet.MakaMek.Tools.BotContainer.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Register all services using the DI extension
builder.Services.AddBotContainerServices(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Basic health check endpoint
app.MapGet("/health", () => "Integration Bot Container Running");

app.MapMcp();

app.Run();

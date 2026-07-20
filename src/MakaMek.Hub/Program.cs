using System.Text.Json.Serialization;
using Sanet.MakaMek.Hub.Configuration;
using Sanet.MakaMek.Hub.Rooms;
using Sanet.MakaMek.Hub.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<HubOptions>()
    .Bind(builder.Configuration.GetSection(HubOptions.SectionName))
    .Validate(
        options => options.MaxConcurrentRooms > 0,
        $"{HubOptions.SectionName}:MaxConcurrentRooms must be greater than zero.")
    .ValidateOnStart();

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IRoomCodeGenerator, CryptographicRoomCodeGenerator>();
builder.Services.AddSingleton<IRoomManager, RoomManager>();

var app = builder.Build();

app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
app.MapControllers();

app.Run();

public partial class Program;

using ReservationSystem.Microservices.DatabaseMcp.Middleware;
using ReservationSystem.Microservices.DatabaseMcp.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools<DatabaseTools>();

var app = builder.Build();

app.UseMiddleware<ApiKeyMiddleware>();
app.MapMcp();

app.Run();

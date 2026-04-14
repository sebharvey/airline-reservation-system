// Description: Entry point and host configuration for the User Azure Functions API

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReservationSystem.Microservices.User.Application.AddUser;
using ReservationSystem.Microservices.User.Application.GetAllUsers;
using ReservationSystem.Microservices.User.Application.GetUser;
using ReservationSystem.Microservices.User.Application.UpdateUser;
using ReservationSystem.Microservices.User.Application.SetUserStatus;
using ReservationSystem.Microservices.User.Application.DeleteUser;
using ReservationSystem.Microservices.User.Application.UnlockUser;
using ReservationSystem.Microservices.User.Application.ResetPassword;
using ReservationSystem.Microservices.User.Application.Login;
using ReservationSystem.Microservices.User.Domain.Repositories;
using ReservationSystem.Microservices.User.Infrastructure.Persistence;
using ReservationSystem.Microservices.User.Swagger;
using ReservationSystem.Shared.Business.Infrastructure.Configuration;
using ReservationSystem.Shared.Business.Security;
using ReservationSystem.Shared.Common.Caching;
using ReservationSystem.Shared.Common.Health;
using ReservationSystem.Shared.Common.Infrastructure.Configuration;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(worker =>
    {
        worker.UseMicroserviceCache();
        worker.UseNewtonsoftJson();
    })
    .ConfigureOpenApi()
    .ConfigureServices((context, services) =>
    {
        // ── Caching ────────────────────────────────────────────────────────────
        services.AddMicroserviceCache();

        // ── OpenAPI ────────────────────────────────────────────────────────────
        services.AddSingleton<IOpenApiConfigurationOptions, OpenApiConfigurationOptions>();

        // ── Telemetry ──────────────────────────────────────────────────────────
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // ── Configuration ──────────────────────────────────────────────────────
        services.Configure<DatabaseOptions>(
            context.Configuration.GetSection(DatabaseOptions.SectionName));
        services.AddOptions<JwtOptions>()
            .Bind(context.Configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // ── Infrastructure ─────────────────────────────────────────────────────
        services.AddDbContext<UserDbContext>((provider, options) =>
        {
            var dbOptions = provider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<DatabaseOptions>>()
                .Value;
            options.UseSqlServer(dbOptions.ConnectionString, sqlOptions =>
            {
                sqlOptions.CommandTimeout(dbOptions.CommandTimeoutSeconds);
            });
        });
        services.AddScoped<IUserRepository, EfUserRepository>();
        services.AddScoped<IJwtService, JwtService>();

        // ── Health check ───────────────────────────────────────────────────────
        services.AddHealthCheck("SqlHealthCheck", sp => ct => Task.FromResult(true));

        // ── Application use-case handlers ──────────────────────────────────────
        services.AddScoped<AddUserHandler>();
        services.AddScoped<GetAllUsersHandler>();
        services.AddScoped<GetUserHandler>();
        services.AddScoped<UpdateUserHandler>();
        services.AddScoped<SetUserStatusHandler>();
        services.AddScoped<UnlockUserHandler>();
        services.AddScoped<ResetPasswordHandler>();
        services.AddScoped<DeleteUserHandler>();
        services.AddScoped<LoginHandler>();
    })
    .Build();

host.Run();

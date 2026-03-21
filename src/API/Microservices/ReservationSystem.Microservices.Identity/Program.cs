// Description: Entry point and host configuration for the Identity Azure Functions API

using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReservationSystem.Microservices.Identity.Application.CreateAccount;
using ReservationSystem.Microservices.Identity.Application.DeleteAccount;
using ReservationSystem.Microservices.Identity.Application.EmailChangeRequest;
using ReservationSystem.Microservices.Identity.Application.Login;
using ReservationSystem.Microservices.Identity.Application.Logout;
using ReservationSystem.Microservices.Identity.Application.RefreshToken;
using ReservationSystem.Microservices.Identity.Application.ResetPassword;
using ReservationSystem.Microservices.Identity.Application.ResetPasswordRequest;
using ReservationSystem.Microservices.Identity.Application.VerifyEmail;
using ReservationSystem.Microservices.Identity.Application.VerifyEmailChange;
using ReservationSystem.Microservices.Identity.Domain.Repositories;
using ReservationSystem.Microservices.Identity.Infrastructure.Persistence;
using ReservationSystem.Shared.Common.Health;
using ReservationSystem.Shared.Common.Infrastructure.Configuration;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        // ── Telemetry ──────────────────────────────────────────────────────────
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // ── Configuration ──────────────────────────────────────────────────────
        services.Configure<DatabaseOptions>(
            context.Configuration.GetSection(DatabaseOptions.SectionName));

        // ── Infrastructure ─────────────────────────────────────────────────────
        services.AddDbContext<IdentityDbContext>((provider, options) =>
        {
            var dbOptions = provider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<DatabaseOptions>>()
                .Value;
            options.UseSqlServer(dbOptions.ConnectionString, sqlOptions =>
            {
                sqlOptions.CommandTimeout(dbOptions.CommandTimeoutSeconds);
            });
        });
        services.AddHttpClient();
        services.AddScoped<IUserAccountRepository, EfUserAccountRepository>();
        services.AddScoped<IRefreshTokenRepository, EfRefreshTokenRepository>();

        // ── Health check ───────────────────────────────────────────────────────
        services.AddHealthCheck("SqlHealthCheck", sp => ct => Task.FromResult(true));

        // ── Application use-case handlers ──────────────────────────────────────
        services.AddScoped<LoginHandler>();
        services.AddScoped<RefreshTokenHandler>();
        services.AddScoped<LogoutHandler>();
        services.AddScoped<ResetPasswordRequestHandler>();
        services.AddScoped<ResetPasswordHandler>();
        services.AddScoped<CreateAccountHandler>();
        services.AddScoped<DeleteAccountHandler>();
        services.AddScoped<VerifyEmailHandler>();
        services.AddScoped<EmailChangeRequestHandler>();
        services.AddScoped<VerifyEmailChangeHandler>();
    })
    .Build();

host.Run();

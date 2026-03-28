using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReservationSystem.Shared.Common.Health;
using ReservationSystem.Orchestration.Retail.Swagger;
using ReservationSystem.Orchestration.Retail.Application.SearchFlights;
using ReservationSystem.Orchestration.Retail.Application.CreateBasket;
using ReservationSystem.Orchestration.Retail.Application.ConfirmBasket;
using ReservationSystem.Orchestration.Retail.Application.GetOrder;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(worker => worker.UseNewtonsoftJson())
    .ConfigureOpenApi()
    .ConfigureServices((context, services) =>
    {
        // ── OpenAPI ────────────────────────────────────────────────────────────
        services.AddSingleton<IOpenApiConfigurationOptions, OpenApiConfigurationOptions>();

        // ── Telemetry ──────────────────────────────────────────────────────────
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // ── Named HttpClients for downstream microservices ─────────────────────
        services.AddHttpClient("OfferMs", client =>
        {
            client.BaseAddress = context.Configuration["OfferMs:BaseUrl"] is { } url ? new Uri(url) : null;
        });

        services.AddHttpClient("OrderMs", client =>
        {
            client.BaseAddress = context.Configuration["OrderMs:BaseUrl"] is { } url ? new Uri(url) : null;
        });

        services.AddHttpClient("SeatMs", client =>
        {
            client.BaseAddress = context.Configuration["SeatMs:BaseUrl"] is { } url ? new Uri(url) : null;
        });

        services.AddHttpClient("BagMs", client =>
        {
            client.BaseAddress = context.Configuration["BagMs:BaseUrl"] is { } url ? new Uri(url) : null;
        });

        services.AddHttpClient("PaymentMs", client =>
        {
            client.BaseAddress = context.Configuration["PaymentMs:BaseUrl"] is { } url ? new Uri(url) : null;
        });

        services.AddHttpClient("DeliveryMs", client =>
        {
            client.BaseAddress = context.Configuration["DeliveryMs:BaseUrl"] is { } url ? new Uri(url) : null;
        });

        services.AddHttpClient("CustomerMs", client =>
        {
            client.BaseAddress = context.Configuration["CustomerMs:BaseUrl"] is { } url ? new Uri(url) : null;
        });

        // ── Health check ───────────────────────────────────────────────────────
        services.AddHealthCheck("HealthCheck", sp => ct => Task.FromResult(true));

        // ── Infrastructure clients ─────────────────────────────────────────────
        services.AddScoped<OfferServiceClient>();
        services.AddScoped<OrderServiceClient>();
        services.AddScoped<PaymentServiceClient>();
        services.AddScoped<SeatServiceClient>();
        services.AddScoped<BagServiceClient>();
        services.AddScoped<DeliveryServiceClient>();

        // ── Application use-case handlers ──────────────────────────────────────
        services.AddScoped<SearchFlightsHandler>();
        services.AddScoped<CreateBasketHandler>();
        services.AddScoped<ConfirmBasketHandler>();
        services.AddScoped<GetOrderHandler>();
    })
    .Build();

host.Run();

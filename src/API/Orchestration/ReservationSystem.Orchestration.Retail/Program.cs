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
            client.BaseAddress = new Uri(context.Configuration["OfferMs:BaseUrl"] ?? "https://localhost:7071/");
        });

        services.AddHttpClient("OrderMs", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["OrderMs:BaseUrl"] ?? "https://localhost:7072/");
        });

        services.AddHttpClient("SeatMs", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["SeatMs:BaseUrl"] ?? "https://localhost:7073/");
        });

        services.AddHttpClient("BagMs", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["BagMs:BaseUrl"] ?? "https://localhost:7074/");
        });

        services.AddHttpClient("PaymentMs", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["PaymentMs:BaseUrl"] ?? "https://localhost:7075/");
        });

        services.AddHttpClient("DeliveryMs", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["DeliveryMs:BaseUrl"] ?? "https://localhost:7076/");
        });

        services.AddHttpClient("CustomerMs", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["CustomerMs:BaseUrl"] ?? "https://localhost:7077/");
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

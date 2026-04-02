using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReservationSystem.Shared.Common.Health;
using ReservationSystem.Shared.Business.Middleware;
using ReservationSystem.Orchestration.Retail.Swagger;
using ReservationSystem.Orchestration.Retail.Application.SearchFlights;
using ReservationSystem.Orchestration.Retail.Application.CreateBasket;
using ReservationSystem.Orchestration.Retail.Application.ConfirmBasket;
using ReservationSystem.Orchestration.Retail.Application.GetOrder;
using ReservationSystem.Orchestration.Retail.Application.GetAdminOrders;
using ReservationSystem.Orchestration.Retail.Application.GetAdminOrderDetail;
using ReservationSystem.Orchestration.Retail.Application.GetAdminOrderTickets;
using ReservationSystem.Orchestration.Retail.Application.GetFlightInventory;
using ReservationSystem.Orchestration.Retail.Application.GetSsrOptions;
using ReservationSystem.Orchestration.Retail.Application.OciRetrieve;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(worker =>
    {
        worker.UseMiddleware<TerminalAuthenticationMiddleware>();
        worker.UseNewtonsoftJson();
    })
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
            client.BaseAddress = new Uri(context.Configuration["OfferMs:BaseUrl"] ?? "https://reservation-system-db-microservice-offer-dnfdbebdezemaghp.uksouth-01.azurewebsites.net/");
            var hostKey = context.Configuration["OfferMs:HostKey"];
            if (!string.IsNullOrEmpty(hostKey))
                client.DefaultRequestHeaders.Add("x-functions-key", hostKey);
        });

        services.AddHttpClient("OrderMs", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["OrderMs:BaseUrl"] ?? "https://reservation-system-db-microservice-order-cnc3fpdzfucbhudc.uksouth-01.azurewebsites.net/");
        });

        services.AddHttpClient("AncillaryMs", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["AncillaryMs:BaseUrl"] ?? "https://reservation-system-microservice-ancillary-dkdfdjfba9fcbvfk.uksouth-01.azurewebsites.net/");
        });

        services.AddHttpClient("PaymentMs", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["PaymentMs:BaseUrl"] ?? "https://reservation-system-db-microservice-payment-f3amf7a6bmauhjd6.uksouth-01.azurewebsites.net/");
        });

        services.AddHttpClient("DeliveryMs", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["DeliveryMs:BaseUrl"] ?? "https://reservation-system-db-microservice-delivery-ehe2f4c3dybehwat.uksouth-01.azurewebsites.net/");
        });

        services.AddHttpClient("CustomerMs", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["CustomerMs:BaseUrl"] ?? "https://reservation-system-db-microservice-customer-axdydza6brbkc0ck.uksouth-01.azurewebsites.net/");
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
        services.AddScoped<GetFlightInventoryHandler>();
        services.AddScoped<GetAdminOrdersHandler>();
        services.AddScoped<GetAdminOrderDetailHandler>();
        services.AddScoped<GetAdminOrderTicketsHandler>();
        services.AddScoped<GetSsrOptionsHandler>();
        services.AddScoped<OciRetrieveHandler>();
    })
    .Build();

host.Run();

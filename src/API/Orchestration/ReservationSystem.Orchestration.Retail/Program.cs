using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReservationSystem.Shared.Common.Health;
using ReservationSystem.Shared.Business.Middleware;
using ReservationSystem.Orchestration.Retail.Middleware;
using ReservationSystem.Orchestration.Retail.Swagger;
using ReservationSystem.Orchestration.Retail.Application.SearchFlights;
using ReservationSystem.Orchestration.Retail.Application.CreateBasket;
using ReservationSystem.Orchestration.Retail.Application.ConfirmBasket;
using ReservationSystem.Orchestration.Retail.Application.BasketSummary;
using ReservationSystem.Orchestration.Retail.Application.PaymentSummary;
using ReservationSystem.Orchestration.Retail.Application.GetOrder;
using ReservationSystem.Orchestration.Retail.Application.ValidateOrder;
using ReservationSystem.Orchestration.Retail.Application.GetAdminOrders;
using ReservationSystem.Orchestration.Retail.Application.GetAdminOrderDetail;
using ReservationSystem.Orchestration.Retail.Application.GetAdminOrderDocuments;
using ReservationSystem.Orchestration.Retail.Application.GetAdminOrderTickets;
using ReservationSystem.Orchestration.Retail.Application.GetFlightInventory;
using ReservationSystem.Orchestration.Retail.Application.GetSsrOptions;
using ReservationSystem.Orchestration.Retail.Application.CancelOrder;
using ReservationSystem.Orchestration.Retail.Application.AddOrderBags;
using ReservationSystem.Orchestration.Retail.Application.UpdateOrderSeats;
using ReservationSystem.Orchestration.Retail.Application.ChangeOrder;
using ReservationSystem.Orchestration.Retail.Application.CheckInAncillaries;
using ReservationSystem.Orchestration.Retail.Application.NdcAirShopping;
using ReservationSystem.Orchestration.Retail.Application.NdcOfferPrice;
using ReservationSystem.Orchestration.Retail.Application.NdcServiceList;
using ReservationSystem.Orchestration.Retail.Application.NdcOrderCreate;
using ReservationSystem.Orchestration.Retail.Application.NdcOrderRetrieve;
using ReservationSystem.Orchestration.Retail.Application.NdcSeatAvailability;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(worker =>
    {
        worker.UseMiddleware<TerminalAuthenticationMiddleware>();
        worker.UseMiddleware<TokenVerificationMiddleware>();
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
        services.AddHttpClient("IdentityMs", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["IdentityMs:BaseUrl"]!);
            var hostKey = context.Configuration["MicroserviceHostKey"];
            if (!string.IsNullOrEmpty(hostKey))
                client.DefaultRequestHeaders.Add("x-functions-key", hostKey);
        });

        services.AddHttpClient("OfferMs", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["OfferMs:BaseUrl"]!);
            var hostKey = context.Configuration["MicroserviceHostKey"];
            if (!string.IsNullOrEmpty(hostKey))
                client.DefaultRequestHeaders.Add("x-functions-key", hostKey);
        });

        services.AddHttpClient("OrderMs", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["OrderMs:BaseUrl"]!);
            var hostKey = context.Configuration["MicroserviceHostKey"];
            if (!string.IsNullOrEmpty(hostKey))
                client.DefaultRequestHeaders.Add("x-functions-key", hostKey);
        });

        services.AddHttpClient("AncillaryMs", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["AncillaryMs:BaseUrl"]!);
            var hostKey = context.Configuration["MicroserviceHostKey"];
            if (!string.IsNullOrEmpty(hostKey))
                client.DefaultRequestHeaders.Add("x-functions-key", hostKey);
        });

        services.AddHttpClient("PaymentMs", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["PaymentMs:BaseUrl"]!);
            var hostKey = context.Configuration["MicroserviceHostKey"];
            if (!string.IsNullOrEmpty(hostKey))
                client.DefaultRequestHeaders.Add("x-functions-key", hostKey);
        });

        services.AddHttpClient("DeliveryMs", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["DeliveryMs:BaseUrl"]!);
            var hostKey = context.Configuration["MicroserviceHostKey"];
            if (!string.IsNullOrEmpty(hostKey))
                client.DefaultRequestHeaders.Add("x-functions-key", hostKey);
        });

        services.AddHttpClient("CustomerMs", client =>
        {
            client.BaseAddress = new Uri(context.Configuration["CustomerMs:BaseUrl"]!);
            var hostKey = context.Configuration["MicroserviceHostKey"];
            if (!string.IsNullOrEmpty(hostKey))
                client.DefaultRequestHeaders.Add("x-functions-key", hostKey);
        });

        // ── Health check ───────────────────────────────────────────────────────
        services.AddHealthCheck("HealthCheck", sp => ct => Task.FromResult(true));

        // ── Infrastructure clients ─────────────────────────────────────────────
        services.AddScoped<IdentityServiceClient>();
        services.AddScoped<OfferServiceClient>();
        services.AddScoped<OrderServiceClient>();
        services.AddScoped<PaymentServiceClient>();
        services.AddScoped<SeatServiceClient>();
        services.AddScoped<BagServiceClient>();
        services.AddScoped<ProductServiceClient>();
        services.AddScoped<DeliveryServiceClient>();
        services.AddScoped<CustomerServiceClient>();

        // ── Application use-case handlers ──────────────────────────────────────
        services.AddScoped<SearchFlightsHandler>();
        services.AddScoped<SearchConnectingFlightsHandler>();
        services.AddScoped<CreateBasketHandler>();
        services.AddScoped<ConfirmBasketHandler>();
        services.AddScoped<BasketSummaryHandler>();
        services.AddScoped<PaymentSummaryHandler>();
        services.AddScoped<GetOrderHandler>();
        services.AddScoped<ValidateOrderHandler>();
        services.AddScoped<GetFlightInventoryHandler>();
        services.AddScoped<GetAdminOrdersHandler>();
        services.AddScoped<GetAdminOrderDetailHandler>();
        services.AddScoped<GetAdminOrderTicketsHandler>();
        services.AddScoped<GetAdminOrderDocumentsHandler>();
        services.AddScoped<GetSsrOptionsHandler>();
        services.AddScoped<CancelOrderHandler>();
        services.AddScoped<AddOrderBagsHandler>();
        services.AddScoped<UpdateOrderSeatsHandler>();
        services.AddScoped<ChangeOrderHandler>();
        services.AddScoped<CheckInAncillariesHandler>();
        services.AddScoped<NdcAirShoppingHandler>();
        services.AddScoped<NdcOfferPriceHandler>();
        services.AddScoped<NdcServiceListHandler>();
        services.AddScoped<NdcOrderCreateHandler>();
        services.AddScoped<NdcOrderRetrieveHandler>();
        services.AddScoped<NdcSeatAvailabilityHandler>();
    })
    .Build();

host.Run();

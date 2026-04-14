using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReservationSystem.Microservices.Ancillary.Swagger;
using ReservationSystem.Microservices.Ancillary.Application.Seat.CreateAircraftType;
using ReservationSystem.Microservices.Ancillary.Application.Seat.CreateSeatmap;
using ReservationSystem.Microservices.Ancillary.Application.Seat.CreateSeatPricing;
using ReservationSystem.Microservices.Ancillary.Application.Seat.DeleteSeatPricing;
using ReservationSystem.Microservices.Ancillary.Application.Seat.GetAircraftType;
using ReservationSystem.Microservices.Ancillary.Application.Seat.GetAllAircraftTypes;
using ReservationSystem.Microservices.Ancillary.Application.Seat.GetAllSeatPricings;
using ReservationSystem.Microservices.Ancillary.Application.Seat.GetSeatmap;
using ReservationSystem.Microservices.Ancillary.Application.Seat.GetSeatOffer;
using ReservationSystem.Microservices.Ancillary.Application.Seat.GetSeatOffers;
using ReservationSystem.Microservices.Ancillary.Application.Seat.GetSeatPricing;
using ReservationSystem.Microservices.Ancillary.Application.Seat.UpdateAircraftType;
using ReservationSystem.Microservices.Ancillary.Application.Seat.DeleteAircraftType;
using ReservationSystem.Microservices.Ancillary.Application.Seat.DeleteSeatmap;
using ReservationSystem.Microservices.Ancillary.Application.Seat.GetAllSeatmaps;
using ReservationSystem.Microservices.Ancillary.Application.Seat.GetSeatmapById;
using ReservationSystem.Microservices.Ancillary.Application.Seat.UpdateSeatmap;
using ReservationSystem.Microservices.Ancillary.Application.Seat.UpdateSeatPricing;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Seat;
using ReservationSystem.Microservices.Ancillary.Infrastructure.Persistence.Seat;
using ReservationSystem.Microservices.Ancillary.Application.Bag.CreateBagPolicy;
using ReservationSystem.Microservices.Ancillary.Application.Bag.CreateBagPricing;
using ReservationSystem.Microservices.Ancillary.Application.Bag.DeleteBagPolicy;
using ReservationSystem.Microservices.Ancillary.Application.Bag.DeleteBagPricing;
using ReservationSystem.Microservices.Ancillary.Application.Bag.GetAllBagPolicies;
using ReservationSystem.Microservices.Ancillary.Application.Bag.GetAllBagPricings;
using ReservationSystem.Microservices.Ancillary.Application.Bag.GetBagPolicy;
using ReservationSystem.Microservices.Ancillary.Application.Bag.GetBagPricing;
using ReservationSystem.Microservices.Ancillary.Application.Bag.UpdateBagPolicy;
using ReservationSystem.Microservices.Ancillary.Application.Bag.UpdateBagPricing;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Bag;
using ReservationSystem.Microservices.Ancillary.Infrastructure.Persistence;
using ReservationSystem.Microservices.Ancillary.Infrastructure.Persistence.Bag;
using ReservationSystem.Microservices.Ancillary.Application.Product.CreateProductGroup;
using ReservationSystem.Microservices.Ancillary.Application.Product.UpdateProductGroup;
using ReservationSystem.Microservices.Ancillary.Application.Product.DeleteProductGroup;
using ReservationSystem.Microservices.Ancillary.Application.Product.GetProductGroup;
using ReservationSystem.Microservices.Ancillary.Application.Product.GetAllProductGroups;
using ReservationSystem.Microservices.Ancillary.Application.Product.CreateProduct;
using ReservationSystem.Microservices.Ancillary.Application.Product.UpdateProduct;
using ReservationSystem.Microservices.Ancillary.Application.Product.DeleteProduct;
using ReservationSystem.Microservices.Ancillary.Application.Product.GetProduct;
using ReservationSystem.Microservices.Ancillary.Application.Product.GetAllProducts;
using ReservationSystem.Microservices.Ancillary.Application.Product.CreateProductPrice;
using ReservationSystem.Microservices.Ancillary.Application.Product.UpdateProductPrice;
using ReservationSystem.Microservices.Ancillary.Application.Product.DeleteProductPrice;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Product;
using ReservationSystem.Microservices.Ancillary.Infrastructure.Persistence.Product;
using ReservationSystem.Shared.Common.Caching;
using ReservationSystem.Shared.Common.Health;
using ReservationSystem.Shared.Common.Infrastructure.Configuration;
using ReservationSystem.Shared.Common.Infrastructure.Persistence;

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

        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        services.Configure<DatabaseOptions>(
            context.Configuration.GetSection(DatabaseOptions.SectionName));

        // ── Infrastructure ─────────────────────────────────────────────────────
        services.AddSingleton<SqlConnectionFactory>();
        services.AddScoped<IAircraftTypeRepository, SqlAircraftTypeRepository>();
        services.AddScoped<ISeatmapRepository, SqlSeatmapRepository>();
        services.AddScoped<ISeatPricingRepository, SqlSeatPricingRepository>();
        services.AddDbContext<AncillaryDbContext>((provider, options) =>
        {
            var dbOptions = provider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<DatabaseOptions>>()
                .Value;
            options.UseSqlServer(dbOptions.ConnectionString, sqlOptions =>
            {
                sqlOptions.CommandTimeout(dbOptions.CommandTimeoutSeconds);
            });
        });
        services.AddScoped<IBagPolicyRepository, EfBagPolicyRepository>();
        services.AddScoped<IBagPricingRepository, EfBagPricingRepository>();
        services.AddScoped<IProductGroupRepository, EfProductGroupRepository>();
        services.AddScoped<IProductRepository, EfProductRepository>();
        services.AddScoped<IProductPriceRepository, EfProductPriceRepository>();

        // ── Health check ────────────────────────────────────────────────────────
        services.AddHealthCheck("SqlHealthCheck", sp => ct => Task.FromResult(true));

        // ── Application use-case handlers (Seat) ────────────────────────────────
        services.AddScoped<GetSeatmapHandler>();
        services.AddScoped<GetSeatOffersHandler>();
        services.AddScoped<GetSeatOfferHandler>();
        services.AddScoped<GetAllAircraftTypesHandler>();
        services.AddScoped<CreateAircraftTypeHandler>();
        services.AddScoped<GetAircraftTypeHandler>();
        services.AddScoped<UpdateAircraftTypeHandler>();
        services.AddScoped<GetAllSeatPricingsHandler>();
        services.AddScoped<CreateSeatPricingHandler>();
        services.AddScoped<GetSeatPricingHandler>();
        services.AddScoped<UpdateSeatPricingHandler>();
        services.AddScoped<DeleteSeatPricingHandler>();
        services.AddScoped<DeleteAircraftTypeHandler>();
        services.AddScoped<GetAllSeatmapsHandler>();
        services.AddScoped<GetSeatmapByIdHandler>();
        services.AddScoped<CreateSeatmapHandler>();
        services.AddScoped<UpdateSeatmapHandler>();
        services.AddScoped<DeleteSeatmapHandler>();

        // ── Application use-case handlers (Bag) ─────────────────────────────────
        services.AddScoped<GetBagPolicyHandler>();
        services.AddScoped<GetAllBagPoliciesHandler>();
        services.AddScoped<CreateBagPolicyHandler>();
        services.AddScoped<UpdateBagPolicyHandler>();
        services.AddScoped<DeleteBagPolicyHandler>();
        services.AddScoped<GetBagPricingHandler>();
        services.AddScoped<GetAllBagPricingsHandler>();
        services.AddScoped<CreateBagPricingHandler>();
        services.AddScoped<UpdateBagPricingHandler>();
        services.AddScoped<DeleteBagPricingHandler>();

        // ── Application use-case handlers (Product) ──────────────────────────────
        services.AddScoped<GetAllProductGroupsHandler>();
        services.AddScoped<GetProductGroupHandler>();
        services.AddScoped<CreateProductGroupHandler>();
        services.AddScoped<UpdateProductGroupHandler>();
        services.AddScoped<DeleteProductGroupHandler>();
        services.AddScoped<GetAllProductsHandler>();
        services.AddScoped<GetProductHandler>();
        services.AddScoped<CreateProductHandler>();
        services.AddScoped<UpdateProductHandler>();
        services.AddScoped<DeleteProductHandler>();
        services.AddScoped<CreateProductPriceHandler>();
        services.AddScoped<UpdateProductPriceHandler>();
        services.AddScoped<DeleteProductPriceHandler>();
    })
    .Build();

host.Run();

using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Configurations;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.OpenApi.Models;

namespace ReservationSystem.Microservices.Delivery.Swagger;

internal sealed class OpenApiConfigurationOptions : DefaultOpenApiConfigurationOptions
{
    public override OpenApiInfo Info { get; set; } = new()
    {
        Version = "1.0.0",
        Title = "Apex Air \u2013 Delivery API",
        Description = "Manages ticket issuance, manifests, documents, and boarding cards for the Apex Air reservation system."
    };

    public override OpenApiVersionType OpenApiVersion { get; set; } = OpenApiVersionType.V3;

    public override bool ForceHttps { get; set; } = true;
}

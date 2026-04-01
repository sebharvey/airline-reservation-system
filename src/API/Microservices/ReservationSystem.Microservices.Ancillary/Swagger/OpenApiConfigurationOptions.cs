using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Configurations;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.OpenApi.Models;

namespace ReservationSystem.Microservices.Ancillary.Swagger;

internal sealed class OpenApiConfigurationOptions : DefaultOpenApiConfigurationOptions
{
    public override OpenApiInfo Info { get; set; } = new()
    {
        Version = "1.0.0",
        Title = "Apex Air \u2013 Ancillary API",
        Description = "Manages seat and bag ancillary services including aircraft types, seatmaps, seat pricing, seat offers, bag policies, bag pricing, and bag offers for the Apex Air reservation system."
    };

    public override OpenApiVersionType OpenApiVersion { get; set; } = OpenApiVersionType.V3;

    // Force HTTPS in the generated spec so server URLs match the Azure deployment.
    public override bool ForceHttps { get; set; } = true;
}

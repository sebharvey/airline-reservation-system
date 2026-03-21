// Author: Seb Harvey
// Description: OpenAPI document metadata for the Template API.

using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Configurations;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.OpenApi.Models;

namespace ReservationSystem.Template.TemplateApi.Swagger;

internal sealed class OpenApiConfigurationOptions : DefaultOpenApiConfigurationOptions
{
    public override OpenApiInfo Info { get; set; } = new()
    {
        Version = "1.0.0",
        Title = "Apex Air \u2013 Template API",
        Description = "Reference scaffold for all Apex Air microservices. Demonstrates Clean Architecture conventions, CRUD patterns for Person and TemplateItem resources, and external service integration."
    };

    public override OpenApiVersionType OpenApiVersion { get; set; } = OpenApiVersionType.V3;

    // Ensure the generated swagger endpoints (/swagger.json, /openapi/v3.json, /swagger/ui)
    // are accessible anonymously — no function key required.
    public override OpenApiAuthorizationLevel OpenApiAuthorizationLevel { get; set; } =
        OpenApiAuthorizationLevel.Anonymous;

    // Force HTTPS in the generated spec so server URLs match the Azure deployment.
    public override bool ForceHttps { get; set; } = true;
}

// Author: Seb Harvey
// Description: Entry point and host configuration for the Severn Valley Timetable Azure Functions API

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReservationSystem.Template.TemplateApi.Functions;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        
        // Register TimetableService as singleton for caching
        services.AddSingleton<TimetableService>();
    })
    .Build();

host.Run();

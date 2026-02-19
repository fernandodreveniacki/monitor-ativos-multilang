using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;


var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHttpClient("producer", client =>
{
    var baseUrl = Environment.GetEnvironmentVariable("PRODUCER_BASE_URL")
                ?? "http://python-producer:8000";
    client.BaseAddress = new Uri(baseUrl);
});

builder.Services.AddHostedService<Worker.Worker>();

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(o =>
{
    o.IncludeScopes = true;
    o.UseUtcTimestamp = true;
    o.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffz ";
});

builder.Services.AddSingleton<Worker.Observability.InMemoryMetrics>();

await builder.Build().RunAsync();
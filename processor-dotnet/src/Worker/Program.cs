using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHttpClient("producer", client =>
{
    var baseUrl = Environment.GetEnvironmentVariable("PRODUCER_BASE_URL")
                ?? "http://python-producer:8000";
    client.BaseAddress = new Uri(baseUrl);
});

builder.Services.AddHostedService<Worker.Worker>();

await builder.Build().RunAsync();
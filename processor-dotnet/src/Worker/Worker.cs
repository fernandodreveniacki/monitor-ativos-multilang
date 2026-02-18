using System.Net.Http.Json;

using Microsoft.Extensions.Logging;
using Worker.Models;

namespace Worker;

public class Worker : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<Worker> _logger;

    public Worker(IHttpClientFactory httpClientFactory, ILogger<Worker> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = int.TryParse(
            Environment.GetEnvironmentVariable("POLL_INTERVAL_SECONDS"),
            out var seconds
        ) ? seconds : 5;

        var symbols = Environment.GetEnvironmentVariable("SYMBOLS") ?? "BTCUSD,AAPL,PETR4";

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

        _logger.LogInformation("Processor Worker started | interval={Interval}s | symbols={Symbols}",
        intervalSeconds, symbols);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var client = _httpClientFactory.CreateClient("producer");

                var envelope = await client.GetFromJsonAsync<QuoteEnvelope>(
                    $"/quotes?symbols={symbols}",
                    cancellationToken: stoppingToken
                );

                if (envelope?.quotes is null || envelope.quotes.Count == 0)
                {
                    _logger.LogWarning("No quotes received");
                    continue;
                }

                foreach (var q in envelope.quotes)
                {
                    _logger.LogInformation("QUOTE {Symbol} price={Price} changePct={ChangePct} quotedAt={QuotedAt}",
                        q.symbol, q.price, q.change_pct, q.quoted_at);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching quotes from producer");
            }
        }
    }
}


using System.Diagnostics;
using System.Net.Http.Json;
using Npgsql;
using Worker.Models;
using Worker.Observability;

namespace Worker;

public sealed class Worker : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<Worker> _logger;
    private readonly InMemoryMetrics _metrics;

    private static long _cycleSeq;

    public Worker(
        IHttpClientFactory httpClientFactory,
        ILogger<Worker> logger,
        InMemoryMetrics metrics)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _metrics = metrics;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = int.TryParse(
            Environment.GetEnvironmentVariable("POLL_INTERVAL_SECONDS"),
            out var seconds)
            ? seconds : 10;

        var maxRetries = int.TryParse(
            Environment.GetEnvironmentVariable("MAX_RETRIES"),
            out var mr)
            ? mr : 5;

        var baseDelayMs = int.TryParse(
            Environment.GetEnvironmentVariable("RETRY_BASE_DELAY_MS"),
            out var bd)
            ? bd : 500;

        var threshold = decimal.Parse(
            Environment.GetEnvironmentVariable("PRICE_THRESHOLD") ?? "150"
        );

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

        _logger.LogInformation(
            "processor.worker.start intervalSeconds={intervalSeconds} threshold={threshold} maxRetries={maxRetries}",
            intervalSeconds, threshold, maxRetries
        );

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var cycleId = Guid.NewGuid().ToString("N")[..12];
            using var scope = _logger.BeginCycleScope(cycleId);

            var sw = Stopwatch.StartNew();
            var counters = new CycleCounters
            {
                SymbolsRequested = 1
            };

            _logger.LogInformation("processor.cycle.start");

            var cycleSucceeded = false;

            for (var attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var client = _httpClientFactory.CreateClient("producer");

                    _logger.LogInformation(
                        "processor.http.request attempt={attempt}",
                        attempt
                    );

                    var symbols = Environment.GetEnvironmentVariable("SYMBOLS") ?? "BTCUSD,AAPL,PETR4";


                    var quoteResponse = await client.GetFromJsonAsync<QuoteResponse>(
                         $"/quotes?symbols={symbols}",
                         cancellationToken: stoppingToken
                     );

                    if (quoteResponse is null)
                        throw new InvalidOperationException("Response is null");

                    counters.SymbolsRequested = quoteResponse.Quotes.Count;
                    counters.QuotesFetched = quoteResponse.Quotes.Count;

                    _logger.LogInformation(
                        "processor.http.success fetched={fetched} generatedAt={generatedAt}",
                        quoteResponse.Quotes.Count,
                        quoteResponse.GeneratedAt
                    );

                    // filtra pelo threshold
                    var toPersist = quoteResponse.Quotes.Where(q => q.Price > threshold).ToList();

                    if (toPersist.Count == 0)
                    {
                        _logger.LogInformation(
                            "processor.threshold.skip threshold={threshold} fetched={fetched}",
                            threshold, quoteResponse.Quotes.Count
                        );

                        cycleSucceeded = true;
                        LogCycleEnd(sw, counters);
                        _metrics.Record(cycleId, sw.ElapsedMilliseconds, counters);
                        break;
                    }

                    await PersistAsync(toPersist, stoppingToken, counters);
                    cycleSucceeded = true;
                    LogCycleEnd(sw, counters);
                    _metrics.Record(cycleId, sw.ElapsedMilliseconds, counters);

                    var seq = Interlocked.Increment(ref _cycleSeq);
                    if (seq % 10 == 0)
                    {
                        var snap = _metrics.Snapshot();
                        _logger.LogInformation(
                            "processor.metrics.snapshot cycles={cycles} avgElapsedMs={avgElapsedMs} inserted={inserted} errors={errors}",
                            snap.Cycles,
                            snap.AvgElapsedMs,
                            snap.QuotesInserted,
                            snap.Errors
                        );
                    }

                    break;
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    counters.Errors++;
                    counters.Retries++;

                    var delay = TimeSpan.FromMilliseconds(
                        baseDelayMs * Math.Pow(2, attempt - 1)
                    );

                    _logger.LogWarning(
                        ex,
                        "processor.retry attempt={attempt} delayMs={delayMs}",
                        attempt,
                        (int)delay.TotalMilliseconds
                    );

                    await Task.Delay(delay, stoppingToken);
                }
                catch (Exception ex)
                {
                    counters.Errors++;

                    _logger.LogError(
                        ex,
                        "processor.cycle.error elapsedMs={elapsedMs}",
                        sw.ElapsedMilliseconds
                    );

                    break;
                }
            }

            if (!cycleSucceeded)
            {
                _logger.LogWarning(
                    "processor.cycle.failed elapsedMs={elapsedMs}",
                    sw.ElapsedMilliseconds
                );

                _metrics.Record(cycleId, sw.ElapsedMilliseconds, counters);
            }
        }
    }

    private async Task PersistAsync(
    List<QuoteItem> quotes,
    CancellationToken ct,
    CycleCounters counters)
    {
        var connString = Environment.GetEnvironmentVariable("SUPABASE_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connString))
            throw new InvalidOperationException("SUPABASE_DB_CONNECTION not set");

        await using var conn = new NpgsqlConnection(connString);
        await conn.OpenAsync(ct);

        await using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            await using var cmd = new NpgsqlCommand(@"
            insert into public.asset_quotes(symbol, price, change_pct, quoted_at)
            values (@symbol, @price, @change_pct, @quoted_at);
        ", conn, tx);


            var pSymbol = cmd.Parameters.Add("@symbol", NpgsqlTypes.NpgsqlDbType.Text);
            var pPrice = cmd.Parameters.Add("@price", NpgsqlTypes.NpgsqlDbType.Numeric);
            var pChange = cmd.Parameters.Add("@change_pct", NpgsqlTypes.NpgsqlDbType.Numeric);
            var pQuoted = cmd.Parameters.Add("@quoted_at", NpgsqlTypes.NpgsqlDbType.TimestampTz);

            var inserted = 0;

            foreach (var q in quotes)
            {
                pSymbol.Value = q.Symbol;
                pPrice.Value = q.Price;
                pChange.Value = q.ChangePct;
                pQuoted.Value = q.QuotedAt.UtcDateTime;

                inserted += await cmd.ExecuteNonQueryAsync(ct);
            }

            counters.QuotesInserted += inserted;

            await tx.CommitAsync(ct);

            _logger.LogInformation("processor.db.commit inserted={inserted}", inserted);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            _logger.LogWarning("processor.db.rollback");
            throw;
        }
    }


    private void LogCycleEnd(Stopwatch sw, CycleCounters counters)
    {
        _logger.LogInformation(
            "processor.cycle.end elapsedMs={elapsedMs} fetched={fetched} inserted={inserted} retries={retries} errors={errors}",
            sw.ElapsedMilliseconds,
            counters.QuotesFetched,
            counters.QuotesInserted,
            counters.Retries,
            counters.Errors
        );
    }

    private sealed class PrecoResponse
    {
        public string ativo { get; set; } = default!;
        public decimal preco { get; set; }
    }
}

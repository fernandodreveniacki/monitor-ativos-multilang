using System.Diagnostics;
using System.Net.Http.Json;
using Npgsql;
using Worker.Models;
using Worker.Observability;

namespace Worker;

public class Worker : BackgroundService
{
    // Factory para criar HttpClient configurado (evita socket exhaustion)
    private readonly IHttpClientFactory _httpClientFactory;

    // Logger padrão do .NET para observabilidade
    private readonly ILogger<Worker> _logger;

    private readonly InMemoryMetrics _metrics;

    // Snapshot a cada N ciclos
    private static long _cycleSeq;
    public Worker(IHttpClientFactory httpClientFactory, ILogger<Worker> logger, InMemoryMetrics metrics)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _metrics = metrics;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Lê o intervalo de polling via variável de ambiente (fallback = 5s)
        var intervalSeconds = int.TryParse(
            Environment.GetEnvironmentVariable("POLL_INTERVAL_SECONDS"),
            out var seconds
        ) ? seconds : 5;

        // Lista de ativos a serem consultados (fallback padrão)
        var symbolsCsv = Environment.GetEnvironmentVariable("SYMBOLS") ?? "BTCUSD,AAPL,PETR4";

        // Configuração de retry com exponential backoff
        var maxRetries = int.TryParse(Environment.GetEnvironmentVariable("MAX_RETRIES"), out var mr) ? mr : 5;
        var baseDelayMs = int.TryParse(Environment.GetEnvironmentVariable("RETRY_BASE_DELAY_MS"), out var bd) ? bd : 500;

        // Timer periódico que controla o ciclo de execução
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

        _logger.LogInformation(
            "processor.worker.start intervalSeconds={intervalSeconds} symbols={symbols} maxRetries={maxRetries} baseDelayMs={baseDelayMs}",
            intervalSeconds, symbolsCsv, maxRetries, baseDelayMs
        );

        // Loop principal: roda para sempre (até cancelamento)
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var cycleId = Guid.NewGuid().ToString("N")[..12];
            using var scope = _logger.BeginCycleScope(cycleId);

            var sw = Stopwatch.StartNew();
            var counters = new CycleCounters();

            var symbols = symbolsCsv
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            counters.SymbolsRequested = symbols.Length;

            _logger.LogInformation(
                "processor.cycle.start symbolsRequested={symbolsRequested}",
                counters.SymbolsRequested
            );

            // A cada tick, tenta executar o ciclo com retry/backoff
            var cycleSucceeded = false;

            for (var attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    // Cria cliente HTTP nomeado ("producer")
                    var client = _httpClientFactory.CreateClient("producer");

                    _logger.LogInformation(
                        "processor.http.request attempt={attempt} symbolsCsv={symbolsCsv}",
                        attempt, symbolsCsv
                    );

                    // Chama API do producer buscando as cotações
                    var envelope = await client.GetFromJsonAsync<QuoteEnvelope>(
                        $"/quotes?symbols={symbolsCsv}",
                        cancellationToken: stoppingToken
                    );

                    // Validação 1: resposta totalmente nula
                    if (envelope is null)
                    {
                        _logger.LogWarning(
                            "processor.http.invalid_response reason=envelope_null attempt={attempt}",
                            attempt
                        );
                        throw new InvalidOperationException("Envelope is null");
                    }

                    // Validação 2: lista vazia ou inexistente
                    if (envelope.quotes is null || envelope.quotes.Count == 0)
                    {
                        // Não é erro fatal: ciclo válido, só sem dados.
                        counters.QuotesFetched = 0;

                        _logger.LogInformation(
                            "processor.cycle.end elapsedMs={elapsedMs} {@counters}",
                            sw.ElapsedMilliseconds,
                            counters
                        );

                        _metrics.Record(cycleId, sw.ElapsedMilliseconds, counters);
                        cycleSucceeded = true;
                        break;
                    }

                    counters.QuotesFetched = envelope.quotes.Count;

                    // Recupera string de conexão com banco (Supabase/Postgres)
                    var connString = Environment.GetEnvironmentVariable("SUPABASE_DB_CONNECTION");
                    if (string.IsNullOrWhiteSpace(connString))
                        throw new InvalidOperationException("SUPABASE_DB_CONNECTION not set");

                    // Abre conexão com PostgreSQL
                    await using var conn = new NpgsqlConnection(connString);
                    await conn.OpenAsync(stoppingToken);

                    // Inicia transação para garantir atomicidade
                    await using var tx = await conn.BeginTransactionAsync(stoppingToken);

                    try
                    {
                        // Comando parametrizado (evita SQL Injection)
                        await using var cmd = new NpgsqlCommand(@"
                                    insert into public.asset_quotes(symbol, price, change_pct, quoted_at)
                                    values (@symbol, @price, @change_pct, @quoted_at::timestamptz)
                                    on conflict (symbol, quoted_at) do nothing;
                                ", conn, tx);

                        // Define tipos explícitos para melhor performance e segurança
                        var pSymbol = cmd.Parameters.Add("@symbol", NpgsqlTypes.NpgsqlDbType.Text);
                        var pPrice = cmd.Parameters.Add("@price", NpgsqlTypes.NpgsqlDbType.Numeric);
                        var pChange = cmd.Parameters.Add("@change_pct", NpgsqlTypes.NpgsqlDbType.Numeric);
                        var pQuotedAt = cmd.Parameters.Add("@quoted_at", NpgsqlTypes.NpgsqlDbType.TimestampTz);

                        // Prepara statement no servidor (melhora performance)
                        await cmd.PrepareAsync(stoppingToken);

                        var inserted = 0;
                        var skipped = 0;

                        // Executa insert para cada cotação
                        foreach (var q in envelope.quotes)
                        {
                            pSymbol.Value = q.symbol;
                            pPrice.Value = q.price;
                            pChange.Value = q.change_pct;
                            pQuotedAt.Value = q.quoted_at.UtcDateTime;

                            var rows = await cmd.ExecuteNonQueryAsync(stoppingToken);
                            if (rows == 1) inserted++;
                            else skipped++;
                        }

                        await tx.CommitAsync(stoppingToken);

                        counters.QuotesInserted = inserted;
                        counters.QuotesConflictSkipped = skipped;

                        _logger.LogInformation(
                            "processor.db.commit inserted={inserted} skippedConflict={skipped}",
                            inserted, skipped
                        );

                        _logger.LogInformation(
                            "processor.cycle.end elapsedMs={elapsedMs} {@counters}",
                            sw.ElapsedMilliseconds,
                            counters
                        );

                        _metrics.Record(cycleId, sw.ElapsedMilliseconds, counters);
                        cycleSucceeded = true;

                        // Snapshot a cada 10 ciclos
                        var seq = Interlocked.Increment(ref _cycleSeq);
                        if (seq % 10 == 0)
                        {
                            var snap = _metrics.Snapshot();
                            _logger.LogInformation(
                                "processor.metrics.snapshot cycles={cycles} avgElapsedMs={avgElapsedMs} quotesInserted={quotesInserted} conflicts={conflicts} errors={errors}",
                                snap.Cycles, snap.AvgElapsedMs, snap.QuotesInserted, snap.Conflicts, snap.Errors
                            );
                        }
                    }
                    catch
                    {
                        await tx.RollbackAsync(stoppingToken);
                        _logger.LogWarning("processor.db.rollback");
                        throw;
                    }

                    break; // Sucesso: sai do retry loop
                }
                catch (Exception ex) when (attempt < maxRetries)
                {

                    counters.Errors++;

                    var delay = TimeSpan.FromMilliseconds(baseDelayMs * Math.Pow(2, attempt - 1));
                    var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 200));
                    var totalDelay = delay + jitter;

                    counters.Retries++;

                    _logger.LogWarning(
                         ex,
                        "processor.http.retry attempt={attempt} backoffMs={backoffMs}",
                         attempt, (int)totalDelay.TotalMilliseconds
                     );

                    await Task.Delay(totalDelay, stoppingToken);
                }
                catch (Exception ex)
                {
                    counters.Errors++;

                    // Todas as tentativas falharam
                    _logger.LogError(
                     ex,
                     "processor.cycle.error elapsedMs={elapsedMs} {@counters}",
                     sw.ElapsedMilliseconds, counters
                 );

                    break;
                }
            }

            if (!cycleSucceeded)
            {
                // Mantém o worker vivo mesmo se o ciclo falhar todo
                _logger.LogWarning(
                    "processor.cycle.failed elapsedMs={elapsedMs} {@counters}",
                    sw.ElapsedMilliseconds, counters
                );

                _metrics.Record(cycleId, sw.ElapsedMilliseconds, counters);
            }
        }
    }
}

using System.Net.Http.Json;
using Worker.Models;
using Npgsql;

namespace Worker;

public class Worker : BackgroundService
{
    // Factory para criar HttpClient configurado (evita socket exhaustion)
    private readonly IHttpClientFactory _httpClientFactory;

    // Logger padrão do .NET para observabilidade
    private readonly ILogger<Worker> _logger;

    public Worker(IHttpClientFactory httpClientFactory, ILogger<Worker> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Lê o intervalo de polling via variável de ambiente (fallback = 5s)
        var intervalSeconds = int.TryParse(
            Environment.GetEnvironmentVariable("POLL_INTERVAL_SECONDS"),
            out var seconds
        ) ? seconds : 5;

        // Lista de ativos a serem consultados (fallback padrão)
        var symbols = Environment.GetEnvironmentVariable("SYMBOLS") ?? "BTCUSD,AAPL,PETR4";

        // Timer periódico que controla o ciclo de execução
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

        _logger.LogInformation("Processor Worker started | interval={Interval}s | symbols={Symbols}",
        intervalSeconds, symbols);

        // Configuração de retry com exponential backoff
        var maxRetries = int.TryParse(Environment.GetEnvironmentVariable("MAX_RETRIES"), out var mr) ? mr : 5;
        var baseDelayMs = int.TryParse(Environment.GetEnvironmentVariable("RETRY_BASE_DELAY_MS"), out var bd) ? bd : 500;

        // Loop de tentativas para lidar com falhas temporárias
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                // Cria cliente HTTP nomeado ("producer")
                var client = _httpClientFactory.CreateClient("producer");

                // Chama API do producer buscando as cotações
                var envelope = await client.GetFromJsonAsync<QuoteEnvelope>(
                    $"/quotes?symbols={symbols}",
                    cancellationToken: stoppingToken
                );

                // Validação 1: resposta totalmente nula
                if (envelope is null)
                {
                    _logger.LogWarning("Envelope is null (attempt {Attempt}/{Max})", attempt, maxRetries);
                    throw new InvalidOperationException("Envelope is null");
                }

                // Validação 2: lista vazia ou inexistente
                if (envelope.quotes is null || envelope.quotes.Count == 0)
                {
                    _logger.LogWarning("No quotes received (attempt {Attempt}/{Max})", attempt, maxRetries);
                    break; // nada para salvar
                }

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
                    {
                        // Comando parametrizado (evita SQL Injection)
                        await using var cmd = new NpgsqlCommand(@"
                            insert into public.asset_quotes(symbol, price, change_pct, quoted_at)
                            values (@symbol, @price, @change_pct, @quoted_at::timestamptz);
                        ", conn, tx);

                        // Define tipos explícitos para melhor performance e segurança
                        var pSymbol = cmd.Parameters.Add("@symbol", NpgsqlTypes.NpgsqlDbType.Text);
                        var pPrice = cmd.Parameters.Add("@price", NpgsqlTypes.NpgsqlDbType.Numeric);
                        var pChange = cmd.Parameters.Add("@change_pct", NpgsqlTypes.NpgsqlDbType.Numeric);
                        var pQuotedAt = cmd.Parameters.Add("@quoted_at", NpgsqlTypes.NpgsqlDbType.TimestampTz);

                        // Prepara statement no servidor (melhora performance)
                        await cmd.PrepareAsync(stoppingToken);

                        // Executa insert para cada cotação
                        foreach (var q in envelope.quotes)
                        {
                            pSymbol.Value = q.symbol;
                            pPrice.Value = q.price;
                            pChange.Value = q.change_pct;
                            pQuotedAt.Value = q.quoted_at;

                            await cmd.ExecuteNonQueryAsync(stoppingToken);
                        }
                    }

                    // Confirma transação se tudo ocorreu bem
                    await tx.CommitAsync(stoppingToken);

                    _logger.LogInformation("Saved {Count} quotes", envelope.quotes.Count);
                }
                catch
                {
                    // Em caso de erro, desfaz tudo (consistência)
                    await tx.RollbackAsync(stoppingToken);
                    throw;
                }

                break; // sucesso - sai do loop de retry
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                // Exponential backoff + jitter (evita thundering herd)
                var delay = TimeSpan.FromMilliseconds(baseDelayMs * Math.Pow(2, attempt - 1));
                var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 200));
                var totalDelay = delay + jitter;

                _logger.LogWarning(ex,
                    "Attempt {Attempt}/{Max} failed. Retrying in {Delay}ms",
                    attempt, maxRetries, (int)totalDelay.TotalMilliseconds);

                await Task.Delay(totalDelay, stoppingToken);
            }
            catch (Exception ex)
            {
                // Todas as tentativas falharam
                _logger.LogError(ex, "All retries failed ({Max}). Skipping cycle.", maxRetries);
            }
        }
    }
}

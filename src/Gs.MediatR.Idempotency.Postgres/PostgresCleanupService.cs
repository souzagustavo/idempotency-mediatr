using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

namespace Gs.MediatR.Idempotency.Postgres;

/// <summary>
/// Apaga periodicamente registros expirados (concluídos fora da retenção e reservas abandonadas).
/// PostgreSQL não tem TTL nativo, então esta limpeza substitui o índice TTL do MongoDB.
/// </summary>
internal sealed class PostgresCleanupService : BackgroundService
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly TimeProvider _time;
    private readonly PostgresIdempotencyOptions _options;
    private readonly ILogger<PostgresCleanupService> _logger;
    private readonly string _deleteSql;

    public PostgresCleanupService(
        NpgsqlDataSource dataSource,
        PostgresSqlNames names,
        TimeProvider time,
        IOptions<PostgresIdempotencyOptions> options,
        ILogger<PostgresCleanupService> logger)
    {
        _dataSource = dataSource;
        _time = time;
        _options = options.Value;
        _logger = logger;
        _deleteSql = $"DELETE FROM {names.Table} WHERE expires_at <= @now;";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableCleanup)
        {
            return;
        }

        try
        {
            using var timer = new PeriodicTimer(_options.CleanupInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                try
                {
                    await using var cmd = _dataSource.CreateCommand(_deleteSql);
                    cmd.Parameters.Add(new NpgsqlParameter("now", NpgsqlDbType.TimestampTz)
                    {
                        Value = _time.GetUtcNow().UtcDateTime
                    });

                    var deleted = await cmd.ExecuteNonQueryAsync(stoppingToken).ConfigureAwait(false);
                    if (deleted > 0)
                    {
                        _logger.LogDebug("Idempotency cleanup removed {Count} expired record(s).", deleted);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Falha de limpeza não deve derrubar a aplicação; tenta de novo no próximo ciclo.
                    _logger.LogWarning(ex, "Idempotency cleanup failed.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }
}

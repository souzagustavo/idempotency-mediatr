using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Gs.MediatR.Idempotency.Postgres;

/// <summary>Cria schema, tabela e índice (idempotente) no startup.</summary>
internal sealed class PostgresSchemaInitializer : IHostedService
{
    // Chave arbitrária do advisory lock: serializa a criação do schema entre réplicas subindo ao mesmo tempo.
    private const long AdvisoryLockKey = 7_283_461_001;

    private readonly NpgsqlDataSource _dataSource;
    private readonly PostgresSqlNames _names;
    private readonly PostgresIdempotencyOptions _options;

    public PostgresSchemaInitializer(
        NpgsqlDataSource dataSource,
        PostgresSqlNames names,
        IOptions<PostgresIdempotencyOptions> options)
    {
        _dataSource = dataSource;
        _names = names;
        _options = options.Value;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.AutoCreateSchema)
        {
            return;
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using (var lockCmd = new NpgsqlCommand($"SELECT pg_advisory_xact_lock({AdvisoryLockKey});", connection, transaction))
        {
            await lockCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        var ddl = $@"
CREATE SCHEMA IF NOT EXISTS {_names.Schema};

CREATE TABLE IF NOT EXISTS {_names.Table} (
    scope       text        NOT NULL,
    request_id  text        NOT NULL,
    status      smallint    NOT NULL,           -- 0 = processing, 1 = completed
    created_at  timestamptz NOT NULL,
    expires_at  timestamptz NOT NULL,           -- processing: fim do lease | completed: fim da retenção
    CONSTRAINT {_names.PrimaryKey} PRIMARY KEY (scope, request_id)
);

CREATE INDEX IF NOT EXISTS {_names.ExpiresIndex} ON {_names.Table} (expires_at);";

        await using (var ddlCmd = new NpgsqlCommand(ddl, connection, transaction))
        {
            await ddlCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

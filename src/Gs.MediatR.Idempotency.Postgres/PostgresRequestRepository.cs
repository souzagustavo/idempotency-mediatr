using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

namespace Gs.MediatR.Idempotency.Postgres;

internal sealed class PostgresRequestRepository : IRequestRepository
{
    private const short Processing = 0;
    private const short Completed = 1;

    private readonly NpgsqlDataSource _dataSource;
    private readonly TimeProvider _time;

    private readonly string _isCompletedSql;
    private readonly string _reserveSql;
    private readonly string _statusSql;
    private readonly string _completeSql;
    private readonly string _releaseSql;

    public PostgresRequestRepository(
        NpgsqlDataSource dataSource,
        PostgresSqlNames names,
        TimeProvider time)
    {
        _dataSource = dataSource;
        _time = time;

        var t = names.Table;

        // Registro expirado é tratado como inexistente.
        _isCompletedSql = $@"
SELECT 1 FROM {t}
 WHERE scope = @scope AND request_id = @id AND status = {Completed} AND expires_at > @now;";

        // Reserva ATÔMICA em um único statement:
        //  - não existe          → INSERT                       → retorna 1 linha  (Acquired)
        //  - existe e EXPIRADO   → DO UPDATE (take over)        → retorna 1 linha  (Acquired)
        //  - existe e VÁLIDO     → WHERE falso, nada acontece   → retorna 0 linhas (concluído ou em andamento)
        _reserveSql = $@"
INSERT INTO {t} AS r (scope, request_id, status, created_at, expires_at)
VALUES (@scope, @id, {Processing}, @now, @expires)
ON CONFLICT (scope, request_id) DO UPDATE
   SET status = {Processing}, created_at = @now, expires_at = @expires
 WHERE r.expires_at <= @now
RETURNING 1;";

        _statusSql = $@"
SELECT status FROM {t} WHERE scope = @scope AND request_id = @id;";

        // Upsert idempotente: serve tanto após reserva (Eager) quanto sem ela (Lazy).
        _completeSql = $@"
INSERT INTO {t} AS r (scope, request_id, status, created_at, expires_at)
VALUES (@scope, @id, {Completed}, @now, @expires)
ON CONFLICT (scope, request_id) DO UPDATE
   SET status = {Completed}, expires_at = @expires;";

        // Só libera reservas em andamento; nunca apaga um registro concluído.
        _releaseSql = $@"
DELETE FROM {t} WHERE scope = @scope AND request_id = @id AND status = {Processing};";
    }

    public async Task<bool> IsCompletedAsync(RequestKey key, CancellationToken cancellationToken = default)
    {
        await using var cmd = _dataSource.CreateCommand(_isCompletedSql);
        AddKey(cmd, key);
        AddTime(cmd, "now", Now());

        return await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null;
    }

    public async Task<ReservationResult> TryReserveAsync(
        RequestKey key, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
    {
        // Algumas tentativas cobrem a corrida em que a linha some (cleanup/Release) entre os passos.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var now = Now();

            await using (var reserve = _dataSource.CreateCommand(_reserveSql))
            {
                AddKey(reserve, key);
                AddTime(reserve, "now", now);
                AddTime(reserve, "expires", now.Add(leaseDuration));

                if (await reserve.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null)
                {
                    return ReservationResult.Acquired;
                }
            }

            // Existe e é válido: descobre se está concluído ou em andamento.
            await using var status = _dataSource.CreateCommand(_statusSql);
            AddKey(status, key);

            var value = await status.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (value is null)
            {
                continue; // sumiu no meio do caminho: tenta de novo
            }

            return Convert.ToInt32(value) == Completed
                ? ReservationResult.AlreadyCompleted
                : ReservationResult.InProgress;
        }

        return ReservationResult.InProgress;
    }

    public async Task MarkCompletedAsync(RequestKey key, TimeSpan retention, CancellationToken cancellationToken = default)
    {
        var now = Now();

        await using var cmd = _dataSource.CreateCommand(_completeSql);
        AddKey(cmd, key);
        AddTime(cmd, "now", now);
        AddTime(cmd, "expires", now.Add(retention));

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ReleaseAsync(RequestKey key, CancellationToken cancellationToken = default)
    {
        await using var cmd = _dataSource.CreateCommand(_releaseSql);
        AddKey(cmd, key);

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private DateTime Now() => _time.GetUtcNow().UtcDateTime; // Kind = Utc → timestamptz

    private static void AddKey(NpgsqlCommand cmd, RequestKey key)
    {
        cmd.Parameters.Add(new NpgsqlParameter("scope", NpgsqlDbType.Text) { Value = key.Scope });
        cmd.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Text) { Value = key.Id });
    }

    private static void AddTime(NpgsqlCommand cmd, string name, DateTime utc)
        => cmd.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.TimestampTz) { Value = utc });
}

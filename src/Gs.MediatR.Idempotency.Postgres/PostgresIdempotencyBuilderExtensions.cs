using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Gs.MediatR.Idempotency.Postgres;

public static class PostgresIdempotencyBuilderExtensions
{
    /// <summary>
    /// Usa PostgreSQL como repositório de estado dos requests.
    /// </summary>
    /// <remarks>
    /// Requer um <c>NpgsqlDataSource</c> no container (ex.: Aspire <c>AddNpgsqlDataSource</c>) OU
    /// <see cref="PostgresIdempotencyOptions.ConnectionString"/>.
    /// </remarks>
    public static IdempotencyBuilder UsePostgres(
        this IdempotencyBuilder builder,
        Action<PostgresIdempotencyOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var services = builder.Services;

        var options = services.AddOptions<PostgresIdempotencyOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        options.Validate(
            o => o.CleanupInterval > TimeSpan.Zero,
            "Postgres idempotency: CleanupInterval must be greater than zero.");

        // TryAdd: se o NpgsqlDataSource já foi registrado (Aspire), este fallback é ignorado.
        services.TryAddSingleton<NpgsqlDataSource>(sp =>
        {
            var o = sp.GetRequiredService<IOptions<PostgresIdempotencyOptions>>().Value;
            if (string.IsNullOrWhiteSpace(o.ConnectionString))
            {
                throw new InvalidOperationException(
                    "No NpgsqlDataSource registered and PostgresIdempotencyOptions.ConnectionString is empty. " +
                    "Register an NpgsqlDataSource (e.g. Aspire AddNpgsqlDataSource) or set the connection string.");
            }

            return NpgsqlDataSource.Create(o.ConnectionString);
        });

        services.TryAddSingleton(sp =>
            new PostgresSqlNames(sp.GetRequiredService<IOptions<PostgresIdempotencyOptions>>().Value));

        services.TryAddSingleton<IRequestRepository, PostgresRequestRepository>();

        // Ordem importa: o schema precisa existir antes da limpeza (hosted services iniciam na ordem de registro).
        services.AddHostedService<PostgresSchemaInitializer>();
        services.AddHostedService<PostgresCleanupService>();

        return builder;
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Gs.MediatR.Idempotency.MongoDb;

public static class MongoIdempotencyBuilderExtensions
{
    /// <summary>
    /// Usa MongoDB como repositório de estado dos requests.
    /// </summary>
    /// <remarks>
    /// Requer um <c>IMongoClient</c> no container (ex.: Aspire <c>AddMongoDBClient</c>) OU
    /// <see cref="MongoIdempotencyOptions.ConnectionString"/>.
    /// </remarks>
    public static IdempotencyBuilder UseMongoDb(
        this IdempotencyBuilder builder,
        Action<MongoIdempotencyOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var services = builder.Services;

        var options = services.AddOptions<MongoIdempotencyOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        // TryAdd: se o IMongoClient já foi registrado (Aspire), este fallback é ignorado.
        services.TryAddSingleton<IMongoClient>(sp =>
        {
            var o = sp.GetRequiredService<IOptions<MongoIdempotencyOptions>>().Value;
            if (string.IsNullOrWhiteSpace(o.ConnectionString))
            {
                throw new InvalidOperationException(
                    "No IMongoClient registered and MongoIdempotencyOptions.ConnectionString is empty. " +
                    "Register an IMongoClient (e.g. Aspire AddMongoDBClient) or set the connection string.");
            }

            return new MongoClient(o.ConnectionString);
        });

        services.TryAddSingleton<IRequestRepository, MongoRequestRepository>();
        services.AddHostedService<MongoIndexInitializer>();

        return builder;
    }
}

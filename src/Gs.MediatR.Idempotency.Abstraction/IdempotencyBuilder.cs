using Microsoft.Extensions.DependencyInjection;

namespace Gs.MediatR.Idempotency;

/// <summary>
/// Ponto de extensão para os providers (MongoDb, Postgres...). Cada provider adiciona um método
/// de extensão (<c>UseMongoDb</c>, <c>UsePostgres</c>) sobre este builder.
/// </summary>
public sealed class IdempotencyBuilder
{
    public IdempotencyBuilder(IServiceCollection services)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public IServiceCollection Services { get; }
}

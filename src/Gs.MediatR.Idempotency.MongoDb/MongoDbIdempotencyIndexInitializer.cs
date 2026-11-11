using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Gs.MediatR.Idempotency.MongoDb;

internal sealed class MongoDbIdempotencyIndexInitializer : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly MongoDbIdempotencyOptions _options;

    public MongoDbIdempotencyIndexInitializer(IServiceScopeFactory serviceScopeFactory, IOptions<MongoDbIdempotencyOptions> options)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.CreateIndexOnStartup)
        {
            return;
        }

        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var client = scope.ServiceProvider.GetRequiredService<IMongoClient>();

        var collection = client.GetDatabase(_options.DatabaseName)
            .GetCollection<IdempotencyRequestEntity>(_options.CollectionName);

        var ttl = new CreateIndexModel<IdempotencyRequestEntity>(
            Builders<IdempotencyRequestEntity>.IndexKeys.Ascending(d => d.ExpiresAtUtc),
            new CreateIndexOptions { Name = "ttl_expiresAtUtc", ExpireAfter = TimeSpan.Zero, Background = true });

        await collection.Indexes.CreateOneAsync(ttl, cancellationToken: stoppingToken).ConfigureAwait(false);
    }
}

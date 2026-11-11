using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Gs.MediatR.Idempotency.MongoDb;

/// <summary>Cria o índice TTL em <c>expiresAtUtc</c> no startup (idempotente).</summary>
internal sealed class MongoIndexInitializer : IHostedService
{
    private readonly IMongoClient _client;
    private readonly MongoIdempotencyOptions _options;

    public MongoIndexInitializer(IMongoClient client, IOptions<MongoIdempotencyOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.CreateIndexOnStartup)
        {
            return;
        }

        var collection = _client.GetDatabase(_options.DatabaseName)
            .GetCollection<RequestDocument>(_options.CollectionName);

        // ExpireAfter = 0: o documento expira exatamente na data contida em expiresAtUtc.
        var ttl = new CreateIndexModel<RequestDocument>(
            Builders<RequestDocument>.IndexKeys.Ascending(d => d.ExpiresAtUtc),
            new CreateIndexOptions { Name = "ttl_expiresAtUtc", ExpireAfter = TimeSpan.Zero });

        await collection.Indexes.CreateOneAsync(ttl, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

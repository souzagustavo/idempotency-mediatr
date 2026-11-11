using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Gs.MediatR.Idempotency.MongoDb;

internal sealed class MongoDbRequestRepository : IRequestRepository
{
    private static readonly FilterDefinitionBuilder<IdempotencyRequestEntity> F = Builders<IdempotencyRequestEntity>.Filter;

    private readonly IMongoCollection<IdempotencyRequestEntity> _collection;

    public MongoDbRequestRepository(IMongoClient client, IOptions<MongoDbIdempotencyOptions> options)
    {
        var o = options.Value;
        _collection = client.GetDatabase(o.DatabaseName).GetCollection<IdempotencyRequestEntity>(o.CollectionName);
    }

    public async Task<bool> ExistsAsync(RequestCompositeKey key, CancellationToken cancellationToken = default)
    {
        var filter = F.Eq(d => d.Id, key.ToString());
        return await _collection.Find(filter).Limit(1).AnyAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAsync(RequestCompositeKey key, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        await _collection.InsertOneAsync(
            new IdempotencyRequestEntity
            {
                Id = key.ToString(),
                RequestFullName = key.RequestFullName,
                IdempotencyKey = key.IdempotencyKey,
                ProcessedAtUtc = now,
                ExpiresAtUtc = now.Add(leaseDuration)
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Serializers;

namespace Gs.MediatR.Idempotency.MongoDb;

internal sealed class IdempotencyRequestEntity : IIdempotencyRequestEntity
{
    [BsonId]
    public string Id { get; set; } = default!;

    [BsonElement("requestFullName")]
    public required string RequestFullName { get; set; }

    [BsonElement("idempotencyKey")]
    public required string IdempotencyKey { get; set; }

    [BsonElement("processedAtUtc")]
    [BsonSerializer(typeof(DateTimeOffsetSerializer))]
    public DateTimeOffset ProcessedAtUtc { get; set; }

    [BsonElement("expiresAtUtc")]
    [BsonSerializer(typeof(DateTimeOffsetSerializer))]
    public DateTimeOffset ExpiresAtUtc { get; set; }
}

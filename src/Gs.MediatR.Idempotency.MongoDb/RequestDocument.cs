using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Gs.MediatR.Idempotency.MongoDb;

internal enum RequestStatus
{
    Processing = 0,
    Completed = 1
}

/// <summary>
/// Um documento por (scope, requestId). O _id composto garante unicidade sem índice extra
/// e é o que torna a reserva atômica (insert com chave duplicada = já existe).
/// </summary>
internal sealed class RequestDocument
{
    /// <summary>"{scope}|{requestId}".</summary>
    [BsonId]
    public string Id { get; set; } = default!;

    [BsonElement("scope")]
    public string Scope { get; set; } = default!;

    [BsonElement("requestId")]
    public string RequestId { get; set; } = default!;

    [BsonElement("status")]
    [BsonRepresentation(BsonType.String)]
    public RequestStatus Status { get; set; }

    [BsonElement("createdAtUtc")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Processing → fim do lease. Completed → fim da retenção.
    /// Um único campo serve ao TTL index e à lógica de "registro expirado".
    /// </summary>
    [BsonElement("expiresAtUtc")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime ExpiresAtUtc { get; set; }
}

namespace Gs.MediatR.Idempotency
{
    public interface IIdempotencyRequestEntity
    {
        string Id { get; set; }
        string RequestFullName { get; set; }
        string IdempotencyKey { get; set; }
        DateTimeOffset ProcessedAtUtc { get; set; }
        DateTimeOffset ExpiresAtUtc { get; set; }
    }
}

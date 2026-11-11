namespace Gs.MediatR.Idempotency.MongoDb;

public sealed class MongoDbIdempotencyOptions
{
    public string CollectionName { get; } = "__MediatR_Idempotency_Requests";

    public string? ConnectionString { get; set; }

    public string? DatabaseName { get; set; }

    public bool CreateIndexOnStartup { get; set; } = true;
}

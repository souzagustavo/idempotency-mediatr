namespace Gs.MediatR.Idempotency.MongoDb;

public sealed class MongoIdempotencyOptions
{
    /// <summary>
    /// Opcional. Se informada (e nenhum <c>IMongoClient</c> estiver registrado), um <c>MongoClient</c> é criado.
    /// Com Aspire deixe vazio: <c>builder.AddMongoDBClient("nome")</c> já registra o <c>IMongoClient</c>.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>Nome do database. Padrão: "idempotency".</summary>
    public string DatabaseName { get; set; } = "idempotency";

    /// <summary>Nome da collection. Padrão: "idempotency_requests".</summary>
    public string CollectionName { get; set; } = "idempotency_requests";

    /// <summary>
    /// Cria no startup o índice TTL em <c>expiresAtUtc</c>, que faz o MongoDB apagar registros expirados
    /// (o monitor de TTL roda ~1x/min). Padrão: true.
    /// </summary>
    public bool CreateIndexOnStartup { get; set; } = true;
}

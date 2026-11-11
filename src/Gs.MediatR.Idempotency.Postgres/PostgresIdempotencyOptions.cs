namespace Gs.MediatR.Idempotency.Postgres;

public sealed class PostgresIdempotencyOptions
{
    /// <summary>
    /// Opcional. Se informada (e nenhum <c>NpgsqlDataSource</c> estiver registrado), um data source é criado.
    /// Com Aspire deixe vazio: <c>builder.AddNpgsqlDataSource("nome")</c> já registra o <c>NpgsqlDataSource</c>.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>Schema da tabela. Padrão: "public". Apenas letras, dígitos e "_".</summary>
    public string Schema { get; set; } = "public";

    /// <summary>Nome da tabela. Padrão: "idempotency_requests". Apenas letras, dígitos e "_".</summary>
    public string TableName { get; set; } = "idempotency_requests";

    /// <summary>Cria schema/tabela/índice no startup se não existirem. Padrão: true.</summary>
    public bool AutoCreateSchema { get; set; } = true;

    /// <summary>Remove periodicamente registros expirados. Padrão: true.</summary>
    public bool EnableCleanup { get; set; } = true;

    /// <summary>Intervalo da limpeza. Padrão: 10 minutos.</summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(10);
}

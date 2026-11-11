namespace Gs.MediatR.Idempotency;

/// <summary>
/// (Modo Eager) Lançada quando outro processamento do mesmo request está em andamento.
/// O consumidor deve tratá-la como falha transitória: NACK/retry com atraso, sem descartar a mensagem.
/// </summary>
public sealed class RequestInProgressException : Exception
{
    public RequestInProgressException(RequestKey key)
        : base($"Request '{key}' is already being processed by another consumer.")
    {
        Key = key;
    }

    public RequestKey Key { get; }
}

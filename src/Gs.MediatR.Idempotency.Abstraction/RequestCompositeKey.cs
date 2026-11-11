namespace Gs.MediatR.Idempotency;

/// <summary>
/// Chave composta de um request: (<paramref name="RequestFullName"/>, <paramref name="IdempotencyKey"/>).
/// Permitindo que Handlers diferentes processem a mesma mensagem sem se bloquearem.
/// </summary>
/// <param name="RequestFullName">Nome completo do tipo do request.</param>
/// <param name="IdempotencyKey">Valor de <see cref="IIdempotentRequest.IdempotencyKey"/>.</param>
public readonly record struct RequestCompositeKey(string RequestFullName, string IdempotencyKey)
{
    public override string ToString() => $"{RequestFullName}|{IdempotencyKey}";
}

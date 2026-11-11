namespace Gs.MediatR.Idempotency;

/// <summary>
/// Chave composta de um request: (<paramref name="Scope"/>, <paramref name="Id"/>).
/// O escopo é o tipo do request (um "consumidor"), permitindo que consumidores diferentes
/// processem a mesma mensagem sem se bloquearem.
/// </summary>
/// <param name="Scope">Normalmente o nome completo do tipo do request.</param>
/// <param name="Id">Valor de <see cref="IIdempotentRequest.IdempotencyKey"/>.</param>
public readonly record struct RequestKey(string Scope, string Id)
{
    public override string ToString() => $"{Scope}:{Id}";
}

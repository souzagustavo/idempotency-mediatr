namespace Gs.MediatR.Idempotency;

/// <summary>
/// Marca um request/command MediatR como idempotente.
/// Somente requests que implementam esta interface passam pelo <see cref="IdempotencyBehavior{TRequest,TResponse}"/>.
/// </summary>
public interface IIdempotentRequest
{
    /// <summary>
    /// Identificador ÚNICO e ESTÁVEL da mensagem. Deve ser gerado pelo PRODUTOR e permanecer igual
    /// em todas as reentregas (ex.: header MessageId). Não use delivery tag/offset do broker,
    /// pois eles podem mudar entre entregas.
    /// </summary>
    string IdempotencyKey { get; }
}

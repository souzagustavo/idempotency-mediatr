namespace Gs.MediatR.Idempotency;

/// <summary>
/// Armazena o ESTADO dos requests (em processamento / concluído).
/// Implementações: MongoDB, PostgreSQL, ou qualquer store que ofereça operações atômicas.
/// </summary>
/// <remarks>
/// Todo registro tem uma data de expiração: concluídos expiram após a retenção; "em processamento"
/// expiram após o lease (permitindo recuperar reservas de processos que caíram).
/// </remarks>
public interface IRequestRepository
{
    /// <summary>
    /// 
    /// </summary>
    Task<bool> ExistsAsync(RequestCompositeKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 
    /// </summary>
    Task AddAsync(RequestCompositeKey key, TimeSpan retention, CancellationToken cancellationToken = default);
}

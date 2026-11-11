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
    /// Modo LAZY: o request já foi concluído (e o registro ainda não expirou)?
    /// </summary>
    Task<bool> IsCompletedAsync(RequestKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Modo EAGER: tenta reservar o request ATOMICAMENTE (marca como "em processamento" por <paramref name="leaseDuration"/>).
    /// Também deve assumir (take over) registros cujo prazo expirou.
    /// </summary>
    Task<ReservationResult> TryReserveAsync(RequestKey key, TimeSpan leaseDuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca o request como concluído por <paramref name="retention"/>. Deve ser um upsert idempotente
    /// (funciona com ou sem reserva prévia).
    /// </summary>
    Task MarkCompletedAsync(RequestKey key, TimeSpan retention, CancellationToken cancellationToken = default);

    /// <summary>
    /// Modo EAGER: libera a reserva (remove o registro "em processamento") quando o handler falha,
    /// permitindo que a reentrega do broker reprocesse. Não deve remover registros concluídos.
    /// </summary>
    Task ReleaseAsync(RequestKey key, CancellationToken cancellationToken = default);
}

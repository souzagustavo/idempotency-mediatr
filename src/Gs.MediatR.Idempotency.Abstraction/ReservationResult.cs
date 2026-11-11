namespace Gs.MediatR.Idempotency;

/// <summary>Resultado de <see cref="IRequestRepository.TryReserveAsync"/>.</summary>
public enum ReservationResult
{
    /// <summary>Reserva obtida: este chamador deve processar o request.</summary>
    Acquired = 0,

    /// <summary>O request já foi concluído anteriormente: é uma duplicata.</summary>
    AlreadyCompleted = 1,

    /// <summary>Outro processamento do mesmo request está em andamento (lease ainda válido).</summary>
    InProgress = 2
}

using MediatR;

namespace Gs.MediatR.Idempotency.Samples.Common;

/// <summary>
/// Command MediatR disparado pelo consumidor do broker.
/// Implementa <see cref="IIdempotentRequest"/>: é isso que "liga" o IdempotencyBehavior para ele.
/// </summary>
/// <param name="MessageId">Id da mensagem gerado pelo PRODUTOR (igual em todas as reentregas).</param>
/// <param name="OrderId">Id de negócio (não serve como chave de idempotência da mensagem).</param>
/// <param name="Amount">Valor do pagamento.</param>
/// <param name="SimulateTransientFailure">Se true, a 1ª tentativa do handler falha (para demonstrar o retry).</param>
public sealed record ProcessPaymentRequest(
    string MessageId,
    Guid OrderId,
    decimal Amount,
    bool SimulateTransientFailure = false)
    : IRequest, IIdempotentRequest
{
    // Implementação explícita: não "polui" a API pública do command.
    string IIdempotentRequest.IdempotencyKey => MessageId;
}

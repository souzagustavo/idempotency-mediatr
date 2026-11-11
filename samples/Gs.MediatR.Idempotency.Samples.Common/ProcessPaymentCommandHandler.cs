using MediatR;
using Microsoft.Extensions.Logging;

namespace Gs.MediatR.Idempotency.Samples.Common;

public sealed class ProcessPaymentCommandHandler(
    PaymentStats stats,
    ILogger<ProcessPaymentCommandHandler> logger) : IRequestHandler<ProcessPaymentRequest>
{
    public Task Handle(ProcessPaymentRequest command, CancellationToken cancellationToken)
    {
        var attempt = stats.RegisterHandlerAttempt(command.MessageId);

        if (command.SimulateTransientFailure && attempt == 1)
        {
            // Falha ANTES do efeito colateral: o behavior não grava "concluído" (Lazy)
            // ou libera a reserva (Eager), e a reentrega reprocessa com sucesso.
            throw new InvalidOperationException("Simulated transient failure (first attempt).");
        }

        stats.RegisterSideEffect(command.OrderId);

        logger.LogInformation(
            "SIDE EFFECT executed: payment of {Amount} for order {OrderId} (message {MessageId}, handler attempt {Attempt}).",
            command.Amount, command.OrderId, command.MessageId, attempt);

        return Task.CompletedTask;
    }
}

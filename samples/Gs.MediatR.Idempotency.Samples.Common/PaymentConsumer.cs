using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Gs.MediatR.Idempotency.Samples.Common;

/// <summary>
/// Consumidor do broker. É só um ADAPTADOR: converte a mensagem em command e despacha via MediatR.
/// Toda a idempotência acontece no pipeline (IdempotencyBehavior), não aqui.
/// </summary>
public sealed class PaymentConsumer(
    InMemoryBroker broker,
    IServiceScopeFactory scopeFactory,
    PaymentStats stats,
    ILogger<PaymentConsumer> logger) : BackgroundService
{
    private const int MaxConcurrency = 8;      // várias mensagens ao mesmo tempo (como um consumidor real)
    private const int MaxDeliveryAttempts = 5; // depois disso a mensagem iria para a DLQ

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var gate = new SemaphoreSlim(MaxConcurrency);

        try
        {
            await foreach (var message in broker.ConsumeAsync(stoppingToken))
            {
                await gate.WaitAsync(stoppingToken);

                _ = Task.Run(async () =>
                {
                    try { await ProcessAsync(message, stoppingToken); }
                    finally { gate.Release(); }
                }, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }

    private async Task ProcessAsync(BrokerMessage message, CancellationToken ct)
    {
        stats.RegisterDelivery();

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();

            await sender.Send(
                new ProcessPaymentCommand(message.MessageId, message.OrderId, message.Amount, message.SimulateTransientFailure),
                ct);

            // Chegou aqui sem exceção → ACK. Inclui duplicatas: o behavior as ignora com sucesso,
            // então o broker para de reentregar.
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // shutdown
        }
        catch (Exception ex)
        {
            // Exceção → NACK: o broker reentrega depois. Inclui RequestInProgressException (modo Eager).
            if (message.DeliveryAttempt >= MaxDeliveryAttempts)
            {
                logger.LogError(ex, "Message {MessageId} exhausted {Attempts} attempts → dead-letter.", message.MessageId, MaxDeliveryAttempts);
                return;
            }

            logger.LogWarning("Message {MessageId} failed ({Reason}); redelivering (attempt {Next}).",
                message.MessageId, ex.Message, message.DeliveryAttempt + 1);

            _ = RedeliverAsync(message with { DeliveryAttempt = message.DeliveryAttempt + 1 }, ct);
        }
    }

    private async Task RedeliverAsync(BrokerMessage message, CancellationToken ct)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
            await broker.PublishAsync(message, ct);
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }
}

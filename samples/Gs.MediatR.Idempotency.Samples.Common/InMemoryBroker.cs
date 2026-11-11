using System.Threading.Channels;

namespace Gs.MediatR.Idempotency.Samples.Common;

/// <summary>Mensagem como chegaria do broker.</summary>
public sealed record BrokerMessage(
    string MessageId,
    Guid OrderId,
    decimal Amount,
    bool SimulateTransientFailure,
    int DeliveryAttempt = 1);

/// <summary>
/// "Broker" em memória só para o sample (substitui RabbitMQ/Kafka/SQS...).
/// Entrega at-least-once: o mesmo MessageId pode ser publicado/entregue várias vezes.
/// </summary>
public sealed class InMemoryBroker
{
    private readonly Channel<BrokerMessage> _channel = Channel.CreateUnbounded<BrokerMessage>();

    public ValueTask PublishAsync(BrokerMessage message, CancellationToken cancellationToken = default)
        => _channel.Writer.WriteAsync(message, cancellationToken);

    public IAsyncEnumerable<BrokerMessage> ConsumeAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}

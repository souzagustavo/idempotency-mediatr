using System.Collections.Concurrent;

namespace Gs.MediatR.Idempotency.Samples.Common;

public sealed record PaymentStatsSnapshot(
    int Published,
    int Deliveries,
    int HandlerAttempts,
    int DistinctOrdersProcessed,
    IReadOnlyDictionary<Guid, int> SideEffectsPerOrder);

/// <summary>Contadores em memória para observar o efeito da idempotência.</summary>
public sealed class PaymentStats
{
    private readonly ConcurrentDictionary<string, int> _attemptsPerMessage = new();
    private readonly ConcurrentDictionary<Guid, int> _sideEffectsPerOrder = new();
    private int _published;
    private int _deliveries;

    public void RegisterPublished() => Interlocked.Increment(ref _published);

    public void RegisterDelivery() => Interlocked.Increment(ref _deliveries);

    /// <summary>Quantas vezes o HANDLER (não o broker) foi chamado para esta mensagem (1 = primeira).</summary>
    public int RegisterHandlerAttempt(string messageId) =>
        _attemptsPerMessage.AddOrUpdate(messageId, 1, (_, current) => current + 1);

    public void RegisterSideEffect(Guid orderId) =>
        _sideEffectsPerOrder.AddOrUpdate(orderId, 1, (_, current) => current + 1);

    public PaymentStatsSnapshot Snapshot() => new(
        Published: Volatile.Read(ref _published),
        Deliveries: Volatile.Read(ref _deliveries),
        HandlerAttempts: _attemptsPerMessage.Values.Sum(),
        DistinctOrdersProcessed: _sideEffectsPerOrder.Count,
        SideEffectsPerOrder: _sideEffectsPerOrder.ToDictionary(kv => kv.Key, kv => kv.Value));
}

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Gs.MediatR.Idempotency.Samples.Common;

public static class SampleServiceCollectionExtensions
{
    /// <summary>
    /// Registra MediatR + IdempotencyBehavior + o "broker" em memória + o consumidor.
    /// O repositório (Mongo/Postgres) é registrado separadamente, em cada sample, via AddIdempotency().UseXxx().
    /// </summary>
    public static IServiceCollection AddPaymentSample(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(ProcessPaymentCommand).Assembly);

            // A ordem de registro = ordem de execução. Aqui só há este behavior; num projeto real
            // (logging, validação, transação...) registre a idempotência DEPOIS da transação.
            cfg.AddIdempotencyBehavior();
        });

        services.AddSingleton<InMemoryBroker>();
        services.AddSingleton<PaymentStats>();
        services.AddHostedService<PaymentConsumer>();

        return services;
    }
}

public static class SampleEndpoints
{
    public static IEndpointRouteBuilder MapSampleEndpoints(this IEndpointRouteBuilder app, string providerName)
    {
        app.MapGet("/", (IOptions<IdempotencyOptions> options) => Results.Ok(new
        {
            provider = providerName,
            mode = options.Value.Mode.ToString(),
            howToTest = new[]
            {
                "POST /payments                        → 1 mensagem entregue 2x (duplicata 500ms depois)",
                "POST /payments?duplicates=5&delayMs=0 → 5 entregas simultâneas (compare Lazy x Eager!)",
                "POST /payments?simulateFailure=true   → 1ª tentativa do handler falha e a reentrega funciona",
                "GET  /stats                           → deliveries x handlerAttempts x sideEffectsPerOrder"
            }
        }));

        // Publica UMA mensagem (mesmo MessageId) `duplicates` vezes, simulando reentregas do broker.
        app.MapPost("/payments", async (
            InMemoryBroker broker,
            PaymentStats stats,
            int? duplicates,
            int? delayMs,
            bool? simulateFailure) =>
        {
            var deliveries = Math.Clamp(duplicates ?? 2, 1, 50);
            var delay = TimeSpan.FromMilliseconds(Math.Clamp(delayMs ?? 500, 0, 10_000));

            var message = new BrokerMessage(
                MessageId: Guid.NewGuid().ToString("N"),   // gerado pelo "produtor", estável nas reentregas
                OrderId: Guid.NewGuid(),
                Amount: 99.90m,
                SimulateTransientFailure: simulateFailure ?? false);

            stats.RegisterPublished();
            await broker.PublishAsync(message);

            // Demais entregas (duplicatas). Com delayMs=0 elas chegam em paralelo e expõem a diferença Lazy x Eager.
            _ = Task.Run(async () =>
            {
                for (var i = 1; i < deliveries; i++)
                {
                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay);
                    }

                    await broker.PublishAsync(message);
                }
            });

            return Results.Accepted(value: new
            {
                message.MessageId,
                message.OrderId,
                deliveries,
                delayMs = delay.TotalMilliseconds
            });
        });

        app.MapGet("/stats", (PaymentStats stats) => Results.Ok(stats.Snapshot()));

        return app;
    }
}

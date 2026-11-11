using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Gs.MediatR.Idempotency;

public static class IdempotencyServiceCollectionExtensions
{
    /// <summary>
    /// Registra as opções de idempotência. Em seguida escolha um provider:
    /// <c>.UseMongoDb(...)</c> ou <c>.UsePostgres(...)</c>.
    /// </summary>
    public static IdempotencyBuilder AddIdempotency(
        this IServiceCollection services,
        Action<IdempotencyOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = services.AddOptions<IdempotencyOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        options.Validate(
            o => o.Retention > TimeSpan.Zero && o.ProcessingLease > TimeSpan.Zero,
            "Idempotency: Retention and ProcessingLease must be greater than zero.");

        services.TryAddSingleton(TimeProvider.System);

        return new IdempotencyBuilder(services);
    }
}

public static class IdempotencyMediatRConfigurationExtensions
{
    /// <summary>
    /// Adiciona o <see cref="IdempotencyBehavior{TRequest,TResponse}"/> ao pipeline do MediatR.
    /// </summary>
    /// <remarks>
    /// A ORDEM DE REGISTRO define a ordem de execução (primeiro registrado = mais externo).
    /// Se você usa um behavior de transação, registre este DEPOIS dele para que (com um repositório
    /// que participe da mesma transação) o registro de conclusão e o efeito do handler façam commit juntos.
    /// </remarks>
    public static MediatRServiceConfiguration AddIdempotencyBehavior(this MediatRServiceConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        configuration.AddOpenBehavior(typeof(IdempotencyBehavior<,>));
        return configuration;
    }
}

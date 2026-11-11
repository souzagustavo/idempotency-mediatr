using Microsoft.Extensions.DependencyInjection;

namespace Gs.MediatR.Idempotency;

public static class IdempotencyServiceCollectionExtensions
{
    /// <summary>
    /// Registra as opções de idempotência. Em seguida escolha um provider:
    /// <c>.UseMongoDb(...)</c> ou <c>.UsePostgres(...)</c>.
    /// </summary>
    public static IdempotencyBuilder AddMediatRIdempotency(
        this IServiceCollection services,
        Action<IdempotencyMediatROptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = services.AddOptions<IdempotencyMediatROptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        options.Validate(
            o => o.RetentionInDays > 0 && o.LockTimeOutInSeconds > 0,
            "Idempotency: RetentionInDays and LockTimeOutInSeconds must be greater than zero.");

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

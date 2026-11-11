using Medallion.Threading;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gs.MediatR.Idempotency;

/// <summary>
/// Pipeline behavior que impede que o mesmo request seja executado mais de uma vez. 
/// Só é aplicado a requests que implementam <see cref="IIdempotentRequest"/>
/// </summary>
/// <remarks>
/// Em caso de duplicata o behavior devolve <c>default</c> de <typeparamref name="TResponse"/> e NÃO chama o handler.
/// Para commands sem retorno (<c>IRequest</c> → <c>Unit</c>) isso é natural. Se o seu command retorna dados,
/// o chamador precisa tolerar <c>default</c> (ou persistir/recuperar o resultado original por conta própria).
/// </remarks>
public sealed class IdempotencyBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull, IIdempotentRequest
{
    private readonly IRequestRepository _repository;
    private readonly IdempotencyMediatROptions _options;
    private readonly ILogger<IdempotencyBehavior<TRequest, TResponse>> _logger;
    private readonly IDistributedLockProvider _distributedLockProvider;

    public IdempotencyBehavior(
        IRequestRepository repository,
        IDistributedLockProvider providerLock,
        IOptions<IdempotencyMediatROptions> options,
        ILogger<IdempotencyBehavior<TRequest, TResponse>> logger)
    {
        _repository = repository;
        _distributedLockProvider = providerLock;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            throw new InvalidOperationException(
                $"{typeof(TRequest).Name}.{nameof(IIdempotentRequest.IdempotencyKey)} must not be null or empty.");
        }

        var key = new RequestCompositeKey(typeof(TRequest).FullName ?? typeof(TRequest).Name, request.IdempotencyKey);

        _logger.LogInformation("Starting IdempotencyBehavior for request of key: {key}", key);

        var @lock = _distributedLockProvider.CreateLock(key.ToString());

        _logger.LogInformation("Trying acquire lock for request of key: {key}, timeout is {seconds} seconds.", key, _options.LockTimeOutInSeconds);

        await using var handler = await @lock.TryAcquireAsync(timeout: _options.GetLockTimeOut(), cancellationToken: cancellationToken);

        TResponse response;

        if (handler is null)
            throw new InvalidOperationException(
                $"Could not acquire distributed lock for request {key}. " +
                $"The lock timeout is {_options.LockTimeOutInSeconds} seconds.");

        _logger.LogInformation("Lock acquired for request of key: {key}", key);

        try
        {
            if (await _repository.ExistsAsync(key, cancellationToken).ConfigureAwait(false))
            {
                _logger.LogInformation("Duplicate request of key: {key} ignored (already completed).", key);
                return default!; // adicionar sucesso
            }

            _logger.LogInformation("The request of key {key} has not yet been processed.", key);

            response = await next(cancellationToken).ConfigureAwait(false);

            await _repository.AddAsync(key, _options.GetRetentionTime(), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await handler.DisposeAsync().ConfigureAwait(false);
        }

        _logger.LogInformation("Finished IdempotencyBehavior for request of key: {key}", key);

        return response;
    }
}

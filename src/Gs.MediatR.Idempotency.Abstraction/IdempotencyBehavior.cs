using Medallion.Threading;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gs.MediatR.Idempotency;

/// <summary>
/// Pipeline behavior que impede que o mesmo request (mesmo <see cref="IIdempotentRequest.IdempotencyKey"/>)
/// seja executado mais de uma vez. Só é aplicado a requests que implementam <see cref="IIdempotentRequest"/>
/// (a constraint genérica faz o container de DI ignorar os demais).
/// </summary>
/// <remarks>
/// Em caso de duplicata o behavior devolve <c>default</c> de <typeparamref name="TResponse"/> e NÃO chama o handler.
/// Para commands sem retorno (<c>IRequest</c> → <c>Unit</c>) isso é natural. Se o seu command retorna dados,
/// o chamador precisa tolerar <c>default</c> (ou persistir/recuperar o resultado original por conta própria).
///
/// IMPORTANTE (atomicidade): o repositório grava o estado em uma conexão própria. Se o efeito colateral do handler
/// e o registro de conclusão não estiverem na mesma transação, existe uma janela em que o handler conclui e o
/// processo cai antes de gravar a conclusão → a mensagem será reprocessada (at-least-once).
/// Para efeitos externos, repasse <see cref="IIdempotentRequest.IdempotencyKey"/> como chave de idempotência ao destino.
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

        _logger.LogInformation("Try acquire lock for request of key: {key}", key);

        await using var handler = await @lock.TryAcquireAsync(timeout: _options.GetLockTimeOut(), cancellationToken: cancellationToken);

        if (handler is null)        
            throw new InvalidOperationException(
                $"Could not acquire distributed lock for request {key}. " +
                $"The lock timeout is {_options.LockTimeOutInSeconds} seconds.");

        _logger.LogInformation("Lock acquired for request of key: {key}", key);

        TResponse response;

        try
        {
            if (await _repository.ExistsAsync(key, cancellationToken).ConfigureAwait(false))
            {
                _logger.LogInformation("Duplicate request of key: {key} ignored (already completed).", key);
                return default!; // adicionar sucesso
            }
            
            response = await next(cancellationToken).ConfigureAwait(false);

            await _repository.AddAsync(key, _options.GetRetentionTime(), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await handler.DisposeAsync().ConfigureAwait(false);
        }

        return response;
    }
}

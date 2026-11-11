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
    private readonly IdempotencyOptions _options;
    private readonly ILogger<IdempotencyBehavior<TRequest, TResponse>> _logger;

    public IdempotencyBehavior(
        IRequestRepository repository,
        IOptions<IdempotencyOptions> options,
        ILogger<IdempotencyBehavior<TRequest, TResponse>> logger)
    {
        _repository = repository;
        _options = options.Value;
        _logger = logger;
    }

    public Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            throw new InvalidOperationException(
                $"{typeof(TRequest).Name}.{nameof(IIdempotentRequest.IdempotencyKey)} must not be null or empty.");
        }

        // Escopo = tipo do request. Um request tem um único handler no MediatR, então equivale a "um consumidor".
        var key = new RequestKey(typeof(TRequest).FullName ?? typeof(TRequest).Name, request.IdempotencyKey);

        return _options.Mode == IdempotencyMode.Eager
            ? HandleEagerAsync(key, next, cancellationToken)
            : HandleLazyAsync(key, next, cancellationToken);
    }

    // ---------------------------------------------------------------------------------------------
    // LAZY:  já concluído? → (sim) ignora | (não) executa handler → grava como concluído
    // ---------------------------------------------------------------------------------------------
    private async Task<TResponse> HandleLazyAsync(
        RequestKey key,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        if (await _repository.IsCompletedAsync(key, ct).ConfigureAwait(false))
        {
            _logger.LogInformation("Duplicate request {RequestKey} ignored (already completed).", key);
            return default!;
        }

        // Se o handler lançar exceção, nada foi gravado e a reentrega do broker poderá reprocessar.
        var response = await next().ConfigureAwait(false);

        await _repository.MarkCompletedAsync(key, _options.Retention, ct).ConfigureAwait(false);
        return response;
    }

    // ---------------------------------------------------------------------------------------------
    // EAGER: reserva atômica → executa handler → grava como concluído | (falha) libera a reserva
    // ---------------------------------------------------------------------------------------------
    private async Task<TResponse> HandleEagerAsync(
        RequestKey key,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        var reservation = await _repository.TryReserveAsync(key, _options.ProcessingLease, ct).ConfigureAwait(false);

        switch (reservation)
        {
            case ReservationResult.AlreadyCompleted:
                _logger.LogInformation("Duplicate request {RequestKey} ignored (already completed).", key);
                return default!;

            case ReservationResult.InProgress:
                _logger.LogWarning("Request {RequestKey} is being processed elsewhere; signalling retry.", key);
                throw new RequestInProgressException(key);
        }

        TResponse response;
        try
        {
            response = await next().ConfigureAwait(false);
        }
        catch
        {
            // Handler falhou: libera a reserva para que a reentrega reprocesse.
            // CancellationToken.None: queremos liberar mesmo que o token original tenha sido cancelado.
            await _repository.ReleaseAsync(key, CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        // Fora do try/catch de propósito: se gravar a conclusão falhar NÃO devemos liberar a reserva
        // (o handler já produziu efeitos). O lease expira sozinho e, até lá, reentregas recebem InProgress.
        await _repository.MarkCompletedAsync(key, _options.Retention, ct).ConfigureAwait(false);
        return response;
    }
}

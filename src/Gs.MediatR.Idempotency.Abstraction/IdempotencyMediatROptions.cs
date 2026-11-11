namespace Gs.MediatR.Idempotency;

public sealed class IdempotencyMediatROptions
{
    /// <summary>
    /// Por quanto tempo lembrar de um request concluído. Deve ser MAIOR que a janela máxima de reentrega
    /// do broker. Padrão: 7 dias.
    /// </summary>
    public int RetentionInDays { get; set; } = 7;

    /// <summary>
    /// Tempo de espera para adquirir o lock distribuído. 
    /// Deve ser menor que o tempo máximo do handler. Padrão: 30 segundos.
    /// </summary>
    public int LockTimeOutInSeconds { get; set; } = 30;

    public TimeSpan GetRetentionTime() => TimeSpan.FromDays(RetentionInDays);
    public TimeSpan GetLockTimeOut() => TimeSpan.FromSeconds(LockTimeOutInSeconds);
}

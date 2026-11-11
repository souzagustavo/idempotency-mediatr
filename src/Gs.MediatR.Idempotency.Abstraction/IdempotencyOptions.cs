namespace Gs.MediatR.Idempotency;

/// <summary>Estratégia do <see cref="IdempotencyBehavior{TRequest,TResponse}"/>.</summary>
public enum IdempotencyMode
{
    /// <summary>
    /// Verifica → executa o handler → grava como concluído (padrão do artigo do Milan Jovanović).
    /// Se o handler falhar nada é gravado e a reentrega reprocessa.
    /// Limitação: duas entregas SIMULTÂNEAS da mesma mensagem podem executar o handler ao mesmo tempo.
    /// </summary>
    Lazy = 0,

    /// <summary>
    /// Reserva atômica → executa o handler → grava como concluído; se o handler falhar, libera a reserva.
    /// Protege contra entregas simultâneas. Se houver outro processamento em andamento lança
    /// <see cref="RequestInProgressException"/> (o consumidor deve dar NACK para reentrega posterior).
    /// </summary>
    Eager = 1
}

public sealed class IdempotencyOptions
{
    /// <summary>Estratégia utilizada. Padrão: <see cref="IdempotencyMode.Lazy"/>.</summary>
    public IdempotencyMode Mode { get; set; } = IdempotencyMode.Lazy;

    /// <summary>
    /// Por quanto tempo lembrar de um request concluído. Deve ser MAIOR que a janela máxima de reentrega
    /// do broker. Padrão: 7 dias.
    /// </summary>
    public TimeSpan Retention { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// (Eager) Tempo máximo de uma reserva "em processamento" antes de ser considerada abandonada.
    /// Deve ser maior que o tempo máximo do handler. Padrão: 5 minutos.
    /// </summary>
    public TimeSpan ProcessingLease { get; set; } = TimeSpan.FromMinutes(5);
}

namespace Norn.Executor.CircuitBreaker;

/// <summary>
/// ADR-04, barreira (c) — contador de falhas consecutivas em memória do processo. Singleton por
/// design: a contagem é por instância viva do Worker, não persistida (ADR-04: "o Norn pode
/// desistir. Esse comportamento é intencional e deve ser medido" — reiniciar o processo reabre o
/// circuito de propósito, e a campanha reseta estado entre execuções de qualquer forma, Fase 12
/// tarefa 2a). Sucesso zera o contador; abertura do circuito também zera, para que um operador que
/// volte o modo para <c>Active</c> receba um novo orçamento de 5 tentativas, não um circuito que
/// reabre no primeiro tick seguinte.
/// </summary>
public sealed class CircuitBreakerState
{
    private int consecutiveFailures;

    /// <returns><c>true</c> se este incremento abriu o circuito.</returns>
    /// <remarks>
    /// CAS em laço, não Increment seguido de comparação: sob falhas concorrentes, duas chamadas
    /// incrementando para valores consecutivos (ex.: 5 e 6 com threshold=5) poderiam as duas ver
    /// "cruzei o limiar" e as duas retornar <c>true</c> — dobrando a contagem de "circuito abriu"
    /// que alimenta a barreira (c) do ADR-04. O CAS garante que só quem de fato transiciona o
    /// contador reporta abertura.
    /// </remarks>
    public bool RecordFailure(int threshold)
    {
        while (true)
        {
            var current = Volatile.Read(ref consecutiveFailures);
            var next = current + 1;
            var tripped = next >= threshold;
            var newValue = tripped ? 0 : next;

            if (Interlocked.CompareExchange(ref consecutiveFailures, newValue, current) == current)
            {
                return tripped;
            }
        }
    }

    public void RecordSuccess() => Interlocked.Exchange(ref consecutiveFailures, 0);

    public int ConsecutiveFailures => Volatile.Read(ref consecutiveFailures);
}

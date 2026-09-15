using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace Norn.BuildingBlocks.Chaos.Effects;

/// <summary>
/// F1 — retém blocos de memória proporcionalmente à intensidade, sem nunca liberá-los enquanto a
/// ativação estiver de pé: é o "vazamento" que o cenário simula. <see cref="Reset"/> descarta tudo
/// — a limpeza real do vazamento, no experimento, é o <c>RestartPod</c> matando o processo.
/// </summary>
internal sealed class MemoryRetentionEffect(IOptions<ChaosScenarioOptions> options) : ChaosEffectBase
{
    private const int ChunkSize = 1_000_000;
    private readonly ConcurrentBag<byte[]> _retained = [];
    private long _retainedBytes;

    public override string ScenarioId => ChaosScenarioIds.F1;

    public override ValueTask TickAsync(ChaosActivation activation, double intensity, CancellationToken cancellationToken)
    {
        var targetBytes = (long)(intensity * options.Value.F1MaxRetainedBytes);

        while (Interlocked.Read(ref _retainedBytes) < targetBytes)
        {
            var chunk = new byte[ChunkSize];
            // Toca cada página para forçar o commit físico — um array alocado e nunca lido pode
            // ficar só reservado em memória virtual, e o RSS (o sinal do F1) não subiria de verdade.
            for (var i = 0; i < chunk.Length; i += 4096)
            {
                chunk[i] = 1;
            }

            _retained.Add(chunk);
            Interlocked.Add(ref _retainedBytes, chunk.Length);
        }

        return ValueTask.CompletedTask;
    }

    public override void Reset()
    {
        _retained.Clear();
        Interlocked.Exchange(ref _retainedBytes, 0);
    }
}

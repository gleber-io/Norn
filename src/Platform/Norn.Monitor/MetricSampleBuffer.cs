using System.Collections.Concurrent;
using Norn.Contracts;

namespace Norn.Monitor;

/// <summary>Buffer de série por (serviço × métrica) (tarefa 2) — retenção limitada a <see cref="MonitorOptions.BufferRetention"/>.</summary>
public interface IMetricSampleBuffer
{
    void Append(MetricSample sample);

    /// <summary>Amostras não drenadas ainda por nenhum consumidor, em ordem de chegada; drena o buffer ao ler.</summary>
    IReadOnlyList<MetricSample> DrainNew();
}

internal sealed class MetricSampleBuffer : IMetricSampleBuffer
{
    private readonly ConcurrentQueue<MetricSample> pending = new();

    public void Append(MetricSample sample) => pending.Enqueue(sample);

    public IReadOnlyList<MetricSample> DrainNew()
    {
        var drained = new List<MetricSample>();
        while (pending.TryDequeue(out var sample))
        {
            drained.Add(sample);
        }

        return drained;
    }
}

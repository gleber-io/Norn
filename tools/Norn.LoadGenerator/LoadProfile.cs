namespace Norn.LoadGenerator;

/// <summary>
/// Perfil de carga senoidal (§8, Fase 5, tarefa 4) — função pura de taxa em função do tempo
/// decorrido, mesmo padrão de <c>IChaosScenario</c> (ADR-13/ADR-18): sem I/O, sem estado. O "dia"
/// comprimido dura <see cref="DayDuration"/>; a fase é sempre a mesma a partir de t=0, o que faz a
/// sazonalidade virar constante controlada entre execuções, contanto que a ativação do caos seja
/// cronometrada a partir do mesmo instante de início do gerador (§3, nota sobre o warmup).
/// </summary>
public sealed class LoadProfile(double baseRequestsPerSecond, double peakRequestsPerSecond, TimeSpan dayDuration)
{
    public TimeSpan DayDuration { get; } = dayDuration;

    /// <summary>Taxa alvo, em requisições/segundo, no instante <paramref name="elapsed"/> desde o início do gerador.</summary>
    public double RequestsPerSecond(TimeSpan elapsed)
    {
        var phase = elapsed.TotalSeconds % DayDuration.TotalSeconds / DayDuration.TotalSeconds;
        // Começa no vale (fase 0 = ponto mais baixo do ciclo) e sobe suavemente — sin deslocado de -pi/2.
        var wave = 0.5 + 0.5 * Math.Sin(2 * Math.PI * phase - Math.PI / 2);
        return baseRequestsPerSecond + (peakRequestsPerSecond - baseRequestsPerSecond) * wave;
    }
}

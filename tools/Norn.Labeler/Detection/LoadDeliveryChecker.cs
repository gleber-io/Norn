namespace Norn.Labeler.Detection;

/// <summary>
/// §3, "Carga entregue é dado, não pressuposto": execução fora de ±10% do alvo é
/// <c>InvalidInstrumentation</c> — contenção de CPU do host (gerador disputa núcleos com o WSL2)
/// vira variância inexplicada se não for descartada, e atinge o F2 em cheio.
/// </summary>
public static class LoadDeliveryChecker
{
    public const double AllowedDeviation = 0.10;

    public static bool IsWithinTarget(double targetRps, double achievedRps)
    {
        if (targetRps <= 0)
        {
            return false;
        }

        var lowerBound = targetRps * (1 - AllowedDeviation);
        var upperBound = targetRps * (1 + AllowedDeviation);
        return achievedRps >= lowerBound && achievedRps <= upperBound;
    }
}

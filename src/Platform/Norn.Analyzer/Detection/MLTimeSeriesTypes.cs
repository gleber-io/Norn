using Microsoft.ML.Data;

namespace Norn.Analyzer.Detection;

internal sealed class MLSample
{
    public float Value { get; set; }
}

internal sealed class SpikePrediction
{
    /// <summary>[0] Alerta (0/1) · [1] Score bruto · [2] P-Value.</summary>
    [VectorType(3)]
    public double[] Prediction { get; set; } = [];
}

internal sealed class ChangePointPrediction
{
    /// <summary>[0] Alerta (0/1) · [1] Score bruto · [2] P-Value · [3] Martingale.</summary>
    [VectorType(4)]
    public double[] Prediction { get; set; } = [];
}

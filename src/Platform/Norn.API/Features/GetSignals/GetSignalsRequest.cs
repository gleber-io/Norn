namespace Norn.API.Features.GetSignals;

public sealed record GetSignalsRequest
{
    public int Limit { get; init; } = 50;

    public Guid? ExperimentRunId { get; init; }
}

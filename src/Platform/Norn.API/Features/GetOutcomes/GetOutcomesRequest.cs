namespace Norn.API.Features.GetOutcomes;

public sealed record GetOutcomesRequest
{
    public int Limit { get; init; } = 50;
}

namespace Norn.API.Features.GetPlans;

public sealed record GetPlansRequest
{
    public int Limit { get; init; } = 50;
}

using FluentValidation;

namespace Norn.API.Features.GetMetricsSeries;

public sealed class GetMetricsSeriesValidator : AbstractValidator<GetMetricsSeriesRequest>
{
    private static readonly TimeSpan MaxWindow = TimeSpan.FromHours(1);

    public GetMetricsSeriesValidator()
    {
        // Nome de métrica do Prometheus é sempre [a-zA-Z_:][a-zA-Z0-9_:]* — a mesma regra que a
        // própria API do Prometheus impõe. Validar aqui, antes de interpolar na query PromQL,
        // é o que impede injeção via querystring (nenhuma escapagem de PromQL existe hoje).
        RuleFor(r => r.MetricName).Matches("^[a-zA-Z_:][a-zA-Z0-9_:]*$");

        // exported_job é o nome do serviço OTel (ex.: "Norn.Shop.Catalog.API") — letras, dígitos,
        // ponto e hífen bastam; qualquer coisa fora disso não é um exported_job válido no cluster.
        RuleFor(r => r.Service).Matches("^[A-Za-z0-9_.-]+$");

        RuleFor(r => r.ToUtc).GreaterThan(r => r.FromUtc);
        RuleFor(r => r).Must(r => r.ToUtc - r.FromUtc <= MaxWindow)
            .WithMessage($"A janela não pode exceder {MaxWindow.TotalHours}h.");
    }
}

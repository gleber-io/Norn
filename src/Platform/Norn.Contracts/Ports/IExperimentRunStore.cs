namespace Norn.Contracts.Ports;

/// <summary>
/// Escrita em <c>experiment_runs</c> (Fase 12). Duas operações porque as colunas nascem em dois
/// instantes diferentes da mesma execução (ver <see cref="ExperimentRunRecord"/> e
/// <see cref="ExperimentRunLabelingResult"/>). Implementada por Norn.Knowledge; consumida por
/// Norn.Labeler, que é o único composition root de campanha com acesso de escrita a esta tabela.
/// </summary>
public interface IExperimentRunStore
{
    Task CreateAsync(ExperimentRunRecord record, CancellationToken cancellationToken);

    Task UpdateLabelingResultAsync(ExperimentRunLabelingResult result, CancellationToken cancellationToken);
}

namespace Norn.Contracts;

/// <summary>
/// Proximidade da violação de SLO, não magnitude bruta do desvio (ADR-14) — a mesma régua
/// para toda métrica. Bandas parametrizadas via <c>IOptions</c>, nunca hardcoded no detector.
/// </summary>
public enum Severity
{
    Low,
    Medium,
    High,
    Critical,
}

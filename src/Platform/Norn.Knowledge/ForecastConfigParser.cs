using System.Text.Json;
using Norn.Contracts.Ports;

namespace Norn.Knowledge;

/// <summary>
/// Parsing e validação de <c>norn:platform:config:forecast</c> (§5.7, ADR-16), isolado de
/// <see cref="RedisPlatformConfig"/> para ser testável sem Redis. Configuração inválida nunca
/// derruba o loop: horizonte fora da faixa permitida (ou JSON inválido) volta desligado.
/// </summary>
public static class ForecastConfigParser
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static ForecastConfig ParseOrDefault(string? rawJson, out bool wasInvalid)
    {
        wasInvalid = false;

        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return new ForecastConfig { Enabled = false };
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<ForecastConfig>(rawJson, Options);
            if (parsed is null || parsed.HorizonMinutes <= 0)
            {
                wasInvalid = true;
                return new ForecastConfig { Enabled = false };
            }

            return parsed;
        }
        catch (JsonException)
        {
            wasInvalid = true;
            return new ForecastConfig { Enabled = false };
        }
    }
}

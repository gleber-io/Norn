using System.Globalization;

namespace Norn.Labeler.Cli;

/// <summary>Leitura de flags de linha de comando <c>--nome valor</c> — mesmo idioma do <c>Norn.LoadGenerator</c> (ADR-18, sem pacote de parsing).</summary>
public static class ArgReader
{
    public static string? GetSetting(string[] args, string flag)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    public static string GetRequiredSetting(string[] args, string flag) =>
        GetSetting(args, flag) ?? throw new ArgumentException($"Flag obrigatória ausente: {flag}");

    public static int GetRequiredInt(string[] args, string flag) =>
        int.Parse(GetRequiredSetting(args, flag), CultureInfo.InvariantCulture);

    public static double GetRequiredDouble(string[] args, string flag) =>
        double.Parse(GetRequiredSetting(args, flag), CultureInfo.InvariantCulture);

    public static Guid GetRequiredGuid(string[] args, string flag) =>
        Guid.Parse(GetRequiredSetting(args, flag));

    public static DateTimeOffset GetRequiredDateTimeOffset(string[] args, string flag) =>
        DateTimeOffset.Parse(GetRequiredSetting(args, flag), CultureInfo.InvariantCulture);

    public static DateTimeOffset? GetOptionalDateTimeOffset(string[] args, string flag)
    {
        var raw = GetSetting(args, flag);
        return raw is null || string.Equals(raw, "none", StringComparison.OrdinalIgnoreCase)
            ? null
            : DateTimeOffset.Parse(raw, CultureInfo.InvariantCulture);
    }
}

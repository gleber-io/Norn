using Microsoft.AspNetCore.Http;

namespace Norn.BuildingBlocks.Chaos;

/// <summary>
/// Middleware fino: só existe para dar ao efeito ativo (se houver, e se ele tocar requisições —
/// F2 e F3 tocam, F1 e F5 não) a chance de agir por requisição. Passa direto por <c>/health</c> e
/// <c>/admin</c> — o F3 não pode atrasar o próprio endpoint que desativa o caos, e o F2 não pode
/// travar o health check que o Kubernetes usa para decidir se o pod está vivo (Fase 6).
/// </summary>
internal sealed class ChaosMiddleware(RequestDelegate next, ChaosRuntimeState runtimeState)
{
    public Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        if (path.StartsWithSegments("/health") || path.StartsWithSegments("/admin"))
        {
            return next(context);
        }

        var effect = runtimeState.ActiveEffect;
        return effect is null ? next(context) : effect.OnRequestAsync(context, next).AsTask();
    }
}

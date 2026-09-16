namespace Norn.API.Events;

/// <summary>
/// Estado do assinante de <c>norn:events</c>, lido por <see cref="NornEventRelayHealthCheck"/>.
/// Deliberadamente <b>não</b> baseado em "recebeu evento nos últimos N segundos" — o canal fica
/// legitimamente quieto por longos períodos sem nada anômalo acontecendo, e isso não pode
/// disparar alarme. Saúde = "a assinatura em si está viva", não "há tráfego".
/// </summary>
public sealed class NornEventRelayHealthState
{
    private volatile bool isSubscribed;

    public bool IsSubscribed => isSubscribed;

    public void MarkSubscribed() => isSubscribed = true;

    public void MarkConnectionFailed() => isSubscribed = false;

    public void MarkConnectionRestored() => isSubscribed = true;
}

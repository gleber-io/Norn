using Microsoft.AspNetCore.SignalR;

namespace Norn.API.Hubs;

/// <summary>
/// Fase 10, tarefa 2. Só push servidor→cliente — nenhum método invocável pelo cliente, então o
/// corpo fica vazio. <see cref="Events.PlatformEventRelay"/> é quem chama
/// <c>IHubContext&lt;NornHub, INornHubClient&gt;</c> a partir do assinante de <c>norn:events</c>.
/// </summary>
public sealed class NornHub : Hub<INornHubClient>;

using System.Net.Http.Json;
using System.Text.Json;

namespace Norn.LoadGenerator;

/// <summary>Fala HTTP com o Shop — nenhuma referência de projeto (§4): os DTOs são reescritos aqui, não importados.</summary>
public sealed class ShopClient(HttpClient httpClient, string catalogBaseUrl, string orderBaseUrl)
{
    // O Shop serializa em camelCase (política padrão do Minimal API); o cliente precisa da mesma
    // convenção nos dois sentidos, já que `JsonSerializerOptions.Default` é sensível a maiúsculas.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Busca um lote de produtos para popular o carrinho — com retentativa, pois o Shop pode ainda estar subindo.</summary>
    public async Task<IReadOnlyList<ProductSummary>> FetchProductPoolAsync(int pageSize, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= 10; attempt++)
        {
            try
            {
                var response = await httpClient.GetFromJsonAsync<GetProductsResponse>(
                    $"{catalogBaseUrl}/api/v1/products?page=1&pageSize={pageSize}", JsonOptions, cancellationToken);

                if (response?.Items is { Count: > 0 })
                {
                    return response.Items;
                }
            }
            catch (HttpRequestException)
            {
                // Catalog ainda subindo — tenta de novo.
            }

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }

        throw new InvalidOperationException($"Não foi possível obter produtos de {catalogBaseUrl} após 10 tentativas.");
    }

    public async Task<bool> BrowseProductsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync($"{catalogBaseUrl}/api/v1/products?page=1&pageSize=20", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    /// Recebe os itens já sorteados (ver <see cref="CartPicker"/>) em vez de sortear aqui: <see cref="Random"/>
    /// não é thread-safe, e este método roda disparado em paralelo por <c>Task.WhenAll</c> — sortear
    /// dentro dele corromperia o estado do gerador (e, com ele, a reprodutibilidade por seed).
    /// </summary>
    public async Task<bool> CreateOrderAsync(IReadOnlyList<CartItem> items, CancellationToken cancellationToken)
    {
        var request = new CreateOrderRequest(Guid.NewGuid(), "BRL", items.Select(i => new CreateOrderItem(i.ProductId, i.Quantity, i.UnitPrice)).ToList());

        try
        {
            using var response = await httpClient.PostAsJsonAsync($"{orderBaseUrl}/api/v1/orders", request, JsonOptions, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    public sealed record ProductSummary(Guid Id, decimal Price);

    public sealed record CartItem(Guid ProductId, int Quantity, decimal UnitPrice);

    private sealed record GetProductsResponse(IReadOnlyList<ProductSummary> Items);

    private sealed record CreateOrderItem(Guid ProductId, int Quantity, decimal UnitPrice);

    private sealed record CreateOrderRequest(Guid CustomerId, string Currency, IReadOnlyList<CreateOrderItem> Items);
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Norn.API.Features.GetMode;
using Norn.API.Features.SetMode;
using Norn.Contracts.Ports;
using Shouldly;
using StackExchange.Redis;
using Xunit;

namespace Norn.API.IntegrationTests;

/// <summary>
/// <c>PUT /api/v1/mode</c> muda o modo de verdade, e — a prova concreta da decisão 2 do plano da
/// Fase 10 — nunca publica em <c>norn:events</c> por conta própria. Só Norn.Worker publica nesse
/// canal (ADR-15); a mudança de modo chega ao dashboard pelo diff no polling do Worker, não por
/// este endpoint.
/// </summary>
[Collection(nameof(NornApiCollectionDefinition))]
public sealed class PutModeTests(NornApiFactory factory)
{
    // Enum vai como string no wire desde a correção de Fase 11 — espelha ConfigureHttpJsonOptions
    // de Norn.API.Program, tanto pro corpo do PUT quanto pra leitura da resposta do GET.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task PutMode_ChangesMode_ReflectedByGetMode()
    {
        var client = factory.CreateClient();

        var putResponse = await client.PutAsJsonAsync(
            "/api/v1/mode", new SetModeRequest { Mode = PlatformMode.DryRun }, JsonOptions, TestContext.Current.CancellationToken);
        putResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var getResponse = await client.GetAsync("/api/v1/mode", TestContext.Current.CancellationToken);
        var mode = await getResponse.Content.ReadFromJsonAsync<ModeResponse>(JsonOptions, TestContext.Current.CancellationToken);
        mode.ShouldNotBeNull();
        mode.Mode.ShouldBe(PlatformMode.DryRun);
    }

    [Fact]
    public async Task PutMode_NeverPublishesToNornEvents()
    {
        await using var redisConnection = await ConnectionMultiplexer.ConnectAsync(factory.RedisConnectionString);
        var subscriber = redisConnection.GetSubscriber();
        var messageCount = 0;
        await subscriber.SubscribeAsync(RedisChannel.Literal("norn:events"), (_, _) => Interlocked.Increment(ref messageCount));

        var client = factory.CreateClient();
        var response = await client.PutAsJsonAsync(
            "/api/v1/mode", new SetModeRequest { Mode = PlatformMode.Active }, JsonOptions, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Sem evento algum a esperar — a ausência é o que este teste prova. Uma pequena espera dá
        // tempo a uma publicação indevida de chegar antes da asserção.
        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

        messageCount.ShouldBe(0);
    }
}

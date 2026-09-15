using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Norn.Shop.Payment.API.Application.Ports;
using Norn.Shop.Payment.API.Features.ProcessPayment;
using NSubstitute;
using Shouldly;
using StackExchange.Redis;
using Xunit;

namespace Norn.Shop.Payment.API.IntegrationTests;

/// <summary>
/// Cobre o endpoint HTTP e, sobretudo, a tarefa 3a da Fase 3: com <c>payment.gateway.bypass</c>
/// ligada, o gateway não é chamado (dublê com asserção de zero invocações), a resposta continua
/// 202 e o pagamento é aprovado no caminho degradado.
/// </summary>
public sealed class PaymentEndpointsTests(PaymentApiFactory factory) : IClassFixture<PaymentApiFactory>
{
    private const string GatewayBypassKey = "shop:flags:payment.gateway.bypass";
    private const string InvalidationChannel = "shop:flags:invalidate";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static ProcessPaymentRequest ValidRequest() => new()
    {
        OrderId = Guid.NewGuid(),
        Amount = 100m,
        Currency = "BRL",
        Method = "credit_card",
    };

    [Fact]
    public async Task ProcessPayment_GatewayApproves_ReturnsAcceptedWithApprovedStatus()
    {
        await SetGatewayBypassAsync(enabled: false);
        factory.Gateway.ClearReceivedCalls();
        factory.Gateway.AuthorizeAsync(Arg.Any<GatewayAuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GatewayAuthorizationResult(true, "AUTH-OK", null, null));
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/payments", ValidRequest(), JsonOptions, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var body = await response.Content.ReadFromJsonAsync<ProcessPaymentResponse>(JsonOptions, TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.Status.ShouldBe("Approved");
        await factory.Gateway.Received(1).AuthorizeAsync(Arg.Any<GatewayAuthorizationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessPayment_GatewayDeclines_ReturnsAcceptedWithDeclinedStatus()
    {
        await SetGatewayBypassAsync(enabled: false);
        factory.Gateway.ClearReceivedCalls();
        factory.Gateway.AuthorizeAsync(Arg.Any<GatewayAuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GatewayAuthorizationResult(false, null, "AMOUNT_EXCEEDS_LIMIT", "acima do limite"));
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/payments", ValidRequest(), JsonOptions, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var body = await response.Content.ReadFromJsonAsync<ProcessPaymentResponse>(JsonOptions, TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.Status.ShouldBe("Declined");
    }

    [Fact]
    public async Task ProcessPayment_GatewayValidationFails_ReturnsBadRequest()
    {
        await SetGatewayBypassAsync(enabled: false);
        var client = factory.CreateClient();
        var invalidRequest = ValidRequest() with { Amount = 0m };

        var response = await client.PostAsJsonAsync("/api/v1/payments", invalidRequest, JsonOptions, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Fase 3, tarefa 3a — teste de integração obrigatório: flag ligada, gateway não é chamado.
    /// </summary>
    [Fact]
    public async Task ProcessPayment_GatewayBypassEnabled_ApprovesDegradedWithoutCallingGateway()
    {
        await SetGatewayBypassAsync(enabled: true);
        factory.Gateway.ClearReceivedCalls();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/payments", ValidRequest(), JsonOptions, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var body = await response.Content.ReadFromJsonAsync<ProcessPaymentResponse>(JsonOptions, TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.Status.ShouldBe("Approved");
        await factory.Gateway.DidNotReceive().AuthorizeAsync(Arg.Any<GatewayAuthorizationRequest>(), Arg.Any<CancellationToken>());

        await SetGatewayBypassAsync(enabled: false);
    }

    private async Task SetGatewayBypassAsync(bool enabled)
    {
        using var scope = factory.Services.CreateScope();
        var connectionMultiplexer = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();

        var database = connectionMultiplexer.GetDatabase();
        await database.StringSetAsync(GatewayBypassKey, enabled ? "true" : "false");
        await connectionMultiplexer.GetSubscriber().PublishAsync(RedisChannel.Literal(InvalidationChannel), "payment.gateway.bypass");
    }
}

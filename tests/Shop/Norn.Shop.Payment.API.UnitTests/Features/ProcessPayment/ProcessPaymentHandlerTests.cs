using MassTransit;
using Norn.BuildingBlocks.Web.FeatureFlags;
using Norn.Shop.Contracts.Events;
using Norn.Shop.Payment.API.Application;
using Norn.Shop.Payment.API.Application.Ports;
using Norn.Shop.Payment.API.Domain;
using Norn.Shop.Payment.API.Features.ProcessPayment;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Norn.Shop.Payment.API.UnitTests.Features.ProcessPayment;

public sealed class ProcessPaymentHandlerTests
{
    private static ProcessPaymentRequest ValidRequest() => new()
    {
        OrderId = Guid.NewGuid(),
        Amount = 100m,
        Currency = "BRL",
        Method = "credit_card",
    };

    [Fact]
    public async Task HandleAsync_GatewayApproves_ApprovesPaymentAndPublishesPaymentApproved()
    {
        var repository = Substitute.For<IPaymentRepository>();
        var gateway = Substitute.For<IPaymentGateway>();
        gateway.AuthorizeAsync(Arg.Any<GatewayAuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GatewayAuthorizationResult(true, "AUTH123", null, null));
        var featureFlags = Substitute.For<IFeatureFlags>();
        featureFlags.IsEnabledAsync(PaymentFeatureFlags.GatewayBypass, Arg.Any<CancellationToken>()).Returns(false);
        var publishEndpoint = Substitute.For<IPublishEndpoint>();
        var metrics = Substitute.For<IPaymentMetrics>();

        var payment = await ProcessPaymentHandler.HandleAsync(
            ValidRequest(), Guid.NewGuid(), null, repository, gateway, featureFlags, publishEndpoint, metrics, TestContext.Current.CancellationToken);

        payment.Status.ShouldBe(PaymentStatus.Approved);
        payment.AuthorizationCode.ShouldBe("AUTH123");
        await publishEndpoint.Received(1).Publish(
            Arg.Any<PaymentApprovedEvent>(), Arg.Any<IPipe<PublishContext<PaymentApprovedEvent>>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_GatewayDeclines_DeclinesPaymentAndPublishesPaymentDeclined()
    {
        var repository = Substitute.For<IPaymentRepository>();
        var gateway = Substitute.For<IPaymentGateway>();
        gateway.AuthorizeAsync(Arg.Any<GatewayAuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GatewayAuthorizationResult(false, null, "AMOUNT_EXCEEDS_LIMIT", "acima do limite"));
        var featureFlags = Substitute.For<IFeatureFlags>();
        featureFlags.IsEnabledAsync(PaymentFeatureFlags.GatewayBypass, Arg.Any<CancellationToken>()).Returns(false);
        var publishEndpoint = Substitute.For<IPublishEndpoint>();
        var metrics = Substitute.For<IPaymentMetrics>();

        var payment = await ProcessPaymentHandler.HandleAsync(
            ValidRequest(), Guid.NewGuid(), null, repository, gateway, featureFlags, publishEndpoint, metrics, TestContext.Current.CancellationToken);

        payment.Status.ShouldBe(PaymentStatus.Declined);
        payment.DeclineReasonCode.ShouldBe("AMOUNT_EXCEEDS_LIMIT");
        await publishEndpoint.Received(1).Publish(
            Arg.Any<PaymentDeclinedEvent>(), Arg.Any<IPipe<PublishContext<PaymentDeclinedEvent>>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_GatewayBypassEnabled_ApprovesDegradedWithoutCallingGateway()
    {
        var repository = Substitute.For<IPaymentRepository>();
        var gateway = Substitute.For<IPaymentGateway>();
        var featureFlags = Substitute.For<IFeatureFlags>();
        featureFlags.IsEnabledAsync(PaymentFeatureFlags.GatewayBypass, Arg.Any<CancellationToken>()).Returns(true);
        var publishEndpoint = Substitute.For<IPublishEndpoint>();
        var metrics = Substitute.For<IPaymentMetrics>();

        var payment = await ProcessPaymentHandler.HandleAsync(
            ValidRequest(), Guid.NewGuid(), null, repository, gateway, featureFlags, publishEndpoint, metrics, TestContext.Current.CancellationToken);

        payment.Status.ShouldBe(PaymentStatus.Approved);
        payment.Degraded.ShouldBeTrue();
        await gateway.DidNotReceive().AuthorizeAsync(Arg.Any<GatewayAuthorizationRequest>(), Arg.Any<CancellationToken>());
        metrics.Received(1).RecordDegradedPayment();
    }
}

using Norn.Shop.Payment.API.Domain;
using Shouldly;
using Xunit;

namespace Norn.Shop.Payment.API.UnitTests.Domain;

public sealed class PaymentTransactionTests
{
    private static PaymentTransaction CreatePayment() => new(Guid.NewGuid(), Guid.NewGuid(), 100m, "BRL", "credit_card");

    [Fact]
    public void Constructor_ValidArguments_StartsPending()
    {
        var payment = CreatePayment();

        payment.Status.ShouldBe(PaymentStatus.Pending);
        payment.Degraded.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_NonPositiveAmount_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new PaymentTransaction(Guid.NewGuid(), Guid.NewGuid(), 0m, "BRL", "credit_card"));

    [Fact]
    public void Constructor_InvalidCurrency_Throws() =>
        Should.Throw<ArgumentException>(() => new PaymentTransaction(Guid.NewGuid(), Guid.NewGuid(), 10m, "R$", "credit_card"));

    [Fact]
    public void Constructor_EmptyMethod_Throws() =>
        Should.Throw<ArgumentException>(() => new PaymentTransaction(Guid.NewGuid(), Guid.NewGuid(), 10m, "BRL", string.Empty));

    [Fact]
    public void Approve_FromPending_SetsApprovedWithAuthorizationCode()
    {
        var payment = CreatePayment();

        payment.Approve("AUTH123", degraded: false);

        payment.Status.ShouldBe(PaymentStatus.Approved);
        payment.AuthorizationCode.ShouldBe("AUTH123");
        payment.Degraded.ShouldBeFalse();
    }

    [Fact]
    public void Approve_Degraded_MarksDegradedTrue()
    {
        var payment = CreatePayment();

        payment.Approve("BYPASS-1", degraded: true);

        payment.Degraded.ShouldBeTrue();
    }

    [Fact]
    public void Approve_NotPending_Throws()
    {
        var payment = CreatePayment();
        payment.Approve("AUTH1", degraded: false);

        Should.Throw<InvalidPaymentTransitionException>(() => payment.Approve("AUTH2", degraded: false));
    }

    [Fact]
    public void Decline_FromPending_SetsDeclinedWithReason()
    {
        var payment = CreatePayment();

        payment.Decline("REASON", "descrição");

        payment.Status.ShouldBe(PaymentStatus.Declined);
        payment.DeclineReasonCode.ShouldBe("REASON");
        payment.DeclineReasonDescription.ShouldBe("descrição");
    }

    [Fact]
    public void Decline_NotPending_Throws()
    {
        var payment = CreatePayment();
        payment.Decline("REASON", "descrição");

        Should.Throw<InvalidPaymentTransitionException>(() => payment.Decline("OTHER", "outra"));
    }
}

using Norn.Shop.Payment.API.Features.ProcessPayment;
using Shouldly;
using Xunit;

namespace Norn.Shop.Payment.API.UnitTests.Features.ProcessPayment;

public sealed class ProcessPaymentValidatorTests
{
    private readonly ProcessPaymentValidator _validator = new();

    private static ProcessPaymentRequest ValidRequest() => new()
    {
        OrderId = Guid.NewGuid(),
        Amount = 100m,
        Currency = "BRL",
        Method = "credit_card",
    };

    [Fact]
    public void Validate_ValidRequest_HasNoErrors() => _validator.Validate(ValidRequest()).IsValid.ShouldBeTrue();

    [Fact]
    public void Validate_NonPositiveAmount_HasError() =>
        _validator.Validate(ValidRequest() with { Amount = 0m }).IsValid.ShouldBeFalse();

    [Fact]
    public void Validate_CurrencyWithWrongLength_HasError() =>
        _validator.Validate(ValidRequest() with { Currency = "R$" }).IsValid.ShouldBeFalse();

    [Fact]
    public void Validate_EmptyMethod_HasError() =>
        _validator.Validate(ValidRequest() with { Method = string.Empty }).IsValid.ShouldBeFalse();

    [Fact]
    public void Validate_EmptyOrderId_HasError() =>
        _validator.Validate(ValidRequest() with { OrderId = Guid.Empty }).IsValid.ShouldBeFalse();
}

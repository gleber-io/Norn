using Norn.Shop.Catalog.API.Features.GetProducts;
using Shouldly;
using Xunit;

namespace Norn.Shop.Catalog.API.UnitTests.Features.GetProducts;

public sealed class GetProductsValidatorTests
{
    private readonly GetProductsValidator _validator = new();

    [Fact]
    public void Validate_DefaultRequest_IsValid() =>
        _validator.Validate(new GetProductsRequest()).IsValid.ShouldBeTrue();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_PageBelowOne_IsInvalid(int page) =>
        _validator.Validate(new GetProductsRequest { Page = page }).IsValid.ShouldBeFalse();

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Validate_PageSizeOutsideRange_IsInvalid(int pageSize) =>
        _validator.Validate(new GetProductsRequest { PageSize = pageSize }).IsValid.ShouldBeFalse();
}

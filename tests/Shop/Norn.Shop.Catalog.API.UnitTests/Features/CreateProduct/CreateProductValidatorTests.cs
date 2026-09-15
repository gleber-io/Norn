using Norn.Shop.Catalog.API.Features.CreateProduct;
using Shouldly;
using Xunit;

namespace Norn.Shop.Catalog.API.UnitTests.Features.CreateProduct;

public sealed class CreateProductValidatorTests
{
    private readonly CreateProductValidator _validator = new();

    private static CreateProductRequest ValidRequest() => new()
    {
        Name = "Produto",
        Description = "desc",
        Price = 10m,
        Currency = "BRL",
        Category = "Categoria",
        Stock = 5,
    };

    [Fact]
    public void Validate_ValidRequest_IsValid() =>
        _validator.Validate(ValidRequest()).IsValid.ShouldBeTrue();

    [Fact]
    public void Validate_EmptyName_IsInvalid() =>
        _validator.Validate(ValidRequest() with { Name = string.Empty }).IsValid.ShouldBeFalse();

    [Fact]
    public void Validate_ZeroPrice_IsInvalid() =>
        _validator.Validate(ValidRequest() with { Price = 0 }).IsValid.ShouldBeFalse();

    [Fact]
    public void Validate_CurrencyNotThreeLetters_IsInvalid() =>
        _validator.Validate(ValidRequest() with { Currency = "R$" }).IsValid.ShouldBeFalse();

    [Fact]
    public void Validate_NegativeStock_IsInvalid() =>
        _validator.Validate(ValidRequest() with { Stock = -1 }).IsValid.ShouldBeFalse();
}

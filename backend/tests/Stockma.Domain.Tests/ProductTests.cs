using FluentAssertions;
using Stockma.Domain.Entities;
using Stockma.Domain.Enums;

namespace Stockma.Domain.Tests;

public class ProductTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static Product CreateProduct(string sku = "SKU-1", string? barcode = null) =>
        new(TenantId, sku, "Acetaminofén 500mg", ProductCategory.Medication, barcode);

    [Fact]
    public void NewProduct_DefaultsCurrencyToCop()
    {
        var product = CreateProduct();

        product.Currency.Should().Be("COP", "el default del tenant es COP (NFR-008)");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void NewProduct_RejectsBlankName(string name)
    {
        var act = () => new Product(TenantId, "SKU-1", name, ProductCategory.Medication, null);

        act.Should().Throw<ArgumentException>().WithParameterName("name");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void NewProduct_RejectsBlankSku(string sku)
    {
        var act = () => new Product(TenantId, sku, "Acetaminofén", ProductCategory.Medication, null);

        act.Should().Throw<ArgumentException>().WithParameterName("sku");
    }

    [Fact]
    public void NewProduct_RejectsEmptyTenantId()
    {
        var act = () => new Product(Guid.Empty, "SKU-1", "Acetaminofén", ProductCategory.Medication, null);

        act.Should().Throw<ArgumentException>().WithParameterName("tenantId");
    }

    private static Product CreateUpdatedProduct()
    {
        var product = CreateProduct("SKU-42");

        product.Update(
            name: "Acetaminofén 650mg",
            category: ProductCategory.Supplement,
            barcode: "7701234567890",
            activeIngredient: "Paracetamol",
            presentation: "Caja x 20",
            storageConditions: "Lugar seco",
            currency: "USD");

        return product;
    }

    [Fact]
    public void Update_KeepsTheSku()
    {
        CreateUpdatedProduct().Sku.Should().Be(
            "SKU-42",
            "el SKU es inmutable tras la creación (FR-010)");
    }

    [Fact]
    public void Update_WritesEveryEditableField()
    {
        CreateUpdatedProduct().Should().BeEquivalentTo(
            new
            {
                Name = "Acetaminofén 650mg",
                Category = ProductCategory.Supplement,
                Barcode = "7701234567890",
                ActiveIngredient = "Paracetamol",
                Presentation = "Caja x 20",
                StorageConditions = "Lugar seco",
                Currency = "USD",
            },
            options => options.ExcludingMissingMembers(),
            "Update debe escribir todos los campos editables");
    }

    [Fact]
    public void Product_MustNotExposeAnySkuSetter()
    {
        var skuProperty = typeof(Product).GetProperty(nameof(Product.Sku));

        skuProperty.Should().NotBeNull();
        skuProperty!.SetMethod?.IsPublic.Should().NotBe(
            true,
            "el SKU no debe tener setter público: su inmutabilidad es una garantía del modelo");
    }
}

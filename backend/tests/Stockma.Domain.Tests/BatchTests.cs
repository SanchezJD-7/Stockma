using FluentAssertions;
using Stockma.Domain.Entities;
using Stockma.Domain.Enums;
using Stockma.Domain.Exceptions;

namespace Stockma.Domain.Tests;

public class BatchTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ProductId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateOnly Expiration = new(2027, 6, 30);

    private static Batch CreateBatch(int quantity = 10) =>
        new(TenantId, ProductId, "LOTE-001", Expiration, quantity, "Estante A3");

    [Fact]
    public void NewBatch_StartsActive()
    {
        CreateBatch().Status.Should().Be(BatchStatus.Active);
    }

    [Fact]
    public void NewBatch_RejectsNegativeQuantity()
    {
        var act = () => CreateBatch(-1);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("initialQuantity");
    }

    [Fact]
    public void NewBatch_AllowsZeroQuantity()
    {
        CreateBatch(0).CurrentQuantity.Should().Be(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void NewBatch_RejectsBlankLotNumber(string lotNumber)
    {
        var act = () => new Batch(TenantId, ProductId, lotNumber, Expiration, 10, "Estante A3");

        act.Should().Throw<ArgumentException>().WithParameterName("lotNumber");
    }

    [Fact]
    public void NewBatch_RejectsEmptyProductId()
    {
        var act = () => new Batch(TenantId, Guid.Empty, "LOTE-001", Expiration, 10, "Estante A3");

        act.Should().Throw<ArgumentException>().WithParameterName("productId");
    }

    [Fact]
    public void Adjust_WithPositiveDelta_IncreasesStock()
    {
        var batch = CreateBatch(10);

        batch.Adjust(5);

        batch.CurrentQuantity.Should().Be(15, "escenario 'Aumento de stock' de FR-013");
    }

    [Fact]
    public void Adjust_WithNegativeDelta_DecreasesStock()
    {
        var batch = CreateBatch(10);

        batch.Adjust(-4);

        batch.CurrentQuantity.Should().Be(6);
    }

    [Fact]
    public void Adjust_ToExactlyZero_IsAllowed()
    {
        var batch = CreateBatch(3);

        batch.Adjust(-3);

        batch.CurrentQuantity.Should().Be(0, "cero no es negativo: el ajuste es válido");
    }

    [Fact]
    public void Adjust_BeyondAvailableStock_IsRejectedEntirely()
    {
        var batch = CreateBatch(3);

        var act = () => batch.Adjust(-5);

        act.Should().Throw<NegativeStockException>(
            "escenario 'Salida excede stock' de FR-013: 422 BATCH_NEGATIVE_STOCK");

        batch.CurrentQuantity.Should().Be(
            3,
            "el rechazo es TOTAL: el stock no debe quedar parcialmente ajustado");
    }

    [Fact]
    public void Adjust_WithZeroDelta_IsRejected()
    {
        var batch = CreateBatch(10);

        var act = () => batch.Adjust(0);

        act.Should().Throw<ArgumentException>().WithParameterName("delta");
    }

    [Fact]
    public void NegativeStockException_CarriesTheContractErrorCode()
    {
        var batch = CreateBatch(3);

        var exception = Assert.Throws<NegativeStockException>(() => batch.Adjust(-5));

        exception.ErrorCode.Should().Be("BATCH_NEGATIVE_STOCK");
    }
}

using MediatR;
using Microsoft.AspNetCore.Mvc;
using Stockma.Application.Products;
using Stockma.Application.Products.Commands;
using Stockma.Application.Products.Queries;

namespace Stockma.Api.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ProductDto>> Register(RegisterProductCommand command, CancellationToken cancellationToken)
    {
        var product = await sender.Send(command, cancellationToken);
        return Created($"/api/products/{product.Id}", product);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProductDto>> Update(Guid id, UpdateProductCommand command, CancellationToken cancellationToken)
    {
        var product = await sender.Send(command with { Id = id }, cancellationToken);
        return Ok(product);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> Search(
        [FromQuery] string query,
        [FromQuery] int limit = ProductSearchLimits.Default,
        CancellationToken cancellationToken = default)
    {
        var matches = await sender.Send(new SearchProductsQuery(query, limit), cancellationToken);
        return Ok(matches);
    }

    [HttpGet("by-barcode/{code}")]
    public async Task<ActionResult<ProductDto>> GetByBarcode(string code, CancellationToken cancellationToken)
    {
        var product = await sender.Send(new GetProductByBarcodeQuery(code), cancellationToken);
        return Ok(product);
    }
}

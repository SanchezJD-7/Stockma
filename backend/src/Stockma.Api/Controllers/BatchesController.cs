using MediatR;
using Microsoft.AspNetCore.Mvc;
using Stockma.Application.Batches;
using Stockma.Application.Batches.Commands;
using Stockma.Application.Batches.Queries;

namespace Stockma.Api.Controllers;

[ApiController]
[Route("api/batches")]
public sealed class BatchesController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<BatchDto>> Register(RegisterBatchCommand command, CancellationToken cancellationToken)
    {
        var batch = await sender.Send(command, cancellationToken);

        return Created($"/api/batches/{batch.Id}", batch);
    }

    [HttpPost("{id:guid}/adjust")]
    public async Task<ActionResult<BatchDto>> Adjust(Guid id, AdjustBatchStockCommand command, CancellationToken cancellationToken)
    {
        var batch = await sender.Send(command with { BatchId = id }, cancellationToken);
        return Ok(batch);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BatchDto>>> GetByProduct([FromQuery] Guid productId, CancellationToken cancellationToken)
    {
        var batches = await sender.Send(new GetBatchesQuery(productId), cancellationToken);
        return Ok(batches);
    }
}

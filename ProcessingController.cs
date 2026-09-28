using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace NodeWorker;

[ApiController]
[Route("api")]
public sealed class ProcessingController(
    IValidator<ProcessRequest> validator,
    ProcessingService service) : ControllerBase
{
    /// <summary>Извлекает элементы, адреса email и расшифрованный текст из HTML.</summary>
    [HttpPost("process")]
    [ProducesResponseType<ProcessResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProcessResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProcessResponse>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ProcessResponse>> ProcessAsync(
        [FromBody] ProcessRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(ProcessResponse.Failure("REQUEST_MISSING", "A JSON request body is required."));
        }

        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var error = validation.Errors[0];
            return BadRequest(ProcessResponse.Failure(
                error.ErrorCode,
                string.Join(" ", validation.Errors.Select(item => item.ErrorMessage))));
        }

        try
        {
            return Ok(await service.ProcessAsync(request, cancellationToken));
        }
        catch (RequestProcessingException exception)
        {
            return BadRequest(ProcessResponse.Failure(exception.Code, exception.Message));
        }
        catch (Exception exception)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ProcessResponse.Failure("INTERNAL_ERROR", exception.Message));
        }
    }
}
using CodeExplainer.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CodeExplainer.Api.Controllers;

[ApiController]
[Route("api")]
public class DiagramController : ControllerBase
{
    private readonly IDiagramService _diagramService;

    public DiagramController(IDiagramService diagramService)
    {
        _diagramService = diagramService;
    }

    [HttpPost("diagram/analyze")]
    [HttpPost("analyze")]
    public async Task<IActionResult> Analyze([FromBody] AnalyzeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Path))
            return BadRequest(new { error = "Path is required." });

        try
        {
            var documents = await _diagramService.AnalyzeAsync(request.Path);
            var diagrams = documents
                .Where(document => document.Type is "mermaid" or "plantuml" or "svg")
                .ToArray();
            return Ok(new { documents, diagrams });
        }
        catch (DirectoryNotFoundException exception)
        {
            return NotFound(new { error = exception.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "The document directory cannot be read." });
        }
        catch (InvalidDataException exception)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge, new { error = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }
}

public sealed record AnalyzeRequest(string Path);

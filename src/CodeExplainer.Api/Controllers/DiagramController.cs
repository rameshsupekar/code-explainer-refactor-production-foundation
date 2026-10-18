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

    [HttpPost("analyze")]
    public async Task<IActionResult> Analyze([FromBody] AnalyzeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Path))
            return BadRequest(new { error = "Path is required." });

        var diagrams = await _diagramService.AnalyzeAsync(request.Path);
        return Ok(new { diagrams });
    }
}

public sealed record AnalyzeRequest(string Path);

using System.Linq;
using System.Threading.Tasks;
using CodeExplainer.Analyzers.CSharp;
using CodeExplainer.Core.Models;
using Xunit;

namespace CodeExplainer.Tests;

public sealed class CSharpAnalyzerTests
{
    [Fact]
    public async Task AnalyzeAsync_ExtractsControllerRoutesAndConstructorDependencies()
    {
        const string source = """
            namespace Demo.Api;
            [Route("api/orders")]
            public sealed class OrdersController : ControllerBase
            {
                private readonly IOrderService service;
                public OrdersController(IOrderService service) => this.service = service;
                [HttpGet("{id:int}")]
                public object Get(int id) => service.Find(id);
            }
            """;

        var symbols = (await new CSharpAnalyzer().AnalyzeAsync("src/OrdersController.cs", source)).ToList();

        var controller = Assert.Single(symbols, symbol => symbol.Kind == SymbolKind.Controller);
        Assert.Equal("IOrderService", controller.Metadata["dependencies"]);
        var endpoint = Assert.Single(symbols, symbol => symbol.Kind == SymbolKind.Endpoint);
        Assert.Equal("GET", endpoint.Metadata["httpMethod"]);
        Assert.Equal("api/orders/{id:int}", endpoint.Metadata["route"]);
        Assert.Equal(7, endpoint.Location.StartLine);
    }

    [Fact]
    public async Task AnalyzeAsync_ExtractsMinimalApiEndpointAndHostedWorker()
    {
        const string source = """
            using Microsoft.AspNetCore.Builder;
            public sealed class QueueWorker : BackgroundService { }
            public static class Routes
            {
                public static void Map(WebApplication app) => app.MapPost("/api/jobs", () => "accepted");
            }
            """;

        var symbols = (await new CSharpAnalyzer().AnalyzeAsync("src/Program.cs", source)).ToList();

        var worker = Assert.Single(symbols, symbol => symbol.Name == "QueueWorker");
        Assert.Equal("background-worker", worker.Metadata["executionRole"]);
        var endpoint = Assert.Single(symbols, symbol => symbol.Kind == SymbolKind.Endpoint);
        Assert.Equal("POST", endpoint.Metadata["httpMethod"]);
        Assert.Equal("api/jobs", endpoint.Metadata["route"]);
        Assert.Equal("minimal-api", endpoint.Metadata["endpointStyle"]);
    }
}
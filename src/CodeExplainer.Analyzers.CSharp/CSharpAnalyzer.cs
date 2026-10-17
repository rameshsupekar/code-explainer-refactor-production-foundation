using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeExplainer.Core.Models;
using CodeExplainer.Core.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModelSymbolKind = CodeExplainer.Core.Models.SymbolKind;

namespace CodeExplainer.Analyzers.CSharp;

public sealed class CSharpAnalyzer
{
    public Task<IEnumerable<CodeSymbol>> AnalyzeAsync(string filePath, string content, CancellationToken cancellationToken = default)
    {
        var tree = CSharpSyntaxTree.ParseText(content, cancellationToken: cancellationToken);
        var root = tree.GetCompilationUnitRoot(cancellationToken);
        var namespaceName = root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString() ?? string.Empty;
        var symbols = new List<CodeSymbol>();

        foreach (var type in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = type.Identifier.ValueText;
            var qualifiedName = string.IsNullOrEmpty(namespaceName) ? name : $"{namespaceName}.{name}";
            var baseTypes = type.BaseList?.Types.Select(t => t.Type.ToString()).ToList() ?? new List<string>();
            var isController = type is ClassDeclarationSyntax &&
                (name.EndsWith("Controller", System.StringComparison.Ordinal) ||
                 baseTypes.Any(baseType => baseType.EndsWith("Controller", System.StringComparison.Ordinal)));
            var isWorker = baseTypes.Any(baseType => baseType.EndsWith("BackgroundService", System.StringComparison.Ordinal) ||
                baseType.EndsWith("IHostedService", System.StringComparison.Ordinal));
            var isService = !isController && !isWorker && type is ClassDeclarationSyntax &&
                (name.EndsWith("Service", System.StringComparison.Ordinal) || name.EndsWith("Repository", System.StringComparison.Ordinal));
            var kind = type is InterfaceDeclarationSyntax
                ? ModelSymbolKind.Interface
                : isController ? ModelSymbolKind.Controller
                : isService ? ModelSymbolKind.Service
                : ModelSymbolKind.Class;
            var metadata = new Dictionary<string, string> { ["namespace"] = namespaceName };
            if (baseTypes.Count > 0) metadata["baseTypes"] = string.Join(",", baseTypes);
            if (isWorker) metadata["executionRole"] = "background-worker";
            var constructorDependencies = type.Members.OfType<ConstructorDeclarationSyntax>()
                .SelectMany(constructor => constructor.ParameterList.Parameters)
                .Select(parameter => parameter.Type?.ToString())
                .Where(parameterType => !string.IsNullOrWhiteSpace(parameterType))
                .Distinct()
                .ToList();
            if (constructorDependencies.Count > 0) metadata["dependencies"] = string.Join(",", constructorDependencies);
            var members = type.Members.Select(member => CreateMember(member, filePath, tree)).ToList();
            var typeId = StableIdentifier.CreateCSharpSymbolId(filePath, qualifiedName);
            symbols.Add(new CodeSymbol(typeId, name, kind, "csharp", filePath, ToLocation(type.GetLocation()), members, metadata));

            if (isController)
            {
                var controllerRoute = GetRouteTemplate(type.AttributeLists.SelectMany(list => list.Attributes));
                foreach (var method in type.Members.OfType<MethodDeclarationSyntax>())
                {
                    var httpAttribute = method.AttributeLists.SelectMany(list => list.Attributes)
                        .Select(attribute => (Attribute: attribute, Verb: GetHttpVerb(attribute.Name.ToString())))
                        .FirstOrDefault(item => item.Verb != null);
                    if (httpAttribute.Verb == null) continue;

                    var actionRoute = GetRouteTemplate(method.AttributeLists.SelectMany(list => list.Attributes));
                    if (actionRoute == null)
                    {
                        actionRoute = GetRouteTemplate(method.AttributeLists.SelectMany(list => list.Attributes), httpAttribute.Attribute);
                    }
                    var controllerName = name.EndsWith("Controller", System.StringComparison.Ordinal)
                        ? name[..^"Controller".Length]
                        : name;
                    var route = CombineRoutes(controllerRoute, actionRoute, controllerName, method.Identifier.ValueText);
                    var endpointId = StableIdentifier.CreateCSharpSymbolId(filePath, $"{qualifiedName}.{method.Identifier.ValueText}:{httpAttribute.Verb}:{route}");
                    symbols.Add(new CodeSymbol(endpointId, $"{httpAttribute.Verb} {route}", ModelSymbolKind.Endpoint, "csharp", filePath,
                        ToLocation(method.GetLocation()), new List<CodeMember>(), new Dictionary<string, string>
                        {
                            ["httpMethod"] = httpAttribute.Verb,
                            ["route"] = route,
                            ["controllerId"] = typeId,
                            ["action"] = method.Identifier.ValueText
                        }));
                }
            }
        }

        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) continue;
            var methodName = memberAccess.Name.Identifier.ValueText;
            var verb = methodName switch
            {
                "MapGet" => "GET",
                "MapPost" => "POST",
                "MapPut" => "PUT",
                "MapDelete" => "DELETE",
                "MapPatch" => "PATCH",
                "MapMethods" => "ANY",
                _ => null
            };
            if (verb == null || invocation.ArgumentList.Arguments.Count == 0 ||
                invocation.ArgumentList.Arguments[0].Expression is not LiteralExpressionSyntax routeLiteral ||
                !routeLiteral.IsKind(SyntaxKind.StringLiteralExpression)) continue;

            var route = NormalizeRoute(routeLiteral.Token.ValueText);
            var endpointId = StableIdentifier.CreateCSharpSymbolId(filePath, $"minimal:{verb}:{route}");
            symbols.Add(new CodeSymbol(endpointId, $"{verb} {route}", ModelSymbolKind.Endpoint, "csharp", filePath,
                ToLocation(invocation.GetLocation()), new List<CodeMember>(), new Dictionary<string, string>
                {
                    ["httpMethod"] = verb,
                    ["route"] = route,
                    ["endpointStyle"] = "minimal-api"
                }));
        }
        return Task.FromResult<IEnumerable<CodeSymbol>>(symbols);
    }

    private static string? GetHttpVerb(string attributeName)
    {
        var name = attributeName.Split('.').Last().Replace("Attribute", string.Empty);
        return name switch
        {
            "HttpGet" => "GET",
            "HttpPost" => "POST",
            "HttpPut" => "PUT",
            "HttpDelete" => "DELETE",
            "HttpPatch" => "PATCH",
            "HttpHead" => "HEAD",
            "HttpOptions" => "OPTIONS",
            "HttpTrace" => "TRACE",
            _ => null
        };
    }

    private static string? GetRouteTemplate(IEnumerable<AttributeSyntax> attributes, AttributeSyntax? fallbackAttribute = null)
    {
        var routeAttribute = attributes.FirstOrDefault(attribute =>
            attribute.Name.ToString().Split('.').Last().Replace("Attribute", string.Empty) == "Route");
        var routeExpression = routeAttribute?.ArgumentList?.Arguments.FirstOrDefault()?.Expression ??
            fallbackAttribute?.ArgumentList?.Arguments.FirstOrDefault()?.Expression;
        if (routeExpression is LiteralExpressionSyntax literal &&
            literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return literal.Token.ValueText;
        }
        return null;
    }

    private static string CombineRoutes(string? prefix, string? route, string controllerName, string actionName)
    {
        var combined = string.IsNullOrWhiteSpace(prefix)
            ? NormalizeRoute(route)
            : string.IsNullOrWhiteSpace(route)
                ? NormalizeRoute(prefix)
                : route.StartsWith("~/", System.StringComparison.Ordinal) || route.StartsWith("/", System.StringComparison.Ordinal)
                    ? NormalizeRoute(route.TrimStart('~', '/'))
                    : NormalizeRoute($"{prefix}/{route}");
        return combined
            .Replace("[controller]", controllerName, System.StringComparison.OrdinalIgnoreCase)
            .Replace("[action]", actionName, System.StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRoute(string? route) => (route ?? string.Empty).Trim('/');

    private static CodeMember CreateMember(MemberDeclarationSyntax member, string filePath, SyntaxTree tree)
    {
        var name = member switch
        {
            MethodDeclarationSyntax m => m.Identifier.ValueText,
            ConstructorDeclarationSyntax c => c.Identifier.ValueText,
            PropertyDeclarationSyntax p => p.Identifier.ValueText,
            FieldDeclarationSyntax f => string.Join(",", f.Declaration.Variables.Select(v => v.Identifier.ValueText)),
            _ => member.Kind().ToString()
        };
        var kind = member switch
        {
            MethodDeclarationSyntax => MemberKind.Method,
            ConstructorDeclarationSyntax => MemberKind.Constructor,
            PropertyDeclarationSyntax => MemberKind.Property,
            _ => MemberKind.Field
        };
        var parameters = member switch
        {
            BaseMethodDeclarationSyntax method => method.ParameterList.Parameters.Select(p => p.ToString()).ToList(),
            _ => new List<string>()
        };
        var returnType = member is MethodDeclarationSyntax methodDeclaration ? methodDeclaration.ReturnType.ToString() : null;
        return new CodeMember($"csharp:{filePath}:{name}", name, kind, ToLocation(member.GetLocation()), parameters, returnType);
    }

    private static SourceLocation ToLocation(Location location)
    {
        var span = location.GetLineSpan();
        return new SourceLocation(span.StartLinePosition.Line + 1, span.EndLinePosition.Line + 1, span.StartLinePosition.Character + 1, span.EndLinePosition.Character + 1);
    }
}

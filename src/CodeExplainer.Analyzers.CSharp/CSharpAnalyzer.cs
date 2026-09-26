using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeExplainer.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
            var kind = type is InterfaceDeclarationSyntax ? SymbolKind.Interface : SymbolKind.Class;
            var metadata = new Dictionary<string, string> { ["namespace"] = namespaceName };
            if (type.BaseList != null) metadata["baseTypes"] = string.Join(",", type.BaseList.Types.Select(t => t.Type.ToString()));
            var members = type.Members.Select(member => CreateMember(member, filePath, tree)).ToList();
            symbols.Add(new CodeSymbol($"csharp:{qualifiedName}", name, kind, "csharp", filePath, ToLocation(type.GetLocation()), members, metadata));
        }
        return Task.FromResult<IEnumerable<CodeSymbol>>(symbols);
    }

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

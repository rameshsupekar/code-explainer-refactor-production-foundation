using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CodeExplainer.Core.Models;
using CodeExplainer.Core.Utilities;

namespace CodeExplainer.Cli;

internal sealed class BehaviorFeatureBuilder
{
    private static readonly Regex FeaturePattern = new("^\\s*Feature:\\s*(?<name>.+?)\\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ScenarioPattern = new("^\\s*Scenario(?:\\s+Outline)?:\\s*(?<name>.+?)\\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex StepPattern = new("^\\s*(?:Given|When|Then|And|But|\\*)\\s+(?<step>.+?)\\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TagPattern = new("^\\s*(?<tags>(?:@[^\\s]+\\s*)+)\\s*$", RegexOptions.Compiled);
    private static readonly Regex TagTokenPattern = new("@[^\\s]+", RegexOptions.Compiled);

    public async Task<IReadOnlyList<BehaviorFeature>> BuildAsync(
        string rootPath,
        string outputPath,
        ProjectInfo? projectInfo,
        CodeModel model,
        CancellationToken cancellationToken)
    {
        var features = new List<BehaviorFeature>();
        if (projectInfo != null)
        {
            foreach (var relativePath in projectInfo.BehaviorSpecPaths.OrderBy(path => path, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fullPath = Path.Combine(rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
                try
                {
                    var lines = await File.ReadAllLinesAsync(fullPath, cancellationToken).ConfigureAwait(false);
                    features.Add(ParseFeature(relativePath, lines, model));
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    continue;
                }
            }
        }

        var endpointScenarios = model.Symbols
            .Where(symbol => symbol.Kind == SymbolKind.Endpoint)
            .OrderBy(symbol => symbol.Id, StringComparer.Ordinal)
            .Select(endpoint => CreateEndpointDraft(endpoint, model))
            .ToArray();
        if (endpointScenarios.Length > 0)
        {
            features.Add(new BehaviorFeature(
                "Detected API endpoint behavior drafts",
                Path.GetRelativePath(outputPath, Path.Combine(outputPath, "behavior", "drafts", "api-endpoints.feature")).Replace('\\', '/'),
                endpointScenarios,
                IsGeneratedDraft: true));
        }

        return features;
    }

    public static string WriteGherkin(BehaviorFeature feature)
    {
        var builder = new StringBuilder();
        if (feature.IsGeneratedDraft)
            builder.AppendLine("# Generated from static analysis; review and replace with business outcomes before treating as a specification.");
        foreach (var scenario in feature.Scenarios)
        {
            if (scenario.Tags.Count > 0)
                builder.AppendLine(string.Join(" ", scenario.Tags.Select(tag => tag.StartsWith('@') ? tag : $"@{tag}")));
            builder.AppendLine($"Scenario: {scenario.Name}");
            foreach (var step in scenario.Steps)
                builder.AppendLine($"  {step}");
            builder.AppendLine();
        }

        return $"Feature: {feature.Name}\n\n{builder}";
    }

    private static BehaviorFeature ParseFeature(string sourcePath, IEnumerable<string> lines, CodeModel model)
    {
        var name = Path.GetFileNameWithoutExtension(sourcePath);
        var featureTags = new List<string>();
        var pendingTags = new List<string>();
        var scenarios = new List<BehaviorScenario>();
        string? scenarioName = null;
        var scenarioSteps = new List<string>();

        void CompleteScenario()
        {
            if (scenarioName == null)
                return;
            var tags = featureTags.Concat(pendingTags).Distinct(StringComparer.Ordinal).ToArray();
            scenarios.Add(new BehaviorScenario(
                scenarioName,
                tags,
                scenarioSteps.ToArray(),
                FindRelatedSymbols($"{name} {scenarioName} {string.Join(' ', scenarioSteps)}", tags, model),
                IsGeneratedDraft: false));
            scenarioName = null;
            scenarioSteps.Clear();
            pendingTags.Clear();
        }

        foreach (var line in lines)
        {
            var featureMatch = FeaturePattern.Match(line);
            if (featureMatch.Success)
            {
                name = featureMatch.Groups["name"].Value.Trim();
                continue;
            }

            var tagMatch = TagPattern.Match(line);
            if (tagMatch.Success)
            {
                var tags = TagTokenPattern.Matches(tagMatch.Groups["tags"].Value)
                    .Select(match => match.Value)
                    .ToArray();
                if (scenarioName == null && scenarios.Count == 0)
                    featureTags.AddRange(tags);
                else
                    pendingTags.AddRange(tags);
                continue;
            }

            var scenarioMatch = ScenarioPattern.Match(line);
            if (scenarioMatch.Success)
            {
                CompleteScenario();
                scenarioName = scenarioMatch.Groups["name"].Value.Trim();
                continue;
            }

            if (scenarioName == null)
                continue;
            var stepMatch = StepPattern.Match(line);
            if (stepMatch.Success)
                scenarioSteps.Add(line.Trim());
            else if (line.TrimStart().StartsWith('|'))
                scenarioSteps.Add(line.Trim());
        }
        CompleteScenario();

        return new BehaviorFeature(name, sourcePath, scenarios, IsGeneratedDraft: false);
    }

    private static BehaviorScenario CreateEndpointDraft(CodeSymbol endpoint, CodeModel model)
    {
        var verb = endpoint.Metadata.TryGetValue("httpMethod", out var method) ? method : "HTTP";
        var route = endpoint.Metadata.TryGetValue("route", out var template) ? template : endpoint.Name;
        var displayName = $"{verb} {route}".Trim();
        return new BehaviorScenario(
            $"{displayName} reaches its handler (draft)",
            new[] { "@draft", "@review_required", "@reqnroll", "@cucumberjs", $"@code_{StableIdentifier.CreateDiagramNodeId(endpoint.Id).ToLowerInvariant()}" },
            new[]
            {
                $"When a client sends a {verb} request to \"{route}\"",
                "Then the endpoint handler is invoked"
            },
            GetImplementationSymbolIds(endpoint, model),
            IsGeneratedDraft: true);
    }

    private static IReadOnlyList<string> FindRelatedSymbols(string text, IReadOnlyList<string> tags, CodeModel model)
    {
        var normalizedText = Normalize(text);
        var explicitNodes = tags
            .Where(tag => tag.StartsWith("@code_", StringComparison.OrdinalIgnoreCase))
            .Select(tag => tag[6..])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var matchedSymbols = model.Symbols
            .Where(symbol => explicitNodes.Contains(StableIdentifier.CreateDiagramNodeId(symbol.Id)))
            .Concat(model.Symbols.Where(symbol =>
            {
                var candidates = new List<string> { symbol.Name };
                if (symbol.Metadata.TryGetValue("route", out var route)) candidates.Add(route);
                if (symbol.Metadata.TryGetValue("action", out var action)) candidates.Add(action);
                return candidates.Any(candidate =>
                {
                    var normalizedCandidate = Normalize(candidate);
                    return normalizedCandidate.Length >= 4 && normalizedText.Contains(normalizedCandidate, StringComparison.Ordinal);
                });
            }))
            .ToArray();
        return matchedSymbols
            .SelectMany(symbol => GetImplementationSymbolIds(symbol, model))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> GetImplementationSymbolIds(CodeSymbol symbol, CodeModel model)
    {
        if (symbol.Metadata.TryGetValue("controllerId", out var controllerId) &&
            model.Symbols.Any(candidate => candidate.Id == controllerId))
            return new[] { symbol.Id, controllerId };
        return new[] { symbol.Id };
    }

    private static string Normalize(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}

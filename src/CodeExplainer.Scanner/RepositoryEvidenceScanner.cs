using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using CodeExplainer.Core.Models;

namespace CodeExplainer.Scanner;

internal static class RepositoryEvidenceScanner
{
    private const int MaximumManifestLength = 1024 * 1024;
    private static readonly Regex TerraformResource = new("resource\\s+\"(?<type>(?:azurerm|aws|google|oci)_[^\"]+)\"", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BicepResource = new("resource\\s+\\w+\\s+'(?<type>Microsoft\\.[^']+)'", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ImageReference = new("(?m)^\\s*image:\\s*['\"]?(?<image>[A-Za-z0-9._:/@-]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool IsConfigurationFile(string path)
    {
        var name = Path.GetFileName(path);
        var extension = Path.GetExtension(path);
        var normalizedPath = path.Replace('\\', '/');
        return extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".sln", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".tf", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".bicep", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Directory.Packages.props", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Directory.Build.props", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("global.json", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("package.json", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("package-lock.json", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("pnpm-lock.yaml", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("yarn.lock", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("tsconfig", StringComparison.OrdinalIgnoreCase) && extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase) && extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("docker-compose", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("compose.yaml", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("compose.yml", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) && IsInfrastructureOrWorkflowPath(normalizedPath) ||
            extension.Equals(".yml", StringComparison.OrdinalIgnoreCase) && IsInfrastructureOrWorkflowPath(normalizedPath);
    }

    public static async Task<IReadOnlyList<RepositoryEvidence>> ScanAsync(
        IEnumerable<string> paths,
        string rootPath,
        CancellationToken cancellationToken)
    {
        var evidence = new Dictionary<string, RepositoryEvidence>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths.OrderBy(value => value, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = Path.GetRelativePath(rootPath, path).Replace('\\', '/');
            Add(evidence, Path.GetFileName(path), "Configuration", relativePath, "configuration file", "Observed");
            string content;
            try
            {
                if (new FileInfo(path).Length > MaximumManifestLength)
                    continue;
                content = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                continue;
            }

            var extension = Path.GetExtension(path);
            if (extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(path).Equals("Directory.Build.props", StringComparison.OrdinalIgnoreCase))
                ScanDotNetProject(content, relativePath, evidence);
            else if (Path.GetFileName(path).Equals("package.json", StringComparison.OrdinalIgnoreCase))
                ScanNodePackage(content, relativePath, evidence);
            else if (Path.GetFileName(path).StartsWith("tsconfig", StringComparison.OrdinalIgnoreCase))
                Add(evidence, "TypeScript", "Language", relativePath, "compiler configuration", "Observed");
            else if (Path.GetFileName(path).StartsWith("appsettings", StringComparison.OrdinalIgnoreCase))
                ScanApplicationSettings(content, relativePath, evidence);
            else if (extension.Equals(".tf", StringComparison.OrdinalIgnoreCase))
                ScanTerraform(content, relativePath, evidence);
            else if (extension.Equals(".bicep", StringComparison.OrdinalIgnoreCase))
                ScanBicep(content, relativePath, evidence);
            else if (Path.GetFileName(path).Equals("Dockerfile", StringComparison.OrdinalIgnoreCase) ||
                     Path.GetFileName(path).StartsWith("docker-compose", StringComparison.OrdinalIgnoreCase) ||
                     Path.GetFileName(path).StartsWith("compose.", StringComparison.OrdinalIgnoreCase) ||
                     extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) ||
                     extension.Equals(".yml", StringComparison.OrdinalIgnoreCase))
                ScanContainerManifest(content, relativePath, evidence);
        }

        return evidence.Values
            .OrderBy(item => item.Category, StringComparer.Ordinal)
            .ThenBy(item => item.Name, StringComparer.Ordinal)
            .ThenBy(item => item.SourcePath, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsInfrastructureOrWorkflowPath(string path) =>
        path.Contains("/.github/workflows/", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("/k8s/", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("/kubernetes/", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("/helm/", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("/charts/", StringComparison.OrdinalIgnoreCase);

    private static void ScanDotNetProject(string content, string sourcePath, IDictionary<string, RepositoryEvidence> evidence)
    {
        try
        {
            var document = XDocument.Parse(content);
            foreach (var framework in document.Descendants().Where(element => element.Name.LocalName is "TargetFramework" or "TargetFrameworks"))
            {
                foreach (var value in framework.Value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                    Add(evidence, $".NET {value}", "Runtime", sourcePath, "project target framework", "Observed");
            }

            foreach (var sdk in document.Descendants().Attributes().Where(attribute => attribute.Name.LocalName == "Sdk"))
            {
                if (sdk.Value.Contains("Web", StringComparison.OrdinalIgnoreCase))
                    Add(evidence, "ASP.NET Core", "Framework", sourcePath, "project SDK", "Observed");
            }

            foreach (var package in document.Descendants().Where(element => element.Name.LocalName == "PackageReference"))
            {
                var name = (string?)package.Attribute("Include") ?? (string?)package.Attribute("Update");
                if (name != null)
                    AddKnownPackage(name, sourcePath, evidence);
            }
        }
        catch (System.Xml.XmlException)
        {
        }
    }

    private static void ScanNodePackage(string content, string sourcePath, IDictionary<string, RepositoryEvidence> evidence)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            foreach (var section in new[] { "dependencies", "devDependencies", "peerDependencies" })
            {
                if (!document.RootElement.TryGetProperty(section, out var packages) || packages.ValueKind != JsonValueKind.Object)
                    continue;
                foreach (var package in packages.EnumerateObject())
                    AddKnownPackage(package.Name, sourcePath, evidence);
            }
        }
        catch (JsonException)
        {
        }
    }

    private static void ScanApplicationSettings(string content, string sourcePath, IDictionary<string, RepositoryEvidence> evidence)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            VisitJsonKeys(document.RootElement, sourcePath, evidence);
        }
        catch (JsonException)
        {
        }
    }

    private static void VisitJsonKeys(JsonElement element, string sourcePath, IDictionary<string, RepositoryEvidence> evidence)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var key = property.Name;
                if (key.Contains("redis", StringComparison.OrdinalIgnoreCase))
                    Add(evidence, "Redis", "Cache", sourcePath, $"configuration key: {key}", "Inferred");
                if (key.Contains("servicebus", StringComparison.OrdinalIgnoreCase))
                    Add(evidence, "Azure Service Bus", "Message broker", sourcePath, $"configuration key: {key}", "Inferred");
                if (key.Contains("rabbitmq", StringComparison.OrdinalIgnoreCase))
                    Add(evidence, "RabbitMQ", "Message broker", sourcePath, $"configuration key: {key}", "Inferred");
                if (key.Contains("kafka", StringComparison.OrdinalIgnoreCase))
                    Add(evidence, "Apache Kafka", "Message broker", sourcePath, $"configuration key: {key}", "Inferred");
                if (key.Contains("cosmos", StringComparison.OrdinalIgnoreCase))
                    Add(evidence, "Azure Cosmos DB", "Database", sourcePath, $"configuration key: {key}", "Inferred");
                if (key.Contains("storage", StringComparison.OrdinalIgnoreCase) || key.Contains("blob", StringComparison.OrdinalIgnoreCase))
                    Add(evidence, "Object storage", "Storage", sourcePath, $"configuration key: {key}", "Inferred");
                VisitJsonKeys(property.Value, sourcePath, evidence);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
                VisitJsonKeys(child, sourcePath, evidence);
        }
    }

    private static void ScanTerraform(string content, string sourcePath, IDictionary<string, RepositoryEvidence> evidence)
    {
        foreach (Match match in TerraformResource.Matches(content))
        {
            var resourceType = match.Groups["type"].Value;
            var provider = resourceType.StartsWith("azurerm_", StringComparison.OrdinalIgnoreCase) ? "Azure" :
                resourceType.StartsWith("aws_", StringComparison.OrdinalIgnoreCase) ? "AWS" :
                resourceType.StartsWith("google_", StringComparison.OrdinalIgnoreCase) ? "Google Cloud" : "Oracle Cloud";
            Add(evidence, $"{provider} resource {resourceType}", "Cloud resource", sourcePath, "Terraform resource declaration", "Observed");
        }
    }

    private static void ScanBicep(string content, string sourcePath, IDictionary<string, RepositoryEvidence> evidence)
    {
        foreach (Match match in BicepResource.Matches(content))
            Add(evidence, $"Azure resource {match.Groups["type"].Value}", "Cloud resource", sourcePath, "Bicep resource declaration", "Observed");
    }

    private static void ScanContainerManifest(string content, string sourcePath, IDictionary<string, RepositoryEvidence> evidence)
    {
        foreach (Match match in ImageReference.Matches(content))
            Add(evidence, $"Container image {match.Groups["image"].Value}", "Container", sourcePath, "container image declaration", "Observed");
        if (sourcePath.Contains("/.github/workflows/", StringComparison.OrdinalIgnoreCase))
            Add(evidence, "GitHub Actions", "CI/CD", sourcePath, "workflow file", "Observed");
        if (Path.GetFileName(sourcePath).Equals("Dockerfile", StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileName(sourcePath).StartsWith("docker-compose", StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileName(sourcePath).StartsWith("compose.", StringComparison.OrdinalIgnoreCase))
            Add(evidence, "Docker", "Container platform", sourcePath, "container manifest", "Observed");
    }

    private static void AddKnownPackage(string package, string sourcePath, IDictionary<string, RepositoryEvidence> evidence)
    {
        var name = package.ToLowerInvariant() switch
        {
            "microsoft.entityframeworkcore" => ("Entity Framework Core", "Data access"),
            "npgsql.entityframeworkcore.postgresql" or "npgsql" => ("PostgreSQL", "Database"),
            "microsoft.data.sqlclient" => ("SQL Server", "Database"),
            "stackexchange.redis" => ("Redis", "Cache"),
            "rabbitmq.client" => ("RabbitMQ", "Message broker"),
            "masstransit" => ("MassTransit", "Messaging"),
            "@angular/core" => ("Angular", "Framework"),
            "react" => ("React", "Framework"),
            "react-dom" => ("React DOM", "Framework"),
            "@nestjs/core" => ("NestJS", "Framework"),
            "express" => ("Express", "Framework"),
            "typescript" => ("TypeScript", "Language"),
            "@azure/identity" or "azure.identity" => ("Microsoft Azure SDK", "Cloud platform"),
            _ when package.StartsWith("Azure.", StringComparison.OrdinalIgnoreCase) || package.StartsWith("@azure/", StringComparison.OrdinalIgnoreCase) => ("Microsoft Azure SDK", "Cloud platform"),
            _ when package.StartsWith("AWSSDK.", StringComparison.OrdinalIgnoreCase) || package.StartsWith("@aws-sdk/", StringComparison.OrdinalIgnoreCase) => ("AWS SDK", "Cloud platform"),
            _ when package.StartsWith("Google.Cloud.", StringComparison.OrdinalIgnoreCase) || package.StartsWith("@google-cloud/", StringComparison.OrdinalIgnoreCase) => ("Google Cloud SDK", "Cloud platform"),
            _ => (string.Empty, string.Empty)
        };
        if (name.Item1.Length > 0)
            Add(evidence, name.Item1, name.Item2, sourcePath, $"declared package: {package}", "Observed");
    }

    private static void Add(
        IDictionary<string, RepositoryEvidence> evidence,
        string name,
        string category,
        string sourcePath,
        string evidenceType,
        string confidence)
    {
        var key = $"{category}|{name}|{sourcePath}|{evidenceType}";
        evidence.TryAdd(key, new RepositoryEvidence(name, category, sourcePath, evidenceType, confidence));
    }
}

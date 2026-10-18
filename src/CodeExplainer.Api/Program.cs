using CodeExplainer.Api.Services;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// Use repository root logs folder (two levels up from API project folder)
var repoRoot = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", ".."));
var logsDir = Path.Combine(repoRoot, "logs");
Directory.CreateDirectory(logsDir);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Strict CORS - only allow the expected development origins
builder.Services.AddCors(options =>
{
    options.AddPolicy("local", policy =>
    {
        policy.WithOrigins("http://localhost:4200", "http://127.0.0.1:4200", "http://127.0.0.1:5002", "http://127.0.0.1:5000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddSingleton<IDiagramService, LocalDiagramService>();

var app = builder.Build();

// Security headers and CSP
var csp = string.Join("; ", new[] {
    "default-src 'self'",
    "script-src 'self' 'unsafe-inline' 'unsafe-eval' blob:",
    "connect-src 'self' http://127.0.0.1:5000 ws:",
    "style-src 'self' 'unsafe-inline'",
    "img-src 'self' data: blob:",
    "font-src 'self' data:",
    "frame-ancestors 'none'",
    "base-uri 'self'",
});

app.Use(async (context, next) =>
{
    // Add common security headers
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=()";
    context.Response.Headers["Content-Security-Policy"] = csp;

    // HSTS only when SSL is used
    if (context.Request.IsHttps)
    {
        context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    }

    // Cache policy: no-store for dynamic API endpoints, aggressive for static assets
    void SetNoStore() => context.Response.Headers["Cache-Control"] = "no-store";
    void SetStaticCache() => context.Response.Headers["Cache-Control"] = "public, max-age=31536000, immutable";

    var path = context.Request.Path.Value ?? string.Empty;
    var ext = Path.GetExtension(path).ToLowerInvariant();
    if (path.StartsWith("/api/") || context.Request.Method != "GET")
    {
        SetNoStore();
    }
    else if (ext == ".js" || ext == ".css" || ext == ".wasm" || ext == ".json" || ext == ".svg" || ext == ".png" || ext == ".jpg" || ext == ".woff2" || ext == ".woff" || ext == ".ttf")
    {
        SetStaticCache();
    }
    else
    {
        // default: no-cache for HTML pages
        context.Response.Headers["Cache-Control"] = "no-cache, must-revalidate, max-age=0";
    }

    await next();
});

// Simple request logging middleware that writes to logs/requests.log and errors.log
app.Use(async (context, next) =>
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    try
    {
        await next();
        sw.Stop();
        var line = $"{DateTime.UtcNow:o} {context.Request.Method} {context.Request.Path} responded {context.Response.StatusCode} in {sw.ElapsedMilliseconds}ms";
        File.AppendAllText(Path.Combine(logsDir, "requests.log"), line + Environment.NewLine);
    }
    catch (Exception ex)
    {
        sw.Stop();
        var err = $"{DateTime.UtcNow:o} ERROR {context.Request.Method} {context.Request.Path} in {sw.ElapsedMilliseconds}ms: {ex}\n";
        File.AppendAllText(Path.Combine(logsDir, "errors.log"), err + Environment.NewLine);
        throw;
    }
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("local");
app.UseRouting();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/logs", async (HttpRequest req) =>
{
    try
    {
        using var reader = new StreamReader(req.Body);
        var body = await reader.ReadToEndAsync();
        var fileName = Path.Combine(logsDir, $"client-{DateTime.UtcNow:yyyyMMddHHmmssfff}.log");
        await File.WriteAllTextAsync(fileName, body);
        return Results.Ok(new { saved = fileName });
    }
    catch (Exception ex)
    {
        var err = $"{DateTime.UtcNow:o} Failed to save client log: {ex}\n";
        File.AppendAllText(Path.Combine(logsDir, "errors.log"), err + Environment.NewLine);
        return Results.StatusCode(500);
    }
});

app.Run();

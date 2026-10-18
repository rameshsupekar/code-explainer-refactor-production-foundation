using CodeExplainer.Api.Services;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// Ensure logs directory exists
var logsDir = Path.Combine(builder.Environment.ContentRootPath, "logs");
Directory.CreateDirectory(logsDir);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy("local", policy =>
    {
        policy.WithOrigins("http://localhost:4200", "http://127.0.0.1:4200", "http://localhost:5000", "http://127.0.0.1:5000", "https://localhost:5001")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});
builder.Services.AddSingleton<IDiagramService, LocalDiagramService>();

var app = builder.Build();

// Simple request logging middleware that writes to logs/requests.log
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

// Endpoint for clients (Blazor/Angular) to POST logs to the server
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

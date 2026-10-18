using CodeExplainer.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy("local", policy =>
    {
        policy.WithOrigins("http://localhost:5000", "http://localhost:5001", "https://localhost:5001")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});
builder.Services.AddSingleton<IDiagramService, LocalDiagramService>();

var app = builder.Build();

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

app.Run();

using System;
using System.Net.Http;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using CodeExplainer.BlazorViewer;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

var apiBaseAddress = builder.Configuration["ApiBaseUrl"] ?? "http://127.0.0.1:5000/";
builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(apiBaseAddress, UriKind.Absolute)
});

await builder.Build().RunAsync();

using IFS.AIWeb.Application;
using IFS.AIWeb.Infrastructure;

const string DevelopmentCorsPolicy = "DevelopmentCors";

var builder = WebApplication.CreateBuilder(args);
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddCors(options =>
{
    options.AddPolicy(DevelopmentCorsPolicy, policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseCors(DevelopmentCorsPolicy);
}

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }))
    .AllowAnonymous();

app.Run();

public partial class Program;

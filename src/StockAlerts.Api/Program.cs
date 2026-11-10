using System.Text.Json.Serialization;
using Microsoft.OpenApi;
using StockAlerts.Api.Workers;
using StockAlerts.Core.StockChecks;
using StockAlerts.Infrastructure;
using StockAlerts.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddInfrastructure();
builder.Services.AddScoped<StockCheckService>();

builder.Services.AddOptions<StockCheckOptions>()
    .BindConfiguration(StockCheckOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddHostedService<StockCheckWorker>();

builder.Services.AddAuthorization();
builder.Services.AddControllers()
    .AddJsonOptions(json => json.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Unhandled exceptions and empty error responses (401, 403, 404...) are returned as ProblemDetails.
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(swagger =>
{
    swagger.SwaggerDoc("v1", new OpenApiInfo { Title = "Stock Alerts API", Version = "v1" });

    const string schemeId = "Bearer";
    swagger.AddSecurityDefinition(schemeId, new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Access token returned by /api/auth/login.",
    });
    swagger.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference(schemeId, document)] = [],
    });
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

// Applying migrations at startup keeps the sample to one command. With several instances or a
// restricted database user, this would be a separate deployment step instead.
await app.Services.InitializeDatabaseAsync();

app.Run();

// Lets the integration tests reference the entry point through WebApplicationFactory<Program>.
public partial class Program;

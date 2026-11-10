using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StockAlerts.Api.Contracts;
using StockAlerts.Core.StockChecks;

namespace StockAlerts.IntegrationTests;

/// <summary>
/// The real application on an in-memory SQLite database, with the background worker switched off and a
/// scripted stock source, so tests decide when a check runs and what it finds.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private const string AdminEmail = "admin@tests.local";
    private const string AdminPassword = "integration-tests-only";

    private readonly string _connectionString = $"Data Source=tests-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
    private readonly SqliteConnection _keepAlive;

    public ApiFactory()
    {
        // An in-memory SQLite database disappears when its last connection closes. This one stays open
        // for the lifetime of the factory.
        _keepAlive = new SqliteConnection(_connectionString);
        _keepAlive.Open();
    }

    public ScriptedStockSource StockSource { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not Development on purpose: nothing comes from appsettings.Development.json.
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("ConnectionStrings:Default", _connectionString);
        builder.UseSetting("Jwt:Key", "integration-tests-signing-key-0123456789");
        builder.UseSetting("Seed:AdminEmail", AdminEmail);
        builder.UseSetting("Seed:AdminPassword", AdminPassword);
        builder.UseSetting("StockCheck:Enabled", "false");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IStockSource>();
            services.AddSingleton<IStockSource>(StockSource);
        });
    }

    /// <summary>Runs one stock check pass, exactly as the background worker would.</summary>
    public async Task<StockCheckSummary> RunStockCheckAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<StockCheckService>();
        return await service.RunAsync(CancellationToken.None);
    }

    public Task<HttpClient> CreateAdminClientAsync() =>
        CreateAuthenticatedClientAsync("/api/auth/login", new LoginRequest(AdminEmail, AdminPassword));

    public Task<HttpClient> CreateUserClientAsync() =>
        CreateAuthenticatedClientAsync(
            "/api/auth/register", new RegisterRequest($"user-{Guid.NewGuid():N}@tests.local", "a-valid-password"));

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string url, object credentials)
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync(url, credentials);
        response.EnsureSuccessStatusCode();
        var auth = await response.ReadAsync<AuthResponse>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _keepAlive.Dispose();
        }
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using StockAlerts.Api.Contracts;

namespace StockAlerts.IntegrationTests;

public class AuthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Password = "a-valid-password";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Register_returns_a_token_that_gives_access_to_protected_endpoints()
    {
        var auth = await _client.CreateAsync<AuthResponse>("/api/auth/register", new RegisterRequest(NewEmail(), Password));

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var response = await _client.GetAsync("/api/subscriptions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(auth.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Register_rejects_an_email_that_is_already_taken_regardless_of_case()
    {
        var email = NewEmail();
        await _client.CreateAsync<AuthResponse>("/api/auth/register", new RegisterRequest(email, Password));

        var response = await _client.PostAsJsonAsync(
            "/api/auth/register", new RegisterRequest(email.ToUpperInvariant(), Password));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Register_with_invalid_data_returns_validation_errors_per_field()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/register", new RegisterRequest("not-an-email", "short"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadAsync<ValidationProblemDetails>();
        Assert.Contains(nameof(RegisterRequest.Email), problem.Errors.Keys);
        Assert.Contains(nameof(RegisterRequest.Password), problem.Errors.Keys);
    }

    [Fact]
    public async Task Login_returns_a_token_for_valid_credentials()
    {
        var email = NewEmail();
        await _client.CreateAsync<AuthResponse>("/api/auth/register", new RegisterRequest(email, Password));

        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty((await response.ReadAsync<AuthResponse>()).AccessToken);
    }

    [Fact]
    public async Task Login_gives_the_same_401_for_a_wrong_password_and_for_an_unknown_email()
    {
        var email = NewEmail();
        await _client.CreateAsync<AuthResponse>("/api/auth/register", new RegisterRequest(email, Password));

        var wrongPassword = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "wrong-password"));
        var unknownEmail = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(NewEmail(), Password));

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal(
            (await wrongPassword.ReadAsync<ProblemDetails>()).Title,
            (await unknownEmail.ReadAsync<ProblemDetails>()).Title);
    }

    [Fact]
    public async Task Protected_endpoint_without_a_token_returns_401_as_problem_details()
    {
        var response = await _client.GetAsync("/api/alerts");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Regular_user_cannot_use_admin_endpoints()
    {
        var user = await factory.CreateUserClientAsync();

        var response = await user.PostAsJsonAsync(
            "/api/stores", new StoreRequest("Some store", "https://store.example"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@tests.local";
}

using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace StockAlerts.IntegrationTests;

public class HostTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Health_endpoint_reports_healthy_when_the_database_is_reachable()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public void Application_refuses_to_start_outside_Development_without_a_JWT_key()
    {
        using var withoutKey = factory.WithWebHostBuilder(builder => builder.UseSetting("Jwt:Key", ""));

        var exception = Assert.Throws<OptionsValidationException>(() => withoutKey.CreateClient());

        Assert.Contains("Jwt:Key is required", exception.Message);
    }
}

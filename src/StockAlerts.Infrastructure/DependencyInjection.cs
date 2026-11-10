using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using StockAlerts.Core.Entities;
using StockAlerts.Core.StockChecks;
using StockAlerts.Infrastructure.Auth;
using StockAlerts.Infrastructure.Persistence;
using StockAlerts.Infrastructure.StockSources;

namespace StockAlerts.Infrastructure;

public enum DatabaseProvider
{
    SqlServer,
    Sqlite,
}

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddPersistence();
        services.AddJwtAuthentication();

        services.AddSingleton<IStockSource, SimulatedStockSource>();
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

        return services;
    }

    private static void AddPersistence(this IServiceCollection services)
    {
        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString("Default");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("ConnectionStrings:Default is not configured.");
            }

            switch (configuration.GetValue<DatabaseProvider>("Database:Provider"))
            {
                case DatabaseProvider.Sqlite:
                    options.UseSqlite(connectionString);
                    break;
                default:
                    options.UseSqlServer(connectionString);
                    break;
            }
        });

        services.AddScoped<IStockCheckRepository, StockCheckRepository>();
        services.AddOptions<SeedOptions>().BindConfiguration(SeedOptions.SectionName);
    }

    private static void AddJwtAuthentication(this IServiceCollection services)
    {
        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<JwtTokenService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                // Keep the claim names exactly as issued ("sub", "role") instead of the legacy XML-style ones.
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Value.Issuer,
                    ValidAudience = jwt.Value.Audience,
                    IssuerSigningKey = JwtTokenService.CreateSigningKey(jwt.Value),
                    NameClaimType = JwtRegisteredClaimNames.Email,
                    RoleClaimType = JwtTokenService.RoleClaim,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });
    }
}

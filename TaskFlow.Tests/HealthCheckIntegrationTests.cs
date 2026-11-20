using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using TaskFlow.API.Data;
using Xunit;

namespace TaskFlow.Tests;

public class HealthCheckIntegrationTests
{
    [Theory]
    [InlineData("/api/health")]
    [InlineData("/api/health/live")]
    public async Task LivenessEndpoint_ReturnsHealthyWithoutDatabaseDependency(string path)
    {
        using var factory = new TaskFlowWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ReadinessEndpoint_ReturnsReadyWhenDatabaseCanConnect()
    {
        using var factory = new TaskFlowWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("Ready", document.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task ReadinessEndpoint_ReturnsUnavailableWithoutDatabaseDetailsWhenDatabaseCannotConnect()
    {
        using var factory = new UnavailableDatabaseFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://api.taskflow-test.example")
        });
        using var response = await client.GetAsync("/api/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("max-age=", response.Headers.GetValues("Strict-Transport-Security").Single());
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Data Source", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception", body, StringComparison.OrdinalIgnoreCase);
        using var document = JsonDocument.Parse(body);
        Assert.Equal("NotReady", document.RootElement.GetProperty("status").GetString());
    }

    private sealed class UnavailableDatabaseFactory : WebApplicationFactory<Program>
    {
        private readonly string _signingKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        private readonly string _databasePath = Path.Combine(
            Path.GetTempPath(), "taskflow-health-unavailable", Guid.NewGuid().ToString("N"), "database.sqlite");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            var configuration = GetTestConfiguration();

            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(configuration));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<ApplicationDbContext>();
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseSqlite(configuration["ConnectionStrings:DefaultConnection"]));
            });
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            var hostConfiguration = GetTestConfiguration();
            hostConfiguration[HostDefaults.EnvironmentKey] = "Production";
            builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(hostConfiguration));
            return base.CreateHost(builder);
        }

        private Dictionary<string, string?> GetTestConfiguration()
        {
            return new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={_databasePath}",
                ["JwtSettings:Key"] = _signingKey,
                ["JwtSettings:Issuer"] = "TaskFlow.Health.IntegrationTests",
                ["JwtSettings:Audience"] = "TaskFlow.Health.IntegrationTests.Client",
                ["JwtSettings:ExpiryMinutes"] = "60",
                ["Cors:AllowedOrigins:0"] = "https://taskflow-health-test.example.test",
                ["Hosting:HttpsTerminatedAtProxy"] = "true"
            };
        }
    }

    [Fact]
    public async Task HttpsRedirection_DoesNotTrustAnUnconfiguredForwardedProtoHeader()
    {
        using var factory = new ProductionHttpsRedirectFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://api.taskflow-test.example"),
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");

        using var response = await client.GetAsync("/api/health/live");

        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
        Assert.Equal("https", response.Headers.Location?.Scheme);
    }

    private sealed class ProductionHttpsRedirectFactory : WebApplicationFactory<Program>
    {
        private readonly string _signingKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        private readonly string _databasePath = Path.Combine(
            Path.GetTempPath(), "taskflow-health-https", Guid.NewGuid().ToString("N"), "database.sqlite");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            var configuration = GetConfiguration();
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(configuration));
            builder.ConfigureTestServices(services =>
                services.Configure<HttpsRedirectionOptions>(options => options.HttpsPort = 443));
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            var configuration = GetConfiguration();
            configuration[HostDefaults.EnvironmentKey] = "Production";
            builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(configuration));
            return base.CreateHost(builder);
        }

        private Dictionary<string, string?> GetConfiguration()
        {
            return new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={_databasePath}",
                ["JwtSettings:Key"] = _signingKey,
                ["JwtSettings:Issuer"] = "TaskFlow.Health.IntegrationTests",
                ["JwtSettings:Audience"] = "TaskFlow.Health.IntegrationTests.Client",
                ["JwtSettings:ExpiryMinutes"] = "60",
                ["Cors:AllowedOrigins:0"] = "https://taskflow-health-test.example.test"
            };
        }
    }
}

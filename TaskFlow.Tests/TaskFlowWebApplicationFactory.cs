using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using TaskFlow.API.Data;
using TaskFlow.API.Models;

namespace TaskFlow.Tests;

public sealed class TaskFlowWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestIssuer = "TaskFlow.IntegrationTests";
    public const string TestAudience = "TaskFlow.IntegrationTests.Client";

    private readonly string _testSigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    private readonly string _databaseName = $"TaskFlow.IntegrationTests.{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        var jwtConfiguration = GetJwtConfiguration();

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(jwtConfiguration);
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var bootstrapConfiguration = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [HostDefaults.EnvironmentKey] = "Testing"
        };

        foreach (var setting in GetJwtConfiguration())
        {
            bootstrapConfiguration[setting.Key] = setting.Value;
        }

        builder.ConfigureHostConfiguration(configuration =>
        {
            configuration.AddInMemoryCollection(bootstrapConfiguration);
        });

        return base.CreateHost(builder);
    }

    public string CreateValidToken(int userId)
    {
        return CreateToken(userId, DateTime.UtcNow.AddMinutes(10));
    }

    public string CreateExpiredToken(int userId)
    {
        return CreateToken(userId, DateTime.UtcNow.AddMinutes(-5));
    }

    public string CreateValidTokenWithoutUserId()
    {
        return CreateToken(null, DateTime.UtcNow.AddMinutes(10));
    }

    public async Task SeedUserWithProjectAsync(int userId, string username, string projectName)
    {
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        context.Users.Add(new User
        {
            Id = userId,
            Username = username,
            Email = $"jwt-integration-{userId}@example.test",
            PasswordHash = "not-used-in-this-test"
        });
        context.Projects.Add(new Project
        {
            Name = projectName,
            OwnerId = userId
        });

        await context.SaveChangesAsync();
    }

    private string CreateToken(int? userId, DateTime expires)
    {
        var now = DateTime.UtcNow;
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_testSigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var claims = userId.HasValue
            ? new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.Value.ToString()),
                new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())
            }
            : new[]
            {
                new Claim("scope", "integration-test")
            };

        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            audience: TestAudience,
            claims: claims,
            notBefore: now.AddMinutes(-10),
            expires: expires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private Dictionary<string, string?> GetJwtConfiguration()
    {
        return new Dictionary<string, string?>
        {
            ["JwtSettings:Key"] = _testSigningKey,
            ["JwtSettings:Issuer"] = TestIssuer,
            ["JwtSettings:Audience"] = TestAudience,
            ["JwtSettings:ExpiryMinutes"] = "60"
        };
    }
}

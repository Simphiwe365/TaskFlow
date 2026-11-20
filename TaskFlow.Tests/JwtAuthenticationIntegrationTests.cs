using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TaskFlow.API.DTOs;
using Xunit;

namespace TaskFlow.Tests;

public class JwtAuthenticationIntegrationTests
{
    private const int TestUserId = 42;

    [Fact]
    public async Task GetProjects_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new TaskFlowWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/projects");

        await AssertProblemDetailsAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetProjects_WithMalformedBearerToken_ReturnsUnauthorized()
    {
        using var factory = new TaskFlowWebApplicationFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-jwt");

        using var response = await client.GetAsync("/api/projects");

        await AssertProblemDetailsAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetProjects_WithExpiredSignedToken_ReturnsUnauthorized()
    {
        using var factory = new TaskFlowWebApplicationFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            factory.CreateExpiredToken(TestUserId));

        using var response = await client.GetAsync("/api/projects");

        await AssertProblemDetailsAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetDashboard_WithValidTokenWithoutUserIdClaim_ReturnsUnauthorized()
    {
        using var factory = new TaskFlowWebApplicationFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            factory.CreateValidTokenWithoutUserId());

        using var response = await client.GetAsync("/api/dashboard");

        await AssertProblemDetailsAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetProjects_WithValidSignedToken_ReturnsOwnedProjects()
    {
        using var factory = new TaskFlowWebApplicationFactory();
        const int otherUserId = 43;
        await factory.SeedUserWithProjectAsync(TestUserId, "jwt-primary-user", "Authenticated project");
        await factory.SeedUserWithProjectAsync(otherUserId, "jwt-other-user", "Other user's project");

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            factory.CreateValidToken(TestUserId));

        using var projectsResponse = await client.GetAsync("/api/projects");

        Assert.Equal(HttpStatusCode.OK, projectsResponse.StatusCode);
        var projects = await projectsResponse.Content.ReadFromJsonAsync<ProjectResponse[]>();
        Assert.Contains(projects!, project => project.Name == "Authenticated project");
        Assert.DoesNotContain(projects!, project => project.Name == "Other user's project");
        Assert.All(projects!, project => Assert.Equal(TestUserId, project.OwnerId));

        using var currentUserResponse = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, currentUserResponse.StatusCode);
        var currentUser = await currentUserResponse.Content.ReadFromJsonAsync<UserSummaryDto>();
        Assert.NotNull(currentUser);
        Assert.Equal(TestUserId, currentUser.Id);
        Assert.Equal("jwt-primary-user", currentUser.Username);
        Assert.Equal("jwt-integration-42@example.test", currentUser.Email);
    }

    private static async Task AssertProblemDetailsAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;
        Assert.Equal((int)expectedStatus, root.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("type").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("title").GetString()));
    }
}

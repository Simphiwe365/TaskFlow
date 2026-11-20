using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TaskFlow.API.DTOs;
using Xunit;

namespace TaskFlow.Tests;

public class AuthControllerIntegrationTests
{
    private const string Password = "CorrectHorse123!";

    [Fact]
    public async Task Register_WithMissingRequiredFields_ReturnsValidationProblem()
    {
        using var factory = new TaskFlowWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new Dictionary<string, string>());

        await AssertValidationProblemAsync(
            response,
            "Username, email and password are required.");
    }

    [Fact]
    public async Task Register_WithShortPassword_ReturnsValidationProblem()
    {
        using var factory = new TaskFlowWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            username = "short-password-user",
            email = "short-password@example.test",
            password = "short"
        });

        await AssertValidationProblemAsync(
            response,
            "Password must be at least 8 characters long.");
    }

    [Fact]
    public async Task Register_WithExistingUsernameOrEmail_ReturnsGenericConflictProblem()
    {
        using var factory = new TaskFlowWebApplicationFactory();
        using var client = factory.CreateClient();
        var registration = new
        {
            username = "duplicate-user",
            email = "duplicate@example.test",
            password = Password
        };

        using var firstResponse = await client.PostAsJsonAsync("/api/auth/register", registration);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        using var duplicateResponse = await client.PostAsJsonAsync("/api/auth/register", registration);

        var responseText = await AssertConflictProblemAsync(
            duplicateResponse,
            "Unable to register using the supplied details.");
        Assert.DoesNotContain(registration.username, responseText);
        Assert.DoesNotContain(registration.email, responseText);
    }

    [Fact]
    public async Task Login_WithMissingCredentials_ReturnsValidationProblem()
    {
        using var factory = new TaskFlowWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new Dictionary<string, string>());

        await AssertValidationProblemAsync(
            response,
            "Username/email and password are required.");
    }

    [Fact]
    public async Task Login_WithUnknownUserAndWrongPassword_UsesSameGenericError()
    {
        using var factory = new TaskFlowWebApplicationFactory();
        using var client = factory.CreateClient();

        using var registrationResponse = await client.PostAsJsonAsync("/api/auth/register", new
        {
            username = "existing-login-user",
            email = "existing-login@example.test",
            password = Password
        });
        Assert.Equal(HttpStatusCode.OK, registrationResponse.StatusCode);

        using var unknownUserResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            identifier = "missing-login@example.test",
            password = Password
        });
        var unknownUserMessages = await AssertValidationProblemAsync(
            unknownUserResponse,
            "Invalid username or password.");

        using var wrongPasswordResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            identifier = "existing-login@example.test",
            password = "WrongPassword123!"
        });
        var wrongPasswordMessages = await AssertValidationProblemAsync(
            wrongPasswordResponse,
            "Invalid username or password.");

        Assert.Equal(unknownUserMessages, wrongPasswordMessages);
    }

    [Fact]
    public async Task RegisterAndLogin_WithValidCredentials_ReturnSuccessfulAuthenticatedResponses()
    {
        using var factory = new TaskFlowWebApplicationFactory();
        using var client = factory.CreateClient();
        const string username = "successful-auth-user";
        const string email = "successful-auth@example.test";

        using var registrationResponse = await client.PostAsJsonAsync("/api/auth/register", new
        {
            username,
            email,
            password = Password
        });

        Assert.Equal(HttpStatusCode.OK, registrationResponse.StatusCode);
        var registration = await registrationResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(registration);
        Assert.False(string.IsNullOrWhiteSpace(registration.Token));
        Assert.Equal(username, registration.User.Username);
        Assert.Equal(email, registration.User.Email);

        using var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            identifier = email,
            password = Password
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var login = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(login);
        Assert.False(string.IsNullOrWhiteSpace(login.Token));
        Assert.Equal(registration.User.Id, login.User.Id);
        Assert.Equal(username, login.User.Username);
        Assert.Equal(email, login.User.Email);

        using var authenticatedClient = factory.CreateClient();
        authenticatedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            login.Token);
        using var currentUserResponse = await authenticatedClient.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, currentUserResponse.StatusCode);
        var currentUser = await currentUserResponse.Content.ReadFromJsonAsync<UserSummaryDto>();
        Assert.NotNull(currentUser);
        Assert.Equal(login.User.Id, currentUser.Id);
        Assert.Equal(username, currentUser.Username);
        Assert.Equal(email, currentUser.Email);
    }

    private static async Task<string[]> AssertValidationProblemAsync(
        HttpResponseMessage response,
        string expectedMessage)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("type").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("title").GetString()));
        Assert.Equal((int)HttpStatusCode.BadRequest, root.GetProperty("status").GetInt32());

        var errors = root.GetProperty("errors");
        var messages = errors.EnumerateObject()
            .SelectMany(error => error.Value.EnumerateArray())
            .Select(error => error.GetString())
            .ToArray();

        Assert.NotEmpty(messages);
        Assert.All(messages, message => Assert.False(string.IsNullOrWhiteSpace(message)));
        Assert.Contains(expectedMessage, messages);
        return messages!;
    }

    private static async Task<string> AssertConflictProblemAsync(
        HttpResponseMessage response,
        string expectedDetail)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;
        Assert.Equal((int)HttpStatusCode.Conflict, root.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("type").GetString()));
        Assert.Equal("Registration conflict", root.GetProperty("title").GetString());
        Assert.Equal(expectedDetail, root.GetProperty("detail").GetString());
        return content;
    }
}

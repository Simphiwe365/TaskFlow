using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.API.Data;
using TaskFlow.API.DTOs;
using TaskFlow.API.Models;
using TaskFlowTaskStatus = TaskFlow.API.Models.TaskStatus;
using Xunit;

namespace TaskFlow.Tests;

public class RequestValidationIntegrationTests
{
    private const int TestUserId = 42;

    [Fact]
    public async Task CreateProject_WithoutBody_ReturnsValidationProblem()
    {
        using var factory = new TaskFlowWebApplicationFactory();
        using var client = CreateAuthenticatedClient(factory, TestUserId);
        using var requestContent = new StringContent(string.Empty, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/api/projects", requestContent);

        await AssertValidationProblemAsync(response);
    }

    [Fact]
    public async Task CreateProject_WithMalformedJson_ReturnsValidationProblem()
    {
        using var factory = new TaskFlowWebApplicationFactory();
        using var client = CreateAuthenticatedClient(factory, TestUserId);
        using var requestContent = new StringContent("{", Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/api/projects", requestContent);

        await AssertValidationProblemAsync(response);
    }

    [Fact]
    public async Task CreateProject_WithNameAtMaximumLength_ReturnsCreatedProject()
    {
        using var factory = new TaskFlowWebApplicationFactory();
        await SeedProjectAsync(factory, TestUserId, "Existing project");
        using var client = CreateAuthenticatedClient(factory, TestUserId);
        var name = new string('p', 100);

        using var response = await client.PostAsJsonAsync("/api/projects", new { name });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>();
        Assert.NotNull(project);
        Assert.Equal(name, project.Name);
        Assert.Equal(TestUserId, project.OwnerId);
    }

    [Fact]
    public async Task CreateProject_WithNameAtMinimumLength_ReturnsCreatedProject()
    {
        using var factory = new TaskFlowWebApplicationFactory();
        using var client = CreateAuthenticatedClient(factory, TestUserId);

        using var response = await client.PostAsJsonAsync("/api/projects", new { name = "P" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>();
        Assert.NotNull(project);
        Assert.Equal("P", project.Name);
    }

    [Fact]
    public async Task CreateProject_WithNameOverMaximumLength_ReturnsValidationProblem()
    {
        using var factory = new TaskFlowWebApplicationFactory();
        using var client = CreateAuthenticatedClient(factory, TestUserId);

        using var response = await client.PostAsJsonAsync(
            "/api/projects",
            new { name = new string('p', 101) });

        await AssertValidationProblemAsync(response);
    }

    [Fact]
    public async Task CreateProject_WithDescriptionAtMaximumLength_ReturnsCreatedProject()
    {
        using var factory = new TaskFlowWebApplicationFactory();
        await SeedProjectAsync(factory, TestUserId, "Existing project");
        using var client = CreateAuthenticatedClient(factory, TestUserId);
        var description = new string('d', 1000);

        using var response = await client.PostAsJsonAsync(
            "/api/projects",
            new { name = "Valid project", description });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>();
        Assert.NotNull(project);
        Assert.Equal("Valid project", project.Name);
        Assert.Equal(description, project.Description);
    }

    [Fact]
    public async Task CreateProject_WithDescriptionOverMaximumLength_ReturnsValidationProblem()
    {
        using var factory = new TaskFlowWebApplicationFactory();
        using var client = CreateAuthenticatedClient(factory, TestUserId);

        using var response = await client.PostAsJsonAsync(
            "/api/projects",
            new { name = "Valid project", description = new string('d', 1001) });

        await AssertValidationProblemAsync(response);
    }

    [Fact]
    public async Task CreateProject_WithMalformedDueDate_ReturnsValidationProblem()
    {
        using var factory = new TaskFlowWebApplicationFactory();
        using var client = CreateAuthenticatedClient(factory, TestUserId);

        using var response = await client.PostAsJsonAsync(
            "/api/projects",
            new { name = "Valid project", dueDate = "not-a-date" });

        await AssertValidationProblemAsync(response);
    }

    [Fact]
    public async Task UpdateProject_WithNameOverMaximumLength_ReturnsValidationProblem()
    {
        using var factory = new TaskFlowWebApplicationFactory();
        var projectId = await SeedProjectAsync(factory, TestUserId, "Existing project");
        using var client = CreateAuthenticatedClient(factory, TestUserId);

        using var response = await client.PutAsJsonAsync(
            $"/api/projects/{projectId}",
            new { name = new string('p', 101) });

        await AssertValidationProblemAsync(response);
    }

    [Fact]
    public async Task CreateTask_WithoutBody_ReturnsValidationProblem()
    {
        using var factory = new TaskFlowWebApplicationFactory();
        var projectId = await SeedProjectAsync(factory, TestUserId, "Task project");
        using var client = CreateAuthenticatedClient(factory, TestUserId);
        using var requestContent = new StringContent(string.Empty, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync($"/api/projects/{projectId}/tasks", requestContent);

        await AssertValidationProblemAsync(response);
    }

    [Fact]
    public async Task CreateTask_WithMalformedJson_ReturnsValidationProblem()
    {
        using var factory = new TaskFlowWebApplicationFactory();
        var projectId = await SeedProjectAsync(factory, TestUserId, "Task project");
        using var client = CreateAuthenticatedClient(factory, TestUserId);
        using var requestContent = new StringContent("{", Encoding.UTF8, "application/json");

        using var response = await client.PostAsync($"/api/projects/{projectId}/tasks", requestContent);

        await AssertValidationProblemAsync(response);
    }

    [Fact]
    public async Task CreateTask_WithTitleAtMaximumLength_ReturnsCreatedTask()
    {
        using var factory = new TaskFlowWebApplicationFactory();
        var projectId = await SeedProjectAsync(factory, TestUserId, "Task project");
        using var client = CreateAuthenticatedClient(factory, TestUserId);
        var title = new string('t', 150);

        using var response = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/tasks",
            new { title });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var task = await response.Content.ReadFromJsonAsync<TaskResponse>();
        Assert.NotNull(task);
        Assert.Equal(title, task.Title);
        Assert.Equal(projectId, task.ProjectId);
    }

    [Fact]
    public async Task CreateTask_WithTitleAtMinimumLength_ReturnsCreatedTask()
    {
        using var factory = new TaskFlowWebApplicationFactory();
        var projectId = await SeedProjectAsync(factory, TestUserId, "Task project");
        using var client = CreateAuthenticatedClient(factory, TestUserId);

        using var response = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/tasks",
            new { title = "T" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var task = await response.Content.ReadFromJsonAsync<TaskResponse>();
        Assert.NotNull(task);
        Assert.Equal("T", task.Title);
    }

    [Fact]
    public async Task CreateTask_WithTitleOverMaximumLength_ReturnsValidationProblem()
    {
        using var factory = new TaskFlowWebApplicationFactory();
        var projectId = await SeedProjectAsync(factory, TestUserId, "Task project");
        using var client = CreateAuthenticatedClient(factory, TestUserId);

        using var response = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/tasks",
            new { title = new string('t', 151) });

        await AssertValidationProblemAsync(response);
    }

    [Fact]
    public async Task CreateTask_WithDescriptionAtMaximumLength_ReturnsCreatedTask()
    {
        using var factory = new TaskFlowWebApplicationFactory();
        var projectId = await SeedProjectAsync(factory, TestUserId, "Task project");
        using var client = CreateAuthenticatedClient(factory, TestUserId);
        var description = new string('d', 2000);

        using var response = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/tasks",
            new { title = "Valid task", description });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var task = await response.Content.ReadFromJsonAsync<TaskResponse>();
        Assert.NotNull(task);
        Assert.Equal("Valid task", task.Title);
        Assert.Equal(description, task.Description);
    }

    [Fact]
    public async Task CreateTask_WithDescriptionOverMaximumLength_ReturnsValidationProblem()
    {
        using var factory = new TaskFlowWebApplicationFactory();
        var projectId = await SeedProjectAsync(factory, TestUserId, "Task project");
        using var client = CreateAuthenticatedClient(factory, TestUserId);

        using var response = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/tasks",
            new { title = "Valid task", description = new string('d', 2001) });

        await AssertValidationProblemAsync(response);
    }

    [Fact]
    public async Task CreateTask_WithUndefinedStatus_ReturnsValidationProblem()
    {
        using var factory = new TaskFlowWebApplicationFactory();
        var projectId = await SeedProjectAsync(factory, TestUserId, "Task project");
        using var client = CreateAuthenticatedClient(factory, TestUserId);

        using var response = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/tasks",
            new { title = "Valid task", status = 999 });

        await AssertValidationProblemAsync(response, "Invalid task status.");
    }

    [Fact]
    public async Task CreateTask_WithUndefinedPriority_ReturnsValidationProblem()
    {
        using var factory = new TaskFlowWebApplicationFactory();
        var projectId = await SeedProjectAsync(factory, TestUserId, "Task project");
        using var client = CreateAuthenticatedClient(factory, TestUserId);

        using var response = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/tasks",
            new { title = "Valid task", priority = 999 });

        await AssertValidationProblemAsync(response, "Invalid task priority.");
    }

    [Fact]
    public async Task CreateTask_WithMalformedDueDate_ReturnsValidationProblem()
    {
        using var factory = new TaskFlowWebApplicationFactory();
        var projectId = await SeedProjectAsync(factory, TestUserId, "Task project");
        using var client = CreateAuthenticatedClient(factory, TestUserId);

        using var response = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/tasks",
            new { title = "Valid task", dueDate = "not-a-date" });

        await AssertValidationProblemAsync(response);
    }

    [Fact]
    public async Task CreateTask_WithNonexistentAssignee_ReturnsValidationProblem()
    {
        using var factory = new TaskFlowWebApplicationFactory();
        var projectId = await SeedProjectAsync(factory, TestUserId, "Task project");
        using var client = CreateAuthenticatedClient(factory, TestUserId);

        using var response = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/tasks",
            new { title = "Valid task", assigneeId = int.MaxValue });

        await AssertValidationProblemAsync(response, "Assignee must be a valid user in the system.");
    }

    [Fact]
    public async Task GetProject_WithNonexistentId_ReturnsNotFound()
    {
        using var factory = new TaskFlowWebApplicationFactory();
        await SeedProjectAsync(factory, TestUserId, "Existing project");
        using var client = CreateAuthenticatedClient(factory, TestUserId);

        using var response = await client.GetAsync($"/api/projects/{int.MaxValue}");

        await AssertProblemDetailsStatusAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetProject_WithNonIntegerId_ReturnsNotFound()
    {
        using var factory = new TaskFlowWebApplicationFactory();
        using var client = CreateAuthenticatedClient(factory, TestUserId);

        using var response = await client.GetAsync("/api/projects/not-an-integer");

        await AssertProblemDetailsStatusAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTask_WithNonexistentIdUnderOwnedProject_ReturnsNotFound()
    {
        using var factory = new TaskFlowWebApplicationFactory();
        var projectId = await SeedProjectAsync(factory, TestUserId, "Task project");
        using var client = CreateAuthenticatedClient(factory, TestUserId);

        using var response = await client.GetAsync($"/api/projects/{projectId}/tasks/{int.MaxValue}");

        await AssertProblemDetailsStatusAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTask_WithNonIntegerIdUnderOwnedProject_ReturnsNotFound()
    {
        using var factory = new TaskFlowWebApplicationFactory();
        var projectId = await SeedProjectAsync(factory, TestUserId, "Task project");
        using var client = CreateAuthenticatedClient(factory, TestUserId);

        using var response = await client.GetAsync($"/api/projects/{projectId}/tasks/not-an-integer");

        await AssertProblemDetailsStatusAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAnotherUsersProjectAndTask_ReturnsNotFoundWithoutPrivateData()
    {
        const int otherUserId = 43;
        using var factory = new TaskFlowWebApplicationFactory();
        await SeedProjectAsync(factory, TestUserId, "Owned project");
        var privateProjectId = await SeedProjectAsync(factory, otherUserId, "Private project");
        var privateTaskId = await SeedTaskAsync(factory, privateProjectId, "Private task title");
        using var client = CreateAuthenticatedClient(factory, TestUserId);

        using var projectResponse = await client.GetAsync($"/api/projects/{privateProjectId}");
        var projectBody = await projectResponse.Content.ReadAsStringAsync();
        using var taskResponse = await client.GetAsync(
            $"/api/projects/{privateProjectId}/tasks/{privateTaskId}");
        var taskBody = await taskResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, projectResponse.StatusCode);
        Assert.DoesNotContain("Private project", projectBody);
        Assert.Equal(HttpStatusCode.NotFound, taskResponse.StatusCode);
        Assert.DoesNotContain("Private task title", taskBody);
    }

    private static HttpClient CreateAuthenticatedClient(TaskFlowWebApplicationFactory factory, int userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            factory.CreateValidToken(userId));
        return client;
    }

    private static async Task<int> SeedProjectAsync(
        TaskFlowWebApplicationFactory factory,
        int userId,
        string projectName)
    {
        await factory.SeedUserWithProjectAsync(userId, $"validation-user-{userId}", projectName);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await context.Projects
            .Where(project => project.OwnerId == userId && project.Name == projectName)
            .Select(project => project.Id)
            .SingleAsync();
    }

    private static async Task<int> SeedTaskAsync(
        TaskFlowWebApplicationFactory factory,
        int projectId,
        string title)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var task = new TaskItem
        {
            ProjectId = projectId,
            Title = title,
            Status = TaskFlowTaskStatus.ToDo,
            Priority = TaskPriority.Medium
        };

        context.Tasks.Add(task);
        await context.SaveChangesAsync();
        return task.Id;
    }

    private static async Task AssertValidationProblemAsync(
        HttpResponseMessage response,
        string? expectedMessage = null)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("type").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("title").GetString()));
        Assert.Equal((int)HttpStatusCode.BadRequest, root.GetProperty("status").GetInt32());
        Assert.True(root.TryGetProperty("errors", out var errors));
        var messages = errors.EnumerateObject()
            .SelectMany(error => error.Value.EnumerateArray())
            .Select(error => error.GetString())
            .ToArray();

        Assert.NotEmpty(messages);
        Assert.All(messages, message => Assert.False(string.IsNullOrWhiteSpace(message)));

        if (expectedMessage is not null)
        {
            Assert.Contains(expectedMessage, messages);
        }
    }

    private static async Task AssertProblemDetailsStatusAsync(
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

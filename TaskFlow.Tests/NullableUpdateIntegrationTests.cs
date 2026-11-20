using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.API.Data;
using TaskFlow.API.DTOs;
using TaskFlow.API.Models;
using Xunit;

namespace TaskFlow.Tests;

public class NullableUpdateIntegrationTests
{
    private const int OwnerId = 42;

    [Fact]
    public async Task UpdateProject_DueDateDistinguishesOmittedNullAndValue()
    {
        using var factory = new TaskFlowWebApplicationFactory();
        var originalDate = new DateTime(2030, 2, 3, 0, 0, 0, DateTimeKind.Utc);
        var replacementDate = new DateTime(2031, 4, 5, 0, 0, 0, DateTimeKind.Utc);
        var projectId = await SeedProjectAsync(factory, OwnerId, "Due date project", dueDate: originalDate);
        using var client = CreateAuthenticatedClient(factory, OwnerId);

        var omitted = await UpdateProjectAsync(client, projectId, EmptyUpdate());
        Assert.Equal(originalDate, omitted.DueDate);

        var cleared = await UpdateProjectAsync(client, projectId, new { dueDate = (DateTime?)null });
        Assert.Null(cleared.DueDate);

        var updated = await UpdateProjectAsync(client, projectId, new { dueDate = replacementDate });
        Assert.Equal(replacementDate, updated.DueDate);
    }

    [Fact]
    public async Task UpdateProject_DescriptionDistinguishesOmittedNullAndValue()
    {
        using var factory = new TaskFlowWebApplicationFactory();
        const string originalDescription = "Original project description";
        var projectId = await SeedProjectAsync(
            factory,
            OwnerId,
            "Description project",
            description: originalDescription);
        using var client = CreateAuthenticatedClient(factory, OwnerId);

        var omitted = await UpdateProjectAsync(client, projectId, EmptyUpdate());
        Assert.Equal(originalDescription, omitted.Description);

        var cleared = await UpdateProjectAsync(client, projectId, new { description = (string?)null });
        Assert.Null(cleared.Description);

        const string replacementDescription = "Updated project description";
        var updated = await UpdateProjectAsync(
            client,
            projectId,
            new { description = replacementDescription });
        Assert.Equal(replacementDescription, updated.Description);
    }

    [Fact]
    public async Task UpdateTask_DueDateDistinguishesOmittedNullAndValue()
    {
        using var factory = new TaskFlowWebApplicationFactory();
        var originalDate = new DateTime(2030, 2, 3, 0, 0, 0, DateTimeKind.Utc);
        var replacementDate = new DateTime(2031, 4, 5, 0, 0, 0, DateTimeKind.Utc);
        var projectId = await SeedProjectAsync(factory, OwnerId, "Task due date project");
        var taskId = await SeedTaskAsync(
            factory,
            projectId,
            "Due date task",
            dueDate: originalDate);
        using var client = CreateAuthenticatedClient(factory, OwnerId);

        var omitted = await UpdateTaskAsync(client, projectId, taskId, EmptyUpdate());
        Assert.Equal(originalDate, omitted.DueDate);

        var cleared = await UpdateTaskAsync(
            client,
            projectId,
            taskId,
            new { dueDate = (DateTime?)null });
        Assert.Null(cleared.DueDate);

        var updated = await UpdateTaskAsync(
            client,
            projectId,
            taskId,
            new { dueDate = replacementDate });
        Assert.Equal(replacementDate, updated.DueDate);
    }

    [Fact]
    public async Task UpdateTask_DescriptionDistinguishesOmittedNullAndValue()
    {
        using var factory = new TaskFlowWebApplicationFactory();
        const string originalDescription = "Original task description";
        var projectId = await SeedProjectAsync(factory, OwnerId, "Task description project");
        var taskId = await SeedTaskAsync(
            factory,
            projectId,
            "Description task",
            description: originalDescription);
        using var client = CreateAuthenticatedClient(factory, OwnerId);

        var omitted = await UpdateTaskAsync(client, projectId, taskId, EmptyUpdate());
        Assert.Equal(originalDescription, omitted.Description);

        var cleared = await UpdateTaskAsync(
            client,
            projectId,
            taskId,
            new { description = (string?)null });
        Assert.Null(cleared.Description);

        const string replacementDescription = "Updated task description";
        var updated = await UpdateTaskAsync(
            client,
            projectId,
            taskId,
            new { description = replacementDescription });
        Assert.Equal(replacementDescription, updated.Description);
    }

    [Fact]
    public async Task UpdateTask_AssigneeDistinguishesOmittedNullAndValue()
    {
        const int originalAssigneeId = 43;
        const int replacementAssigneeId = 44;
        using var factory = new TaskFlowWebApplicationFactory();
        await factory.SeedUserWithProjectAsync(originalAssigneeId, "original-assignee", "Original assignee project");
        await factory.SeedUserWithProjectAsync(replacementAssigneeId, "replacement-assignee", "Replacement assignee project");
        var projectId = await SeedProjectAsync(factory, OwnerId, "Assignee project");
        var taskId = await SeedTaskAsync(
            factory,
            projectId,
            "Assigned task",
            assigneeId: originalAssigneeId);
        using var client = CreateAuthenticatedClient(factory, OwnerId);

        var omitted = await UpdateTaskAsync(client, projectId, taskId, EmptyUpdate());
        Assert.Equal(originalAssigneeId, omitted.AssigneeId);
        Assert.Equal("original-assignee", omitted.Assignee?.Username);

        var cleared = await UpdateTaskAsync(
            client,
            projectId,
            taskId,
            new { assigneeId = (int?)null });
        Assert.Null(cleared.AssigneeId);
        Assert.Null(cleared.Assignee);

        var updated = await UpdateTaskAsync(
            client,
            projectId,
            taskId,
            new { assigneeId = replacementAssigneeId });
        Assert.Equal(replacementAssigneeId, updated.AssigneeId);
        Assert.Equal("replacement-assignee", updated.Assignee?.Username);
    }

    private static Dictionary<string, object?> EmptyUpdate()
    {
        return new Dictionary<string, object?>();
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
        string projectName,
        string? description = null,
        DateTime? dueDate = null)
    {
        await factory.SeedUserWithProjectAsync(userId, $"nullable-user-{userId}", projectName);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var project = await context.Projects.SingleAsync(
            item => item.OwnerId == userId && item.Name == projectName);
        project.Description = description;
        project.DueDate = dueDate;
        await context.SaveChangesAsync();
        return project.Id;
    }

    private static async Task<int> SeedTaskAsync(
        TaskFlowWebApplicationFactory factory,
        int projectId,
        string title,
        string? description = null,
        DateTime? dueDate = null,
        int? assigneeId = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var task = new TaskItem
        {
            ProjectId = projectId,
            Title = title,
            Description = description,
            DueDate = dueDate,
            AssigneeId = assigneeId
        };

        context.Tasks.Add(task);
        await context.SaveChangesAsync();
        return task.Id;
    }

    private static async Task<ProjectResponse> UpdateProjectAsync(
        HttpClient client,
        int projectId,
        object update)
    {
        using var response = await client.PutAsJsonAsync($"/api/projects/{projectId}", update);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>();
        Assert.NotNull(project);
        return project;
    }

    private static async Task<TaskResponse> UpdateTaskAsync(
        HttpClient client,
        int projectId,
        int taskId,
        object update)
    {
        using var response = await client.PutAsJsonAsync(
            $"/api/projects/{projectId}/tasks/{taskId}",
            update);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(content);
        if (document.RootElement.TryGetProperty("assignee", out var assignee) &&
            assignee.ValueKind == JsonValueKind.Object)
        {
            Assert.False(assignee.TryGetProperty("email", out _));
        }

        var task = JsonSerializer.Deserialize<TaskResponse>(
            content,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(task);
        return task;
    }
}

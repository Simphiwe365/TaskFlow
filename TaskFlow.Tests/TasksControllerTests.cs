using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskFlow.API.Controllers;
using TaskFlow.API.Data;
using TaskFlow.API.DTOs;
using TaskFlow.API.Models;
using Xunit;
using TaskStatus = TaskFlow.API.Models.TaskStatus;

namespace TaskFlow.Tests;

public class TasksControllerTests
{
    [Fact]
    public async Task CreateTask_ShouldCreateTaskSuccessfully()
    {
        await using var context = CreateContext();
        context.Projects.Add(new Project { Id = 1, Name = "Owned", OwnerId = 1 });
        context.Users.Add(new User { Id = 2, Username = "assignee", Email = "assignee@example.com", PasswordHash = "hash" });
        await context.SaveChangesAsync();

        var controller = CreateController(context, 1);
        var response = await controller.CreateTask(1, new CreateTaskRequest
        {
            Title = "  Implement feature  ",
            Description = "  Add task CRUD  ",
            Status = TaskStatus.InProgress,
            Priority = TaskPriority.High,
            AssigneeId = 2
        });

        var created = Assert.IsType<CreatedAtActionResult>(response.Result);
        var task = Assert.IsType<TaskResponse>(created.Value);
        Assert.Equal("Implement feature", task.Title);
        Assert.Equal("Add task CRUD", task.Description);
        Assert.Equal(TaskStatus.InProgress, task.Status);
        Assert.Equal(TaskPriority.High, task.Priority);
        Assert.Equal(1, task.ProjectId);
        Assert.Equal(2, task.AssigneeId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateTask_ShouldRejectInvalidTitle(string? title)
    {
        await using var context = CreateContext();
        context.Projects.Add(new Project { Id = 1, Name = "Owned", OwnerId = 1 });
        await context.SaveChangesAsync();

        var controller = CreateController(context, 1);
        var response = await controller.CreateTask(1, new CreateTaskRequest { Title = title! });

        Assert.IsType<BadRequestObjectResult>(response.Result);
    }

    [Fact]
    public void CreateTaskRequest_ShouldRejectTitleAndDescriptionLengths()
    {
        var invalidTitle = new CreateTaskRequest { Title = new string('x', 151) };
        var invalidDescription = new CreateTaskRequest
        {
            Title = "Valid",
            Description = new string('x', 2001)
        };

        Assert.Contains(Validate(invalidTitle), r => r.MemberNames.Contains(nameof(CreateTaskRequest.Title)));
        Assert.Contains(Validate(invalidDescription), r => r.MemberNames.Contains(nameof(CreateTaskRequest.Description)));
    }

    [Fact]
    public async Task GetTasks_ShouldListTasksForOwnedProject()
    {
        await using var context = CreateContext();
        context.Projects.Add(new Project { Id = 1, Name = "Owned", OwnerId = 1 });
        context.Tasks.Add(new TaskItem { Id = 1, ProjectId = 1, Title = "One", Status = TaskStatus.ToDo, Priority = TaskPriority.Medium });
        await context.SaveChangesAsync();

        var controller = CreateController(context, 1);
        var response = await controller.GetTasks(1);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var tasks = Assert.IsAssignableFrom<IEnumerable<TaskResponse>>(ok.Value);
        Assert.Single(tasks);
    }

    [Fact]
    public async Task GetTaskById_ShouldRetrieveOwnedTask()
    {
        await using var context = CreateContext();
        context.Projects.Add(new Project { Id = 1, Name = "Owned", OwnerId = 1 });
        context.Tasks.Add(new TaskItem { Id = 1, ProjectId = 1, Title = "One", Status = TaskStatus.ToDo, Priority = TaskPriority.Medium });
        await context.SaveChangesAsync();

        var controller = CreateController(context, 1);
        var response = await controller.GetTaskById(1, 1);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var task = Assert.IsType<TaskResponse>(ok.Value);
        Assert.Equal("One", task.Title);
    }

    [Fact]
    public async Task UpdateTask_ShouldUpdateProperties()
    {
        await using var context = CreateContext();
        context.Projects.Add(new Project { Id = 1, Name = "Owned", OwnerId = 1 });
        context.Tasks.Add(new TaskItem
        {
            Id = 1,
            ProjectId = 1,
            Title = "Original",
            Description = "Original description",
            Status = TaskStatus.ToDo,
            Priority = TaskPriority.Low
        });
        await context.SaveChangesAsync();

        var controller = CreateController(context, 1);
        var response = await controller.UpdateTask(1, 1, new UpdateTaskRequest
        {
            Title = "  Updated  ",
            Description = "  Updated description  ",
            Status = TaskStatus.Done,
            Priority = TaskPriority.Urgent,
            DueDate = DateTime.UtcNow.AddDays(5)
        });

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var task = Assert.IsType<TaskResponse>(ok.Value);
        Assert.Equal("Updated", task.Title);
        Assert.Equal("Updated description", task.Description);
        Assert.Equal(TaskStatus.Done, task.Status);
        Assert.Equal(TaskPriority.Urgent, task.Priority);

        var stored = await context.Tasks.FindAsync(1);
        Assert.NotNull(stored);
        Assert.Equal("Updated", stored.Title);
    }

    [Fact]
    public async Task UpdateTask_ShouldPreserveUnspecifiedFields()
    {
        await using var context = CreateContext();
        var dueDate = DateTime.UtcNow.AddDays(7);
        context.Projects.Add(new Project { Id = 1, Name = "Owned", OwnerId = 1 });
        context.Tasks.Add(new TaskItem
        {
            Id = 1,
            ProjectId = 1,
            Title = "Original",
            Description = "Keep description",
            Status = TaskStatus.InProgress,
            Priority = TaskPriority.High,
            DueDate = dueDate
        });
        await context.SaveChangesAsync();

        var controller = CreateController(context, 1);
        var response = await controller.UpdateTask(1, 1, new UpdateTaskRequest
        {
            Title = "  Changed title  "
        });

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var task = Assert.IsType<TaskResponse>(ok.Value);
        Assert.Equal("Changed title", task.Title);
        Assert.Equal("Keep description", task.Description);
        Assert.Equal(TaskStatus.InProgress, task.Status);
        Assert.Equal(TaskPriority.High, task.Priority);
        Assert.Equal(dueDate, task.DueDate);
    }

    [Theory]
    [InlineData((TaskStatus)999)]
    [InlineData((TaskStatus)(-1))]
    public async Task UpdateTask_ShouldRejectInvalidStatus(TaskStatus status)
    {
        await using var context = CreateContext();
        context.Projects.Add(new Project { Id = 1, Name = "Owned", OwnerId = 1 });
        context.Tasks.Add(new TaskItem { Id = 1, ProjectId = 1, Title = "One", Status = TaskStatus.ToDo, Priority = TaskPriority.Medium });
        await context.SaveChangesAsync();

        var controller = CreateController(context, 1);
        var response = await controller.UpdateTask(1, 1, new UpdateTaskRequest { Status = status });

        Assert.IsType<BadRequestObjectResult>(response.Result);
    }

    [Theory]
    [InlineData((TaskPriority)999)]
    [InlineData((TaskPriority)(-1))]
    public async Task UpdateTask_ShouldRejectInvalidPriority(TaskPriority priority)
    {
        await using var context = CreateContext();
        context.Projects.Add(new Project { Id = 1, Name = "Owned", OwnerId = 1 });
        context.Tasks.Add(new TaskItem { Id = 1, ProjectId = 1, Title = "One", Status = TaskStatus.ToDo, Priority = TaskPriority.Medium });
        await context.SaveChangesAsync();

        var controller = CreateController(context, 1);
        var response = await controller.UpdateTask(1, 1, new UpdateTaskRequest { Priority = priority });

        Assert.IsType<BadRequestObjectResult>(response.Result);
    }

    [Fact]
    public async Task CreateTask_ShouldRejectNonexistentAssignee()
    {
        await using var context = CreateContext();
        context.Projects.Add(new Project { Id = 1, Name = "Owned", OwnerId = 1 });
        await context.SaveChangesAsync();

        var controller = CreateController(context, 1);
        var response = await controller.CreateTask(1, new CreateTaskRequest
        {
            Title = "Valid",
            AssigneeId = 999
        });

        Assert.IsType<BadRequestObjectResult>(response.Result);
    }

    [Fact]
    public async Task DeleteTask_ShouldDeleteOwnedTask()
    {
        await using var context = CreateContext();
        context.Projects.Add(new Project { Id = 1, Name = "Owned", OwnerId = 1 });
        context.Tasks.Add(new TaskItem { Id = 1, ProjectId = 1, Title = "One", Status = TaskStatus.ToDo, Priority = TaskPriority.Medium });
        await context.SaveChangesAsync();

        var controller = CreateController(context, 1);
        var response = await controller.DeleteTask(1, 1);

        Assert.IsType<NoContentResult>(response);
        Assert.Null(await context.Tasks.FindAsync(1));
    }

    [Fact]
    public async Task OneUserCannotAccessAnotherUsersProjectTasks()
    {
        await using var context = CreateContext();
        context.Projects.Add(new Project { Id = 1, Name = "Private", OwnerId = 2 });
        context.Tasks.Add(new TaskItem { Id = 1, ProjectId = 1, Title = "Private task", Status = TaskStatus.ToDo, Priority = TaskPriority.Medium });
        await context.SaveChangesAsync();

        var controller = CreateController(context, 1);

        var listResponse = await controller.GetTasks(1);
        var getResponse = await controller.GetTaskById(1, 1);
        var updateResponse = await controller.UpdateTask(1, 1, new UpdateTaskRequest { Title = "Changed" });

        Assert.IsType<NotFoundResult>(listResponse.Result);
        Assert.IsType<NotFoundResult>(getResponse.Result);
        Assert.IsType<NotFoundResult>(updateResponse.Result);
    }

    [Fact]
    public async Task UpdatingTaskWithWrongProjectId_ShouldReturnNotFound()
    {
        await using var context = CreateContext();
        context.Projects.Add(new Project { Id = 1, Name = "Owned", OwnerId = 1 });
        context.Projects.Add(new Project { Id = 2, Name = "Other project", OwnerId = 1 });
        context.Tasks.Add(new TaskItem { Id = 1, ProjectId = 1, Title = "Belongs to 1", Status = TaskStatus.ToDo, Priority = TaskPriority.Medium });
        await context.SaveChangesAsync();

        var controller = CreateController(context, 1);
        var response = await controller.UpdateTask(2, 1, new UpdateTaskRequest { Title = "Changed" });

        Assert.IsType<NotFoundResult>(response.Result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-an-integer")]
    [InlineData("0")]
    public async Task MissingOrInvalidUserClaim_ShouldReturnUnauthorized(string? claimValue)
    {
        await using var context = CreateContext();
        var controller = CreateController(context, claimValue);

        var response = await controller.GetTasks(1);

        Assert.IsType<UnauthorizedResult>(response.Result);
    }

    [Fact]
    public async Task CreateTask_ShouldRejectUndefinedStatus()
    {
        await using var context = CreateContext();
        context.Projects.Add(new Project { Id = 1, Name = "Owned", OwnerId = 1 });
        await context.SaveChangesAsync();

        var controller = CreateController(context, 1);
        var response = await controller.CreateTask(1, new CreateTaskRequest
        {
            Title = "Valid",
            Status = (TaskStatus)999
        });

        Assert.IsType<BadRequestObjectResult>(response.Result);
    }

    [Fact]
    public async Task CreateTask_ShouldRejectUndefinedPriority()
    {
        await using var context = CreateContext();
        context.Projects.Add(new Project { Id = 1, Name = "Owned", OwnerId = 1 });
        await context.SaveChangesAsync();

        var controller = CreateController(context, 1);
        var response = await controller.CreateTask(1, new CreateTaskRequest
        {
            Title = "Valid",
            Priority = (TaskPriority)999
        });

        Assert.IsType<BadRequestObjectResult>(response.Result);
    }

    [Fact]
    public async Task UpdateTask_ShouldRejectTitleOver150Characters()
    {
        await using var context = CreateContext();
        context.Projects.Add(new Project { Id = 1, Name = "Owned", OwnerId = 1 });
        context.Tasks.Add(new TaskItem
        {
            Id = 1,
            ProjectId = 1,
            Title = "Original",
            Status = TaskStatus.ToDo,
            Priority = TaskPriority.Medium
        });
        await context.SaveChangesAsync();

        var controller = CreateController(context, 1);
        var response = await controller.UpdateTask(1, 1, new UpdateTaskRequest
        {
            Title = new string('x', 151)
        });

        Assert.IsType<BadRequestObjectResult>(response.Result);

        var stored = await context.Tasks.FindAsync(1);
        Assert.NotNull(stored);
        Assert.Equal("Original", stored.Title);
    }

    [Fact]
    public async Task UpdateTask_ShouldRejectDescriptionOver2000Characters()
    {
        await using var context = CreateContext();
        context.Projects.Add(new Project { Id = 1, Name = "Owned", OwnerId = 1 });
        context.Tasks.Add(new TaskItem
        {
            Id = 1,
            ProjectId = 1,
            Title = "Original",
            Description = "Original description",
            Status = TaskStatus.ToDo,
            Priority = TaskPriority.Medium
        });
        await context.SaveChangesAsync();

        var controller = CreateController(context, 1);
        var response = await controller.UpdateTask(1, 1, new UpdateTaskRequest
        {
            Description = new string('x', 2001)
        });

        Assert.IsType<BadRequestObjectResult>(response.Result);

        var stored = await context.Tasks.FindAsync(1);
        Assert.NotNull(stored);
        Assert.Equal("Original description", stored.Description);
    }

    [Fact]
    public async Task UpdateTask_ShouldRejectNonexistentAssignee()
    {
        await using var context = CreateContext();
        context.Projects.Add(new Project { Id = 1, Name = "Owned", OwnerId = 1 });
        context.Tasks.Add(new TaskItem
        {
            Id = 1,
            ProjectId = 1,
            Title = "Original",
            Status = TaskStatus.ToDo,
            Priority = TaskPriority.Medium,
            AssigneeId = null
        });
        await context.SaveChangesAsync();

        var controller = CreateController(context, 1);
        var response = await controller.UpdateTask(1, 1, new UpdateTaskRequest
        {
            AssigneeId = 999
        });

        Assert.IsType<BadRequestObjectResult>(response.Result);

        var stored = await context.Tasks.FindAsync(1);
        Assert.NotNull(stored);
        Assert.Null(stored.AssigneeId);
    }

    [Fact]
    public async Task DeleteTask_ShouldReturnNotFoundForDifferentProject()
    {
        await using var context = CreateContext();
        context.Projects.Add(new Project { Id = 1, Name = "Owned", OwnerId = 1 });
        context.Projects.Add(new Project { Id = 2, Name = "Other project", OwnerId = 1 });
        context.Tasks.Add(new TaskItem
        {
            Id = 1,
            ProjectId = 1,
            Title = "Belongs to project 1",
            Status = TaskStatus.ToDo,
            Priority = TaskPriority.Medium
        });
        await context.SaveChangesAsync();

        var controller = CreateController(context, 1);
        var response = await controller.DeleteTask(2, 1);

        Assert.IsType<NotFoundResult>(response);
        Assert.NotNull(await context.Tasks.FindAsync(1));
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static TasksController CreateController(ApplicationDbContext context, int userId)
    {
        return CreateController(context, new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        });
    }

    private static TasksController CreateController(ApplicationDbContext context, string? claimValue)
    {
        var claims = claimValue is null
            ? Array.Empty<Claim>()
            : new[] { new Claim(ClaimTypes.NameIdentifier, claimValue) };

        return CreateController(context, claims);
    }

    private static TasksController CreateController(ApplicationDbContext context, Claim[] claims)
    {
        var controller = new TasksController(context);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuthentication"))
            }
        };

        return controller;
    }

    private static IReadOnlyCollection<ValidationResult> Validate(object request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, true);
        return results;
    }
}

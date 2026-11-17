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

namespace TaskFlow.Tests;

public class ProjectsControllerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateProjectRequest_ShouldRejectMissingOrWhitespaceName(string? name)
    {
        var request = new CreateProjectRequest { Name = name! };

        var results = Validate(request);

        Assert.NotEmpty(results);
    }

    [Fact]
    public void CreateProjectRequest_ShouldRejectNameOver100Characters()
    {
        var request = new CreateProjectRequest { Name = new string('x', 101) };

        var results = Validate(request);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateProjectRequest.Name)));
    }

    [Fact]
    public void CreateProjectRequest_ShouldRejectDescriptionOver1000Characters()
    {
        var request = new CreateProjectRequest
        {
            Name = "Valid name",
            Description = new string('x', 1001)
        };

        var results = Validate(request);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateProjectRequest.Description)));
    }

    [Fact]
    public void CreateProjectRequest_ShouldAllowPastDueDate()
    {
        var request = new CreateProjectRequest
        {
            Name = "Valid name",
            DueDate = DateTime.UtcNow.AddYears(-1)
        };

        Assert.Empty(Validate(request));
    }

    [Fact]
    public async Task CreateProject_ShouldTrimNameAndDescription()
    {
        await using var context = CreateContext();
        var controller = CreateController(context, 1);

        var response = await controller.CreateProject(new CreateProjectRequest
        {
            Name = "  Project name  ",
            Description = "  Project description  ",
            DueDate = DateTime.UtcNow.AddYears(-1)
        });

        var created = Assert.IsType<CreatedAtActionResult>(response.Result);
        var project = Assert.IsType<ProjectResponse>(created.Value);
        Assert.Equal("Project name", project.Name);
        Assert.Equal("Project description", project.Description);
        Assert.True(project.DueDate < DateTime.UtcNow);
    }

    [Fact]
    public async Task UpdateProject_ShouldRejectWhitespaceName()
    {
        await using var context = CreateContext();
        context.Projects.Add(new Project { Id = 10, Name = "Original", OwnerId = 1 });
        await context.SaveChangesAsync();

        var controller = CreateController(context, 1);
        var response = await controller.UpdateProject(10, new UpdateProjectRequest { Name = "   " });

        Assert.IsType<BadRequestObjectResult>(response.Result);
    }

    [Fact]
    public async Task UpdateProject_ShouldTrimNameAndDescriptionAndPreserveUntouchedFields()
    {
        await using var context = CreateContext();
        var dueDate = DateTime.UtcNow.AddDays(10);
        context.Projects.Add(new Project
        {
            Id = 11,
            Name = "Original",
            Description = "Original description",
            DueDate = dueDate,
            OwnerId = 1
        });
        await context.SaveChangesAsync();

        var controller = CreateController(context, 1);
        var response = await controller.UpdateProject(11, new UpdateProjectRequest
        {
            Name = "  Updated name  ",
            Description = null,
            DueDate = null
        });

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var updated = Assert.IsType<ProjectResponse>(ok.Value);
        Assert.Equal("Updated name", updated.Name);
        Assert.Equal("Original description", updated.Description);
        Assert.Equal(dueDate, updated.DueDate);

        var stored = await context.Projects.FindAsync(11);
        Assert.NotNull(stored);
        Assert.Equal("Updated name", stored.Name);
        Assert.Equal("Original description", stored.Description);
        Assert.Equal(dueDate, stored.DueDate);
    }

    [Fact]
    public async Task UpdateProject_ShouldAllowDescriptionOnlyPartialUpdate()
    {
        await using var context = CreateContext();
        var dueDate = DateTime.UtcNow.AddDays(5);
        context.Projects.Add(new Project
        {
            Id = 12,
            Name = "Keep name",
            Description = "Old",
            DueDate = dueDate,
            OwnerId = 1
        });
        await context.SaveChangesAsync();

        var controller = CreateController(context, 1);
        var response = await controller.UpdateProject(12, new UpdateProjectRequest
        {
            Description = "  New description  "
        });

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var updated = Assert.IsType<ProjectResponse>(ok.Value);
        Assert.Equal("Keep name", updated.Name);
        Assert.Equal("New description", updated.Description);
        Assert.Equal(dueDate, updated.DueDate);
    }

    [Fact]
    public async Task OneUserCannotAccessAnotherUsersProject()
    {
        await using var context = CreateContext();
        context.Projects.Add(new Project { Id = 20, Name = "Private", OwnerId = 2 });
        await context.SaveChangesAsync();

        var controller = CreateController(context, 1);

        var getResponse = await controller.GetProject(20);
        var updateResponse = await controller.UpdateProject(20, new UpdateProjectRequest { Name = "Changed" });

        Assert.IsType<NotFoundResult>(getResponse.Result);
        Assert.IsType<NotFoundResult>(updateResponse.Result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-an-integer")]
    [InlineData("0")]
    public async Task MissingOrInvalidUserClaim_ShouldReturnUnauthorized(string? claimValue)
    {
        await using var context = CreateContext();
        var controller = CreateController(context, claimValue);

        var response = await controller.GetProjects();

        Assert.IsType<UnauthorizedResult>(response.Result);
    }

    [Fact]
    public void ProjectRequests_ShouldNormalizeTrimmedValuesBeforeValidation()
    {
        var create = new CreateProjectRequest
        {
            Name = "  Valid name  ",
            Description = "  "
        };
        var update = new UpdateProjectRequest
        {
            Name = "  Updated name  ",
            Description = "  Updated description  "
        };

        Assert.Equal("Valid name", create.Name);
        Assert.Equal(string.Empty, create.Description);
        Assert.Equal("Updated name", update.Name);
        Assert.Equal("Updated description", update.Description);
        Assert.Empty(Validate(create));
        Assert.Empty(Validate(update));
    }

    [Fact]
    public async Task UpdateProject_ShouldNotMutateEntityWhenAnySuppliedFieldIsInvalid()
    {
        await using var context = CreateContext();
        context.Projects.Add(new Project
        {
            Id = 13,
            Name = "Original name",
            Description = "Original description",
            DueDate = DateTime.UtcNow.AddDays(3),
            OwnerId = 1
        });
        await context.SaveChangesAsync();

        var controller = CreateController(context, 1);
        var response = await controller.UpdateProject(13, new UpdateProjectRequest
        {
            Name = "  Valid replacement  ",
            Description = new string('x', 1001)
        });

        Assert.IsType<BadRequestObjectResult>(response.Result);

        var stored = await context.Projects.FindAsync(13);
        Assert.NotNull(stored);
        Assert.Equal("Original name", stored.Name);
        Assert.Equal("Original description", stored.Description);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static ProjectsController CreateController(ApplicationDbContext context, int userId)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        };

        return CreateController(context, claims);
    }

    private static ProjectsController CreateController(ApplicationDbContext context, string? claimValue)
    {
        var claims = claimValue is null
            ? Array.Empty<Claim>()
            : new[] { new Claim(ClaimTypes.NameIdentifier, claimValue) };

        return CreateController(context, claims);
    }

    private static ProjectsController CreateController(ApplicationDbContext context, Claim[] claims)
    {
        var controller = new ProjectsController(context);
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

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskFlow.API.Data;
using TaskFlow.API.DTOs;
using TaskFlow.API.Models;

namespace TaskFlow.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ProjectsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProjectResponse>>> GetProjects()
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var projects = await _context.Projects
            .AsNoTracking()
            .Where(p => p.OwnerId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new ProjectResponse
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                CreatedAt = p.CreatedAt,
                DueDate = p.DueDate,
                OwnerId = p.OwnerId,
                TaskCount = p.Tasks.Count
            })
            .ToListAsync();

        return Ok(projects);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProjectResponse>> GetProject(int id)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var project = await _context.Projects
            .AsNoTracking()
            .Where(p => p.Id == id && p.OwnerId == userId)
            .Select(p => new ProjectResponse
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                CreatedAt = p.CreatedAt,
                DueDate = p.DueDate,
                OwnerId = p.OwnerId,
                TaskCount = p.Tasks.Count
            })
            .FirstOrDefaultAsync();

        if (project is null)
        {
            return NotFound();
        }

        return Ok(project);
    }

    [HttpPost]
    public async Task<ActionResult<ProjectResponse>> CreateProject([FromBody] CreateProjectRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ValidationError(nameof(request.Name), "Project name is required and cannot contain only whitespace.");
        }

        var name = request.Name.Trim();
        if (name.Length > 100)
        {
            return ValidationError(nameof(request.Name), "Project name cannot exceed 100 characters.");
        }

        if (request.Description is not null && request.Description.Length > 1000)
        {
            return ValidationError(nameof(request.Description), "Project description cannot exceed 1000 characters.");
        }

        var project = new Project
        {
            Name = name,
            Description = request.Description?.Trim(),
            DueDate = request.DueDate,
            OwnerId = userId
        };

        _context.Projects.Add(project);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetProject), new { id = project.Id }, new ProjectResponse
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            CreatedAt = project.CreatedAt,
            DueDate = project.DueDate,
            OwnerId = project.OwnerId,
            TaskCount = 0
        });
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ProjectResponse>> UpdateProject(int id, [FromBody] UpdateProjectRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == userId);
        if (project is null)
        {
            return NotFound();
        }

        string? updatedName = null;
        if (request.Name is not null)
        {
            var name = request.Name.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                return ValidationError(nameof(request.Name), "Project name cannot contain only whitespace.");
            }

            if (name.Length > 100)
            {
                return ValidationError(nameof(request.Name), "Project name cannot exceed 100 characters.");
            }

            updatedName = name;
        }

        string? updatedDescription = null;
        if (request.DescriptionSpecified && request.Description is not null)
        {
            var description = request.Description.Trim();
            if (description.Length > 1000)
            {
                return ValidationError(nameof(request.Description), "Project description cannot exceed 1000 characters.");
            }

            updatedDescription = description;
        }

        if (updatedName is not null)
        {
            project.Name = updatedName;
        }

        if (request.DescriptionSpecified)
        {
            project.Description = updatedDescription;
        }

        if (request.DueDateSpecified)
        {
            project.DueDate = request.DueDate;
        }

        await _context.SaveChangesAsync();

        return Ok(new ProjectResponse
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            CreatedAt = project.CreatedAt,
            DueDate = project.DueDate,
            OwnerId = project.OwnerId,
            TaskCount = await _context.Tasks.CountAsync(t => t.ProjectId == project.Id)
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteProject(int id)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == userId);
        if (project is null)
        {
            return NotFound();
        }

        _context.Projects.Remove(project);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private bool TryGetCurrentUserId(out int userId)
    {
        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claimValue, out userId) && userId > 0;
    }

    private ActionResult ValidationError(string field, string message)
    {
        ModelState.AddModelError(field, message);
        return ValidationProblem();
    }
}

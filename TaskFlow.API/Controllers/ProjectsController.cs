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
        var userId = GetCurrentUserId();

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
        var userId = GetCurrentUserId();

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
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Project name is required." });
        }

        var project = new Project
        {
            Name = request.Name.Trim(),
            Description = request.Description,
            DueDate = request.DueDate,
            OwnerId = GetCurrentUserId()
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
        var userId = GetCurrentUserId();

        var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == userId);
        if (project is null)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            project.Name = request.Name.Trim();
        }

        if (request.Description is not null)
        {
            project.Description = request.Description;
        }

        if (request.DueDate is not null)
        {
            project.DueDate = request.DueDate.Value;
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
        var userId = GetCurrentUserId();

        var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == userId);
        if (project is null)
        {
            return NotFound();
        }

        _context.Projects.Remove(project);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private int GetCurrentUserId()
    {
        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.Parse(claimValue ?? "0");
    }
}

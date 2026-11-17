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
[Route("api/projects/{projectId:int}/tasks")]
public class TasksController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public TasksController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TaskResponse>>> GetTasks(int projectId)
    {
        var userId = GetCurrentUserId();

        var projectExists = await _context.Projects.AnyAsync(p => p.Id == projectId && p.OwnerId == userId);
        if (!projectExists)
        {
            return NotFound();
        }

        var tasks = await _context.Tasks
            .AsNoTracking()
            .Where(t => t.ProjectId == projectId)
            .Include(t => t.Assignee)
            .OrderByDescending(t => t.UpdatedAt)
            .Select(t => new TaskResponse
            {
                Id = t.Id,
                ProjectId = t.ProjectId,
                Title = t.Title,
                Description = t.Description,
                Status = t.Status,
                Priority = t.Priority,
                DueDate = t.DueDate,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt,
                AssigneeId = t.AssigneeId,
                Assignee = t.Assignee == null ? null : new UserSummaryDto
                {
                    Id = t.Assignee.Id,
                    Username = t.Assignee.Username,
                    Email = t.Assignee.Email
                }
            })
            .ToListAsync();

        return Ok(tasks);
    }

    [HttpPost]
    public async Task<ActionResult<TaskResponse>> CreateTask(int projectId, [FromBody] CreateTaskRequest request)
    {
        var userId = GetCurrentUserId();

        var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId && p.OwnerId == userId);
        if (project is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest(new { message = "Task title is required." });
        }

        if (request.AssigneeId.HasValue)
        {
            var assigneeExists = await _context.Users.AnyAsync(u => u.Id == request.AssigneeId.Value && u.Id != userId);
            if (!assigneeExists)
            {
                return BadRequest(new { message = "Assignee must be a valid user in the system." });
            }
        }

        var task = new TaskItem
        {
            ProjectId = projectId,
            Title = request.Title.Trim(),
            Description = request.Description,
            Status = request.Status,
            Priority = request.Priority,
            DueDate = request.DueDate,
            AssigneeId = request.AssigneeId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Tasks.Add(task);
        await _context.SaveChangesAsync();

        var createdTask = await _context.Tasks
            .AsNoTracking()
            .Include(t => t.Assignee)
            .Where(t => t.Id == task.Id)
            .Select(t => new TaskResponse
            {
                Id = t.Id,
                ProjectId = t.ProjectId,
                Title = t.Title,
                Description = t.Description,
                Status = t.Status,
                Priority = t.Priority,
                DueDate = t.DueDate,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt,
                AssigneeId = t.AssigneeId,
                Assignee = t.Assignee == null ? null : new UserSummaryDto
                {
                    Id = t.Assignee.Id,
                    Username = t.Assignee.Username,
                    Email = t.Assignee.Email
                }
            })
            .FirstAsync();

        return CreatedAtAction(nameof(GetTaskById), new { projectId, taskId = task.Id }, createdTask);
    }

    [HttpGet("{taskId:int}")]
    public async Task<ActionResult<TaskResponse>> GetTaskById(int projectId, int taskId)
    {
        var userId = GetCurrentUserId();

        var task = await _context.Tasks
            .AsNoTracking()
            .Include(t => t.Assignee)
            .Where(t => t.Id == taskId && t.ProjectId == projectId && t.Project.OwnerId == userId)
            .Select(t => new TaskResponse
            {
                Id = t.Id,
                ProjectId = t.ProjectId,
                Title = t.Title,
                Description = t.Description,
                Status = t.Status,
                Priority = t.Priority,
                DueDate = t.DueDate,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt,
                AssigneeId = t.AssigneeId,
                Assignee = t.Assignee == null ? null : new UserSummaryDto
                {
                    Id = t.Assignee.Id,
                    Username = t.Assignee.Username,
                    Email = t.Assignee.Email
                }
            })
            .FirstOrDefaultAsync();

        if (task is null)
        {
            return NotFound();
        }

        return Ok(task);
    }

    [HttpPut("{taskId:int}")]
    public async Task<ActionResult<TaskResponse>> UpdateTask(int projectId, int taskId, [FromBody] UpdateTaskRequest request)
    {
        var userId = GetCurrentUserId();

        var task = await _context.Tasks
            .Include(t => t.Assignee)
            .FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId && t.Project.OwnerId == userId);

        if (task is null)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            task.Title = request.Title.Trim();
        }

        if (request.Description is not null)
        {
            task.Description = request.Description;
        }

        if (request.Status.HasValue)
        {
            task.Status = request.Status.Value;
        }

        if (request.Priority.HasValue)
        {
            task.Priority = request.Priority.Value;
        }

        if (request.DueDate.HasValue)
        {
            task.DueDate = request.DueDate;
        }

        if (request.AssigneeId.HasValue)
        {
            var assigneeExists = await _context.Users.AnyAsync(u => u.Id == request.AssigneeId.Value);
            if (!assigneeExists)
            {
                return BadRequest(new { message = "Assignee must be a valid user in the system." });
            }

            task.AssigneeId = request.AssigneeId.Value;
        }

        task.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new TaskResponse
        {
            Id = task.Id,
            ProjectId = task.ProjectId,
            Title = task.Title,
            Description = task.Description,
            Status = task.Status,
            Priority = task.Priority,
            DueDate = task.DueDate,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt,
            AssigneeId = task.AssigneeId,
            Assignee = task.Assignee == null ? null : new UserSummaryDto
            {
                Id = task.Assignee.Id,
                Username = task.Assignee.Username,
                Email = task.Assignee.Email
            }
        });
    }

    [HttpDelete("{taskId:int}")]
    public async Task<IActionResult> DeleteTask(int projectId, int taskId)
    {
        var userId = GetCurrentUserId();

        var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId && t.Project.OwnerId == userId);
        if (task is null)
        {
            return NotFound();
        }

        _context.Tasks.Remove(task);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private int GetCurrentUserId()
    {
        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.Parse(claimValue ?? "0");
    }
}

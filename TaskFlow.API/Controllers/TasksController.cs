using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskFlow.API.Data;
using TaskFlow.API.DTOs;
using TaskFlow.API.Models;
using TaskFlowTaskStatus = TaskFlow.API.Models.TaskStatus;

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
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var projectExists = await _context.Projects.AnyAsync(
            p => p.Id == projectId && p.OwnerId == userId);

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

    [HttpGet("{taskId:int}")]
    public async Task<ActionResult<TaskResponse>> GetTaskById(int projectId, int taskId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var task = await _context.Tasks
            .AsNoTracking()
            .Include(t => t.Assignee)
            .Where(t =>
                t.Id == taskId &&
                t.ProjectId == projectId &&
                t.Project.OwnerId == userId)
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

    [HttpPost]
    public async Task<ActionResult<TaskResponse>> CreateTask(
        int projectId,
        [FromBody] CreateTaskRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var project = await _context.Projects.FirstOrDefaultAsync(
            p => p.Id == projectId && p.OwnerId == userId);

        if (project is null)
        {
            return NotFound();
        }

        var title = request.Title?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(title))
        {
            return BadRequest(new { message = "Task title is required and cannot contain only whitespace." });
        }

        if (title.Length > 150)
        {
            return BadRequest(new { message = "Task title cannot exceed 150 characters." });
        }

        var description = request.Description?.Trim();
        if (description is not null && description.Length > 2000)
        {
            return BadRequest(new { message = "Task description cannot exceed 2000 characters." });
        }

        if (!Enum.IsDefined(request.Status))
        {
            return BadRequest(new { message = "Invalid task status." });
        }

        if (!Enum.IsDefined(request.Priority))
        {
            return BadRequest(new { message = "Invalid task priority." });
        }

        if (request.AssigneeId.HasValue)
        {
            var assigneeExists = await _context.Users.AnyAsync(
                u => u.Id == request.AssigneeId.Value);

            if (!assigneeExists)
            {
                return BadRequest(new { message = "Assignee must be a valid user in the system." });
            }
        }

        var task = new TaskItem
        {
            ProjectId = projectId,
            Title = title,
            Description = description,
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

    [HttpPut("{taskId:int}")]
    public async Task<ActionResult<TaskResponse>> UpdateTask(
        int projectId,
        int taskId,
        [FromBody] UpdateTaskRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var task = await _context.Tasks
            .Include(t => t.Assignee)
            .FirstOrDefaultAsync(t =>
                t.Id == taskId &&
                t.ProjectId == projectId &&
                t.Project.OwnerId == userId);

        if (task is null)
        {
            return NotFound();
        }

        string? updatedTitle = null;
        if (request.Title is not null)
        {
            var title = request.Title.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                return BadRequest(new { message = "Task title cannot contain only whitespace." });
            }

            if (title.Length > 150)
            {
                return BadRequest(new { message = "Task title cannot exceed 150 characters." });
            }

            updatedTitle = title;
        }

        string? updatedDescription = null;
        if (request.Description is not null)
        {
            var description = request.Description.Trim();
            if (description.Length > 2000)
            {
                return BadRequest(new { message = "Task description cannot exceed 2000 characters." });
            }

            updatedDescription = description;
        }

        TaskFlowTaskStatus? updatedStatus = null;
        if (request.Status.HasValue)
        {
            if (!Enum.IsDefined(request.Status.Value))
            {
                return BadRequest(new { message = "Invalid task status." });
            }

            updatedStatus = request.Status.Value;
        }

        TaskPriority? updatedPriority = null;
        if (request.Priority.HasValue)
        {
            if (!Enum.IsDefined(request.Priority.Value))
            {
                return BadRequest(new { message = "Invalid task priority." });
            }

            updatedPriority = request.Priority.Value;
        }

        int? updatedAssigneeId = null;
        if (request.AssigneeId.HasValue)
        {
            var assigneeExists = await _context.Users.AnyAsync(
                u => u.Id == request.AssigneeId.Value);

            if (!assigneeExists)
            {
                return BadRequest(new { message = "Assignee must be a valid user in the system." });
            }

            updatedAssigneeId = request.AssigneeId.Value;
        }

        var updatedDueDate = request.DueDate;

        if (updatedTitle is not null)
        {
            task.Title = updatedTitle;
        }

        if (request.Description is not null)
        {
            task.Description = updatedDescription;
        }

        if (updatedStatus.HasValue)
        {
            task.Status = updatedStatus.Value;
        }

        if (updatedPriority.HasValue)
        {
            task.Priority = updatedPriority.Value;
        }

        if (updatedDueDate is not null)
        {
            task.DueDate = updatedDueDate.Value;
        }

        if (request.AssigneeId.HasValue)
        {
            task.AssigneeId = updatedAssigneeId;
        }

        task.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var assignee = task.Assignee;
        if (request.AssigneeId.HasValue)
        {
            assignee = await _context.Users.AsNoTracking().FirstOrDefaultAsync(
                u => u.Id == request.AssigneeId.Value);
        }

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
            Assignee = assignee == null ? null : new UserSummaryDto
            {
                Id = assignee.Id,
                Username = assignee.Username,
                Email = assignee.Email
            }
        });
    }

    [HttpDelete("{taskId:int}")]
    public async Task<IActionResult> DeleteTask(int projectId, int taskId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var task = await _context.Tasks.FirstOrDefaultAsync(t =>
            t.Id == taskId &&
            t.ProjectId == projectId &&
            t.Project.OwnerId == userId);

        if (task is null)
        {
            return NotFound();
        }

        _context.Tasks.Remove(task);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private bool TryGetCurrentUserId(out int userId)
    {
        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claimValue, out userId) && userId > 0;
    }
}

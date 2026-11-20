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
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public DashboardController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<DashboardResponse>> GetDashboard()
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var totalProjects = await _context.Projects.CountAsync(p => p.OwnerId == userId);
        var allTasks = await _context.Tasks
            .AsNoTracking()
            .Where(t => t.Project.OwnerId == userId)
            .Include(t => t.Assignee)
            .OrderByDescending(t => t.UpdatedAt)
            .ToListAsync();

        var response = new DashboardResponse
        {
            TotalProjects = totalProjects,
            TotalTasks = allTasks.Count,
            CompletedTasks = allTasks.Count(t => t.Status == TaskFlowTaskStatus.Done),
            InProgressTasks = allTasks.Count(t => t.Status == TaskFlowTaskStatus.InProgress),
            OverdueTasks = allTasks.Count(t => t.DueDate.HasValue && t.DueDate.Value < DateTime.UtcNow && t.Status != TaskFlowTaskStatus.Done),
            RecentTasks = allTasks.Take(5).Select(t => new TaskResponse
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
                Assignee = t.Assignee == null ? null : new TaskAssigneeDto
                {
                    Id = t.Assignee.Id,
                    Username = t.Assignee.Username
                }
            }).ToList()
        };

        return Ok(response);
    }

    private bool TryGetCurrentUserId(out int userId)
    {
        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claimValue, out userId) && userId > 0;
    }
}

public class DashboardResponse
{
    public int TotalProjects { get; set; }
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int InProgressTasks { get; set; }
    public int OverdueTasks { get; set; }
    public List<TaskResponse> RecentTasks { get; set; } = new();
}

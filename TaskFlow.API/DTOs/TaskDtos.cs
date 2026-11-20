using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using TaskFlow.API.Models;
using TaskFlowTaskStatus = TaskFlow.API.Models.TaskStatus;

namespace TaskFlow.API.DTOs;

public class CreateTaskRequest
{
    private string _title = string.Empty;
    private string? _description;

    [Required(ErrorMessage = "Task title is required.")]
    [StringLength(150, MinimumLength = 1, ErrorMessage = "Task title must be between 1 and 150 characters.")]
    public string Title
    {
        get => _title;
        set => _title = value?.Trim() ?? string.Empty;
    }

    [StringLength(2000, ErrorMessage = "Task description cannot exceed 2000 characters.")]
    public string? Description
    {
        get => _description;
        set => _description = value?.Trim();
    }

    public TaskFlowTaskStatus Status { get; set; } = TaskFlowTaskStatus.ToDo;
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public DateTime? DueDate { get; set; }
    public int? AssigneeId { get; set; }
}

public class UpdateTaskRequest
{
    private string? _title;
    private string? _description;
    private DateTime? _dueDate;
    private int? _assigneeId;

    [StringLength(150, MinimumLength = 1, ErrorMessage = "Task title must be between 1 and 150 characters.")]
    public string? Title
    {
        get => _title;
        set => _title = value?.Trim();
    }

    [StringLength(2000, ErrorMessage = "Task description cannot exceed 2000 characters.")]
    public string? Description
    {
        get => _description;
        set
        {
            DescriptionSpecified = true;
            _description = value?.Trim();
        }
    }

    [JsonIgnore]
    public bool DescriptionSpecified { get; private set; }

    public TaskFlowTaskStatus? Status { get; set; }
    public TaskPriority? Priority { get; set; }

    public DateTime? DueDate
    {
        get => _dueDate;
        set
        {
            DueDateSpecified = true;
            _dueDate = value;
        }
    }

    [JsonIgnore]
    public bool DueDateSpecified { get; private set; }

    public int? AssigneeId
    {
        get => _assigneeId;
        set
        {
            AssigneeIdSpecified = true;
            _assigneeId = value;
        }
    }

    [JsonIgnore]
    public bool AssigneeIdSpecified { get; private set; }
}

public class TaskResponse
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskFlowTaskStatus Status { get; set; }
    public TaskPriority Priority { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? AssigneeId { get; set; }
    public TaskAssigneeDto? Assignee { get; set; }
}

public class TaskAssigneeDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
}

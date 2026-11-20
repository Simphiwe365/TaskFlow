using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TaskFlow.API.DTOs;

public class CreateProjectRequest
{
    private string _name = string.Empty;
    private string? _description;

    [Required(ErrorMessage = "Project name is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Project name must be between 1 and 100 characters.")]
    public string Name
    {
        get => _name;
        set => _name = value?.Trim() ?? string.Empty;
    }

    [StringLength(1000, ErrorMessage = "Project description cannot exceed 1000 characters.")]
    public string? Description
    {
        get => _description;
        set => _description = value?.Trim();
    }

    public DateTime? DueDate { get; set; }
}

public class UpdateProjectRequest
{
    private string? _name;
    private string? _description;
    private DateTime? _dueDate;

    [StringLength(100, MinimumLength = 1, ErrorMessage = "Project name must be between 1 and 100 characters.")]
    public string? Name
    {
        get => _name;
        set => _name = value?.Trim();
    }

    [StringLength(1000, ErrorMessage = "Project description cannot exceed 1000 characters.")]
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
}

public class ProjectResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public int OwnerId { get; set; }
    public int TaskCount { get; set; }
}

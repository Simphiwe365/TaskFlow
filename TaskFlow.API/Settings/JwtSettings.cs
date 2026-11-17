namespace TaskFlow.API.Settings;

public class JwtSettings
{
    public string Key { get; set; } = "taskflow-dev-secret-key-very-long-and-secure-1234567890";
    public string Issuer { get; set; } = "TaskFlowAPI";
    public string Audience { get; set; } = "TaskFlowClient";
    public int ExpiryMinutes { get; set; } = 60;
}

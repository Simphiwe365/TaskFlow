namespace TaskFlow.API.Settings;

public class JwtSettings
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "TaskFlowAPI";
    public string Audience { get; set; } = "TaskFlowClient";
    public int ExpiryMinutes { get; set; } = 60;
}

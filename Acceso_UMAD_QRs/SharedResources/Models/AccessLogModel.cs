namespace SharedResources.Models;

public class AccessLogModel
{
    public int IdLog { get; set; }

    public int IdUser { get; set; }

    public string AccessPoint { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; }

    public UserModel User { get; set; } = null!;
}
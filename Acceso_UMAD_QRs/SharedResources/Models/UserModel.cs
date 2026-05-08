namespace SharedResources.Models;

public class UserModel
{
    public int IdUser { get; set; }

    public int IdRole { get; set; }

    public string? StudentId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public RoleModel Role { get; set; } = null!;

    public ICollection<AccessTokenModel> Tokens { get; set; } = [];
    public ICollection<AccessLogModel> AccessLogs { get; set; } = [];
}
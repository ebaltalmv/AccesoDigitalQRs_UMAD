namespace SharedResources.Models;

public class RoleModel
{
    public int IdRole { get; set; }

    public string RoleName { get; set; } = string.Empty;

    public ICollection<UserModel> Users { get; set; } = [];
}
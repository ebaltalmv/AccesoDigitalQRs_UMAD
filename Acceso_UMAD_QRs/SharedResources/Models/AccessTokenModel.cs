namespace SharedResources.Models;

public class AccessTokenModel
{
    public int IdToken { get; set; }

    public int IdUser { get; set; }

    public string QrHash { get; set; } = string.Empty;

    public DateTime ExpirationDate { get; set; }

    public bool IsActive { get; set; }

    public UserModel User { get; set; } = null!;
}
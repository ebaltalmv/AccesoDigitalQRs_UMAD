namespace SharedResources.Models;

public class RolModel
{
    public int IdRol { get; set; }

    public string NombreRol { get; set; } = string.Empty;

    public ICollection<UsuarioModel> Usuarios { get; set; } = [];
}
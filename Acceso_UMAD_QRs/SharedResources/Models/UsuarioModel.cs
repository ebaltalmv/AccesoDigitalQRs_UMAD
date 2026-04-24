namespace SharedResources.Models;

public class UsuarioModel
{
    public int IdUsuario { get; set; }

    public int IdRol { get; set; }

    public string? Matricula { get; set; }

    public string NombreCompleto { get; set; } = string.Empty;

    public string Correo { get; set; } = string.Empty;

    public string Contrasena { get; set; } = string.Empty;

    public RolModel Rol { get; set; } = null!;

    public ICollection<TokenAccesoModel> Tokens { get; set; } = [];
    public ICollection<RegistroModel> Registros { get; set; } = [];
}
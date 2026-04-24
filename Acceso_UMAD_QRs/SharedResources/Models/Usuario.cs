namespace SharedResources.Models;

public class Usuario
{
    public int IdUsuario { get; set; }

    public int IdRol { get; set; }

    // Puede ser nulo (?) para invitados según las reglas de negocio
    public string? Matricula { get; set; } 

    public string NombreCompleto { get; set; } = string.Empty;

    public string Correo { get; set; } = string.Empty;

    public string Contrasena { get; set; } = string.Empty;

    // Navegación de relaciones
    public Rol Rol { get; set; } = null!;

    public ICollection<TokenAcceso> Tokens { get; set; } = [];
    public ICollection<Registro> Registros { get; set; } = [];
}
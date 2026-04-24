namespace SharedResources.Models;

public class Rol
{
    public int IdRol { get; set; }

    public string NombreRol { get; set; } = string.Empty;

    // Relación: Un rol puede tener muchos usuarios
    public ICollection<Usuario> Usuarios { get; set; } = [];
}
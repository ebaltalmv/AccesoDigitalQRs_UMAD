namespace SharedResources.Models;

public class TokenAccesoModel
{
    public int IdToken { get; set; }

    public int IdUsuario { get; set; }

    public string HashQr { get; set; } = string.Empty;

    public DateTime FechaExpiracion { get; set; }

    public bool Activo { get; set; }

    public UsuarioModel Usuario { get; set; } = null!;
}
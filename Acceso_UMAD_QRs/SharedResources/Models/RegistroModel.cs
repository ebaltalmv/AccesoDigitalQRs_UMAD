namespace SharedResources.Models;

public class RegistroModel
{
    public int IdRegistro { get; set; }

    public int IdUsuario { get; set; }

    public string PuntoAcceso { get; set; } = string.Empty;

    public DateTime FechaHora { get; set; }

    public UsuarioModel Usuario { get; set; } = null!;
}
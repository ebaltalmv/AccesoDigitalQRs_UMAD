namespace SharedResources.Models;

public class Registro
{
    public int IdRegistro { get; set; }

    public int IdUsuario { get; set; }

    public string PuntoAcceso { get; set; } = string.Empty;

    public DateTime FechaHora { get; set; }

    public Usuario Usuario { get; set; } = null!;
}
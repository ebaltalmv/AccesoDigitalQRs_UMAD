namespace UMADAccesos.Services.Device;

/// <summary>
/// Representa el resultado de una operación de captura de evidencia fotográfica.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public sealed class EvidenciaFotoResult
{
    // Indica si la captura de fotografía finalizó correctamente.
    public bool Exitoso { get; init; }

    // Indica si el usuario canceló la captura de la fotografía.
    public bool Cancelado { get; init; }

    // Ruta relativa del archivo de imagen guardado (relativa a AppDataDirectory).
    public string? RutaRelativa { get; init; }

    // Hash SHA-256 del archivo de imagen para verificación de integridad.
    public string? HashSha256 { get; init; }

    // Mensaje descriptivo del error ocurrido, si aplica.
    public string? MensajeError { get; init; }

    /// <summary>
    /// Crea un resultado exitoso con la ruta relativa y el hash SHA-256 del archivo capturado.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public static EvidenciaFotoResult Exito(string rutaRelativa, string hashSha256) => new()
    {
        Exitoso = true,
        RutaRelativa = rutaRelativa,
        HashSha256 = hashSha256
    };

    /// <summary>
    /// Crea un resultado que indica cancelación por parte del usuario.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public static EvidenciaFotoResult CanceladoPorUsuario() => new()
    {
        Cancelado = true
    };

    /// <summary>
    /// Crea un resultado de error con el mensaje descriptivo proporcionado.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public static EvidenciaFotoResult Error(string mensaje) => new()
    {
        MensajeError = mensaje
    };
}

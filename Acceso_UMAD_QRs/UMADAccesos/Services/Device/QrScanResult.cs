namespace UMADAccesos.Services.Device;

/// <summary>
/// Representa el resultado de una operación de escaneo de código QR, incluyendo éxito, cancelación o error.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public sealed class QrScanResult
{
    // Indica si el escaneo finalizó correctamente y se obtuvo un código.
    public bool Exitoso { get; init; }

    // Indica si el usuario canceló la operación de escaneo.
    public bool Cancelado { get; init; }

    // Cadena del código QR escaneado. Nulo si no se obtuvo resultado.
    public string? CodigoEscaneado { get; init; }

    // Mensaje descriptivo del error ocurrido, si aplica.
    public string? MensajeError { get; init; }

    /// <summary>
    /// Crea un resultado exitoso con el código QR escaneado.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public static QrScanResult Exito(string codigo) => new()
    {
        Exitoso = true,
        CodigoEscaneado = codigo
    };

    /// <summary>
    /// Crea un resultado que indica cancelación por parte del usuario.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public static QrScanResult CanceladoPorUsuario() => new()
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
    public static QrScanResult Error(string mensaje) => new()
    {
        MensajeError = mensaje
    };
}

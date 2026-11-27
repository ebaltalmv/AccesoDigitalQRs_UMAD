namespace UMADAccesos.Services.Auth;

/// <summary>
/// Encapsula el resultado de una operación de autenticación o cambio de sesión controlada.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public sealed class ResultadoAutenticacion
{
    // Indica si la autenticación se completó satisfactoriamente.
    public bool Exitoso { get; init; }

    // Mensaje seguro y legible para el usuario con el resultado de la operación.
    public string Mensaje { get; init; } = string.Empty;

    // Sesión inmutable generada cuando la autenticación fue exitosa; nulo en caso de fallo.
    public SesionUsuario? Sesion { get; init; }

    /// <summary>
    /// Construye un resultado de autenticación exitoso acompañado de la sesión activa generada.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public static ResultadoAutenticacion Exito(SesionUsuario sesion, string mensaje = "Sesión iniciada correctamente.")
    {
        ArgumentNullException.ThrowIfNull(sesion);

        string mensajeNormalizado = string.IsNullOrWhiteSpace(mensaje)
            ? "Sesión iniciada correctamente."
            : mensaje.Trim();

        return new ResultadoAutenticacion
        {
            Exitoso = true,
            Mensaje = mensajeNormalizado,
            Sesion = sesion
        };
    }

    /// <summary>
    /// Construye un resultado de autenticación fallido con un mensaje genérico seguro y sin sesión activa.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public static ResultadoAutenticacion Fallo(string mensaje)
    {
        string mensajeNormalizado = string.IsNullOrWhiteSpace(mensaje)
            ? "No fue posible autenticar la sesión."
            : mensaje.Trim();

        return new ResultadoAutenticacion
        {
            Exitoso = false,
            Mensaje = mensajeNormalizado,
            Sesion = null
        };
    }
}

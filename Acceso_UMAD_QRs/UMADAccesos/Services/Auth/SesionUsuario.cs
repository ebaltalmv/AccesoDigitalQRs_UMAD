using UMADAccesos.Models;

namespace UMADAccesos.Services.Auth;

/// <summary>
/// Representa de forma inmutable la sesión activa de un usuario autenticado sin exponer credenciales ni hashes criptográficos.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public sealed class SesionUsuario
{
    // Identificador numérico local de la persona autenticada en SQLite.
    public int PersonaId { get; init; }

    // Identificador único global de la persona autenticada.
    public Guid IdentificadorGlobal { get; init; }

    // Nombre completo visible de la persona autenticada.
    public string NombreCompleto { get; init; } = string.Empty;

    // Correo electrónico institucional o registrado de la persona autenticada.
    public string Correo { get; init; } = string.Empty;

    // Matrícula o clave institucional opcional asociada a la persona.
    public string? Matricula { get; init; }

    // Rol institucional registrado en la entidad Persona.
    public RolPersona RolInstitucional { get; init; }

    // Rol operativo RBAC asignado a la sesión activa.
    public RolUsuario RolOperativo { get; init; }

    // Fecha y hora UTC en que se inició la sesión.
    public DateTime InicioSesionUtc { get; init; }

    // Fecha y hora UTC en que expira la sesión de forma obligatoria.
    public DateTime ExpiraSesionUtc { get; init; }

    // Indica si la sesión fue creada mediante el modo de prueba restringido a compilaciones de desarrollo.
    public bool EsSesionModoPrueba { get; init; }

    /// <summary>
    /// Evalúa de forma segura (fail-closed) si la sesión ya venció respecto a la marca temporal UTC proporcionada o actual.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public bool EstaExpirada(DateTime? ahoraUtc = null)
    {
        if (ExpiraSesionUtc == default)
        {
            return true;
        }

        DateTime expiracionNormalizada = NormalizarUtc(ExpiraSesionUtc);
        DateTime referenciaUtc = NormalizarUtc(ahoraUtc ?? DateTime.UtcNow);

        if (InicioSesionUtc != default && expiracionNormalizada <= NormalizarUtc(InicioSesionUtc))
        {
            return true;
        }

        return referenciaUtc >= expiracionNormalizada;
    }

    /// <summary>
    /// Normaliza una marca temporal a formato UTC para comparaciones consistentes de expiración.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private static DateTime NormalizarUtc(DateTime fecha)
    {
        return fecha.Kind switch
        {
            DateTimeKind.Utc => fecha,
            DateTimeKind.Local => fecha.ToUniversalTime(),
            _ => DateTime.SpecifyKind(fecha, DateTimeKind.Utc)
        };
    }
}

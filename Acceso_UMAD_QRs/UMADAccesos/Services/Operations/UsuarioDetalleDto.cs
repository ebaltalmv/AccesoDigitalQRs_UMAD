using UMADAccesos.Models;

namespace UMADAccesos.Services.Operations;

/// <summary>
/// Representa la vista detallada y segura de un usuario del sistema sin exponer hashes de contraseña.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public sealed class UsuarioDetalleDto
{
    // Identificador numérico primario del usuario en SQLite.
    public int Id { get; set; }

    // Identificador único global del usuario utilizado para sincronización.
    public Guid IdentificadorGlobal { get; set; }

    // Matrícula o clave institucional opcional asociada al usuario.
    public string? Matricula { get; set; }

    // Nombre completo del usuario registrado.
    public string NombreCompleto { get; set; } = string.Empty;

    // Correo electrónico normalizado en minúsculas utilizado para autenticación.
    public string Correo { get; set; } = string.Empty;

    // Rol institucional asignado al usuario.
    public RolPersona Rol { get; set; }

    // Indica si la cuenta se encuentra habilitada para iniciar sesión y operar.
    public bool Activo { get; set; }

    // Fecha y hora UTC en que se creó la cuenta.
    public DateTime FechaCreacionUtc { get; set; }

    // Fecha y hora UTC de la última actualización registrada sobre la cuenta.
    public DateTime? FechaActualizacionUtc { get; set; }

    // Cantidad actual de tokens de acceso activos y vigentes asociados al usuario.
    public int TokensActivosCount { get; set; }
}

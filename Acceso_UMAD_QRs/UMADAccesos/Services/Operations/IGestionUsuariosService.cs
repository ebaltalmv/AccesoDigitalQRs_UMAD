using UMADAccesos.Models;

namespace UMADAccesos.Services.Operations;

/// <summary>
/// Define el contrato de aprovisionamiento inicial y administración segura de cuentas de usuario en SQLite.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public interface IGestionUsuariosService
{
    /// <summary>
    /// Determina si la instalación requiere configuración inicial por no existir ningún administrador activo.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<bool> RequiereConfiguracionInicialAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea la primera cuenta de administrador activo en una base de datos sin administradores activos.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<UsuarioDetalleDto> CrearAdministradorInicialAsync(
        string nombreCompleto,
        string correo,
        string? matricula,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene el listado completo de usuarios registrados con su conteo de tokens activos sin exponer hashes.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<IReadOnlyList<UsuarioDetalleDto>> ObtenerUsuariosAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea un nuevo usuario previa verificación en base de datos de privilegios de administrador activo.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<UsuarioDetalleDto> CrearUsuarioAsync(
        CrearUsuarioRequest solicitud,
        int administradorPersonaId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Activa o desactiva una cuenta existente protegiendo contra autodesactivación y bloqueo del último administrador activo.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<UsuarioDetalleDto> ActualizarEstadoUsuarioAsync(
        int usuarioId,
        bool activo,
        int administradorPersonaId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Modifica el rol institucional de un usuario impidiendo degradar al último administrador activo del sistema.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<UsuarioDetalleDto> ActualizarRolUsuarioAsync(
        int usuarioId,
        RolPersona nuevoRol,
        int administradorPersonaId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Restablece la contraseña de un usuario almacenando exclusivamente su nuevo hash PBKDF2-SHA256.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task RestablecerContrasenaAsync(
        int usuarioId,
        string nuevaContrasena,
        int administradorPersonaId,
        CancellationToken cancellationToken = default);
}

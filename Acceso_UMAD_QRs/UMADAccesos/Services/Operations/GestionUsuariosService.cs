using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UMADAccesos.Data;
using UMADAccesos.Models;
using UMADAccesos.Services.Security;

namespace UMADAccesos.Services.Operations;

/// <summary>
/// Implementa el aprovisionamiento del administrador inicial y la gestión segura de usuarios en SQLite.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public sealed class GestionUsuariosService : IGestionUsuariosService
{
    // Longitud mínima exigida para contraseñas de usuario.
    private const int LongitudMinimaPassword = 8;

    // Longitud máxima permitida para el nombre completo conforme al esquema SQLite.
    private const int LongitudMaximaNombre = 150;

    // Longitud máxima permitida para el correo electrónico conforme al esquema SQLite.
    private const int LongitudMaximaCorreo = 150;

    // Longitud máxima permitida para la matrícula conforme al esquema SQLite.
    private const int LongitudMaximaMatricula = 50;

    // Semáforo estático para serializar el aprovisionamiento del administrador inicial y evitar condiciones de carrera.
    private static readonly SemaphoreSlim BloqueoConfiguracionInicial = new(1, 1);

    // Fábrica de contextos EF Core para operaciones aisladas sobre SQLite.
    private readonly IDbContextFactory<UmadDbContext> _dbContextFactory;

    // Servicio criptográfico encargado de derivar hashes PBKDF2-SHA256 de contraseñas.
    private readonly IPasswordHasher _passwordHasher;

    // Servicio de emisión de tokens digitales QR firmados.
    private readonly ITokenAccesoService _tokenAccesoService;

    // Registrador de eventos de auditoría y gestión de cuentas sin exposición de credenciales.
    private readonly ILogger<GestionUsuariosService> _logger;

    /// <summary>
    /// Inicializa una nueva instancia del servicio de gestión y aprovisionamiento de usuarios.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public GestionUsuariosService(
        IDbContextFactory<UmadDbContext> dbContextFactory,
        IPasswordHasher passwordHasher,
        ITokenAccesoService tokenAccesoService,
        ILogger<GestionUsuariosService> logger)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _tokenAccesoService = tokenAccesoService ?? throw new ArgumentNullException(nameof(tokenAccesoService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retorna verdadero únicamente cuando no existe ningún registro en Personas con rol Administrador y estado activo.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<bool> RequiereConfiguracionInicialAsync(CancellationToken cancellationToken = default)
    {
        await using UmadDbContext db = await _dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        bool existeAdministradorActivo = await db.Personas
            .AsNoTracking()
            .AnyAsync(p => p.Rol == RolPersona.Administrador && p.Activo, cancellationToken)
            .ConfigureAwait(false);

        return !existeAdministradorActivo;
    }

    /// <summary>
    /// Crea de forma segura y atómica la cuenta del administrador inicial cuando el sistema carece de administradores activos.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<UsuarioDetalleDto> CrearAdministradorInicialAsync(
        string nombreCompleto,
        string correo,
        string? matricula,
        string password,
        CancellationToken cancellationToken = default)
    {
        string nombreNormalizado = ValidarYNormalizarNombre(nombreCompleto, nameof(nombreCompleto));
        string correoNormalizado = ValidarYNormalizarCorreo(correo, nameof(correo));
        string? matriculaNormalizada = ValidarYNormalizarMatricula(matricula, nameof(matricula));
        ValidarPassword(password, nameof(password));

        await BloqueoConfiguracionInicial.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using UmadDbContext db = await _dbContextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);

            bool existeAdministradorActivo = await db.Personas
                .AsNoTracking()
                .AnyAsync(p => p.Rol == RolPersona.Administrador && p.Activo, cancellationToken)
                .ConfigureAwait(false);

            if (existeAdministradorActivo)
            {
                throw new InvalidOperationException("Ya existe al menos un administrador activo configurado en el sistema.");
            }

            await ValidarUnicidadCorreoYMatriculaAsync(db, correoNormalizado, matriculaNormalizada, cancellationToken)
                .ConfigureAwait(false);

            Persona administrador = new()
            {
                IdentificadorGlobal = Guid.NewGuid(),
                Matricula = matriculaNormalizada,
                NombreCompleto = nombreNormalizado,
                Correo = correoNormalizado,
                PasswordHash = _passwordHasher.HashPassword(password),
                Rol = RolPersona.Administrador,
                Activo = true,
                FechaCreacionUtc = DateTime.UtcNow
            };

            db.Personas.Add(administrador);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Administrador inicial aprovisionado correctamente con PersonaId={PersonaId}.",
                administrador.Id);

            return MapearDetalle(administrador, tokensActivosCount: 0);
        }
        finally
        {
            BloqueoConfiguracionInicial.Release();
        }
    }

    /// <summary>
    /// Obtiene todos los usuarios registrados ordenados por nombre con su conteo vigente de tokens activos.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<IReadOnlyList<UsuarioDetalleDto>> ObtenerUsuariosAsync(CancellationToken cancellationToken = default)
    {
        DateTime ahoraUtc = DateTime.UtcNow;

        await using UmadDbContext db = await _dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        return await db.Personas
            .AsNoTracking()
            .OrderBy(p => p.NombreCompleto)
            .ThenBy(p => p.Id)
            .Select(p => new UsuarioDetalleDto
            {
                Id = p.Id,
                IdentificadorGlobal = p.IdentificadorGlobal,
                Matricula = p.Matricula,
                NombreCompleto = p.NombreCompleto,
                Correo = p.Correo,
                Rol = p.Rol,
                Activo = p.Activo,
                FechaCreacionUtc = p.FechaCreacionUtc,
                FechaActualizacionUtc = p.FechaActualizacionUtc,
                TokensActivosCount = p.TokensAcceso.Count(t => !t.Revocado && t.FechaExpiracion >= ahoraUtc)
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Crea un nuevo usuario previa validación de administrador activo, integridad de datos y emisión opcional de token inicial.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<UsuarioDetalleDto> CrearUsuarioAsync(
        CrearUsuarioRequest solicitud,
        int administradorPersonaId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        await using UmadDbContext db = await _dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        await ValidarAdministradorActivoAsync(db, administradorPersonaId, cancellationToken).ConfigureAwait(false);

        if (!Enum.IsDefined(solicitud.Rol))
        {
            throw new ArgumentOutOfRangeException(nameof(solicitud), "El rol institucional especificado no es válido.");
        }

        if (solicitud.EmitirTokenInicial && !Enum.IsDefined(solicitud.TipoPaseInicial))
        {
            throw new ArgumentOutOfRangeException(nameof(solicitud), "El tipo de pase inicial especificado no es válido.");
        }

        string nombreNormalizado = ValidarYNormalizarNombre(solicitud.NombreCompleto, nameof(solicitud));
        string correoNormalizado = ValidarYNormalizarCorreo(solicitud.Correo, nameof(solicitud));
        string? matriculaNormalizada = ValidarYNormalizarMatricula(solicitud.Matricula, nameof(solicitud));
        ValidarPassword(solicitud.Password, nameof(solicitud));

        await ValidarUnicidadCorreoYMatriculaAsync(db, correoNormalizado, matriculaNormalizada, cancellationToken)
            .ConfigureAwait(false);

        Persona persona = new()
        {
            IdentificadorGlobal = Guid.NewGuid(),
            Matricula = matriculaNormalizada,
            NombreCompleto = nombreNormalizado,
            Correo = correoNormalizado,
            PasswordHash = _passwordHasher.HashPassword(solicitud.Password),
            Rol = solicitud.Rol,
            Activo = true,
            FechaCreacionUtc = DateTime.UtcNow
        };

        db.Personas.Add(persona);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        int tokensActivosCount = 0;
        if (solicitud.EmitirTokenInicial)
        {
            await _tokenAccesoService
                .EmitirTokenAsync(persona.Id, solicitud.TipoPaseInicial, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            tokensActivosCount = await ContarTokensActivosAsync(db, persona.Id, cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation(
            "Usuario PersonaId={PersonaId} creado con Rol={Rol} por AdministradorId={AdministradorId}.",
            persona.Id,
            persona.Rol,
            administradorPersonaId);

        return MapearDetalle(persona, tokensActivosCount);
    }

    /// <summary>
    /// Actualiza el estado activo de un usuario asegurando autorización de administrador, no autodesactivación y permanencia de al menos un administrador activo.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<UsuarioDetalleDto> ActualizarEstadoUsuarioAsync(
        int usuarioId,
        bool activo,
        int administradorPersonaId,
        CancellationToken cancellationToken = default)
    {
        await using UmadDbContext db = await _dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        await ValidarAdministradorActivoAsync(db, administradorPersonaId, cancellationToken).ConfigureAwait(false);

        if (usuarioId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(usuarioId), "El identificador de usuario debe ser mayor que cero.");
        }

        if (usuarioId == administradorPersonaId && !activo)
        {
            throw new InvalidOperationException("Un administrador no puede desactivar su propia cuenta.");
        }

        Persona? usuario = await db.Personas
            .FirstOrDefaultAsync(p => p.Id == usuarioId, cancellationToken)
            .ConfigureAwait(false);

        if (usuario is null)
        {
            throw new InvalidOperationException($"No se encontró el usuario con identificador {usuarioId}.");
        }

        if (!activo && usuario.Rol == RolPersona.Administrador && usuario.Activo)
        {
            int administradoresActivos = await db.Personas
                .AsNoTracking()
                .CountAsync(p => p.Rol == RolPersona.Administrador && p.Activo, cancellationToken)
                .ConfigureAwait(false);

            if (administradoresActivos <= 1)
            {
                throw new InvalidOperationException("No se puede desactivar al último administrador activo del sistema.");
            }
        }

        usuario.Activo = activo;
        usuario.FechaActualizacionUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        int tokensActivosCount = await ContarTokensActivosAsync(db, usuario.Id, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Estado del usuario PersonaId={PersonaId} actualizado a Activo={Activo} por AdministradorId={AdministradorId}.",
            usuario.Id,
            usuario.Activo,
            administradorPersonaId);

        return MapearDetalle(usuario, tokensActivosCount);
    }

    /// <summary>
    /// Actualiza el rol institucional de un usuario impidiendo degradar al último administrador activo del sistema.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<UsuarioDetalleDto> ActualizarRolUsuarioAsync(
        int usuarioId,
        RolPersona nuevoRol,
        int administradorPersonaId,
        CancellationToken cancellationToken = default)
    {
        await using UmadDbContext db = await _dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        await ValidarAdministradorActivoAsync(db, administradorPersonaId, cancellationToken).ConfigureAwait(false);

        if (usuarioId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(usuarioId), "El identificador de usuario debe ser mayor que cero.");
        }

        if (!Enum.IsDefined(nuevoRol))
        {
            throw new ArgumentOutOfRangeException(nameof(nuevoRol), "El rol institucional especificado no es válido.");
        }

        Persona? usuario = await db.Personas
            .FirstOrDefaultAsync(p => p.Id == usuarioId, cancellationToken)
            .ConfigureAwait(false);

        if (usuario is null)
        {
            throw new InvalidOperationException($"No se encontró el usuario con identificador {usuarioId}.");
        }

        if (usuario.Rol == RolPersona.Administrador && usuario.Activo && nuevoRol != RolPersona.Administrador)
        {
            int administradoresActivos = await db.Personas
                .AsNoTracking()
                .CountAsync(p => p.Rol == RolPersona.Administrador && p.Activo, cancellationToken)
                .ConfigureAwait(false);

            if (administradoresActivos <= 1)
            {
                throw new InvalidOperationException("No se puede cambiar el rol del último administrador activo del sistema.");
            }
        }

        usuario.Rol = nuevoRol;
        usuario.FechaActualizacionUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        int tokensActivosCount = await ContarTokensActivosAsync(db, usuario.Id, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Rol del usuario PersonaId={PersonaId} actualizado a Rol={Rol} por AdministradorId={AdministradorId}.",
            usuario.Id,
            usuario.Rol,
            administradorPersonaId);

        return MapearDetalle(usuario, tokensActivosCount);
    }

    /// <summary>
    /// Restablece la contraseña de un usuario derivando un nuevo hash seguro PBKDF2-SHA256 previa autorización de administrador activo.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task RestablecerContrasenaAsync(
        int usuarioId,
        string nuevaContrasena,
        int administradorPersonaId,
        CancellationToken cancellationToken = default)
    {
        await using UmadDbContext db = await _dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        await ValidarAdministradorActivoAsync(db, administradorPersonaId, cancellationToken).ConfigureAwait(false);

        if (usuarioId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(usuarioId), "El identificador de usuario debe ser mayor que cero.");
        }

        ValidarPassword(nuevaContrasena, nameof(nuevaContrasena));

        Persona? usuario = await db.Personas
            .FirstOrDefaultAsync(p => p.Id == usuarioId, cancellationToken)
            .ConfigureAwait(false);

        if (usuario is null)
        {
            throw new InvalidOperationException($"No se encontró el usuario con identificador {usuarioId}.");
        }

        usuario.PasswordHash = _passwordHasher.HashPassword(nuevaContrasena);
        usuario.FechaActualizacionUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Contraseña restablecida de forma segura para PersonaId={PersonaId} por AdministradorId={AdministradorId}.",
            usuario.Id,
            administradorPersonaId);
    }

    /// <summary>
    /// Verifica en SQLite que el identificador proporcionado pertenezca a un administrador activo.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private static async Task ValidarAdministradorActivoAsync(
        UmadDbContext db,
        int administradorPersonaId,
        CancellationToken cancellationToken)
    {
        if (administradorPersonaId <= 0)
        {
            throw new UnauthorizedAccessException("Solo un administrador activo puede realizar operaciones de gestión de usuarios.");
        }

        bool esAdministradorActivo = await db.Personas
            .AsNoTracking()
            .AnyAsync(
                p => p.Id == administradorPersonaId && p.Rol == RolPersona.Administrador && p.Activo,
                cancellationToken)
            .ConfigureAwait(false);

        if (!esAdministradorActivo)
        {
            throw new UnauthorizedAccessException("Solo un administrador activo puede realizar operaciones de gestión de usuarios.");
        }
    }

    /// <summary>
    /// Valida la unicidad de correo electrónico y matrícula opcional en SQLite sin distinguir mayúsculas o minúsculas.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private static async Task ValidarUnicidadCorreoYMatriculaAsync(
        UmadDbContext db,
        string correoNormalizado,
        string? matriculaNormalizada,
        CancellationToken cancellationToken)
    {
        bool existeCorreo = await db.Personas
            .AsNoTracking()
            .AnyAsync(p => p.Correo.ToLower() == correoNormalizado, cancellationToken)
            .ConfigureAwait(false);

        if (existeCorreo)
        {
            throw new InvalidOperationException("Ya existe una cuenta registrada con ese correo electrónico.");
        }

        if (matriculaNormalizada is not null)
        {
            string matriculaComparacion = matriculaNormalizada.ToLowerInvariant();
            bool existeMatricula = await db.Personas
                .AsNoTracking()
                .AnyAsync(p => p.Matricula != null && p.Matricula.ToLower() == matriculaComparacion, cancellationToken)
                .ConfigureAwait(false);

            if (existeMatricula)
            {
                throw new InvalidOperationException("La matrícula ingresada ya se encuentra registrada.");
            }
        }
    }

    /// <summary>
    /// Cuenta los tokens activos y vigentes asociados a una persona específica.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private static async Task<int> ContarTokensActivosAsync(
        UmadDbContext db,
        int personaId,
        CancellationToken cancellationToken)
    {
        DateTime ahoraUtc = DateTime.UtcNow;
        return await db.TokensAcceso
            .AsNoTracking()
            .CountAsync(t => t.PersonaId == personaId && !t.Revocado && t.FechaExpiracion >= ahoraUtc, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Valida y normaliza el nombre completo del usuario.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private static string ValidarYNormalizarNombre(string nombreCompleto, string paramName)
    {
        if (string.IsNullOrWhiteSpace(nombreCompleto))
        {
            throw new ArgumentException("El nombre completo es obligatorio.", paramName);
        }

        string normalizado = nombreCompleto.Trim();
        if (normalizado.Length > LongitudMaximaNombre)
        {
            throw new ArgumentException($"El nombre completo no puede exceder {LongitudMaximaNombre} caracteres.", paramName);
        }

        return normalizado;
    }

    /// <summary>
    /// Valida el formato mínimo y normaliza el correo electrónico en minúsculas sin espacios externos.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private static string ValidarYNormalizarCorreo(string correo, string paramName)
    {
        if (string.IsNullOrWhiteSpace(correo))
        {
            throw new ArgumentException("El correo electrónico es obligatorio.", paramName);
        }

        string normalizado = correo.Trim().ToLowerInvariant();
        int indiceArroba = normalizado.IndexOf('@');
        if (indiceArroba <= 0
            || indiceArroba == normalizado.Length - 1
            || normalizado.IndexOf('@', indiceArroba + 1) >= 0
            || normalizado.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("El correo electrónico proporcionado no tiene un formato válido.", paramName);
        }

        if (normalizado.Length > LongitudMaximaCorreo)
        {
            throw new ArgumentException($"El correo electrónico no puede exceder {LongitudMaximaCorreo} caracteres.", paramName);
        }

        return normalizado;
    }

    /// <summary>
    /// Normaliza la matrícula opcional devolviendo nulo cuando está en blanco o validando su longitud máxima.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private static string? ValidarYNormalizarMatricula(string? matricula, string paramName)
    {
        if (string.IsNullOrWhiteSpace(matricula))
        {
            return null;
        }

        string normalizada = matricula.Trim();
        if (normalizada.Length > LongitudMaximaMatricula)
        {
            throw new ArgumentException($"La matrícula no puede exceder {LongitudMaximaMatricula} caracteres.", paramName);
        }

        return normalizada;
    }

    /// <summary>
    /// Valida que la contraseña cumpla la política mínima obligatoria de longitud.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private static void ValidarPassword(string password, string paramName)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < LongitudMinimaPassword)
        {
            throw new ArgumentException($"La contraseña debe contener al menos {LongitudMinimaPassword} caracteres.", paramName);
        }
    }

    /// <summary>
    /// Proyecta una entidad Persona al DTO seguro UsuarioDetalleDto sin exponer información sensible.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private static UsuarioDetalleDto MapearDetalle(Persona persona, int tokensActivosCount)
    {
        return new UsuarioDetalleDto
        {
            Id = persona.Id,
            IdentificadorGlobal = persona.IdentificadorGlobal,
            Matricula = persona.Matricula,
            NombreCompleto = persona.NombreCompleto,
            Correo = persona.Correo,
            Rol = persona.Rol,
            Activo = persona.Activo,
            FechaCreacionUtc = persona.FechaCreacionUtc,
            FechaActualizacionUtc = persona.FechaActualizacionUtc,
            TokensActivosCount = tokensActivosCount
        };
    }
}

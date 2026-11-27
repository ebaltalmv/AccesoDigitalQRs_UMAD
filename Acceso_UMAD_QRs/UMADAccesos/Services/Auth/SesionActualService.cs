using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UMADAccesos.Data;
using UMADAccesos.Models;
using UMADAccesos.Services.Security;

namespace UMADAccesos.Services.Auth;

/// <summary>
/// Administra de forma segura y concurrente la autenticación de usuarios, el ciclo de vida de sesión y el doble blindaje del modo de prueba.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public sealed class SesionActualService : ISesionActualService
{
    // Mensaje genérico único para rechazos de autenticación que evita enumeración de cuentas.
    private const string MensajeCredencialesInvalidas = "Credenciales inválidas o cuenta inactiva.";

    // Fábrica de contextos EF Core para consultas aisladas y seguras sobre SQLite.
    private readonly IDbContextFactory<UmadDbContext> _dbContextFactory;

    // Servicio criptográfico encargado de verificar hashes PBKDF2-SHA256 en tiempo constante.
    private readonly IPasswordHasher _passwordHasher;

    // Motor de autorización RBAC para mapear roles, evaluar permisos y validar propiedad de recursos.
    private readonly IAutorizacionRolService _autorizacionRolService;

    // Servicio de control de entorno y vigencia de sesión.
    private readonly IEntornoEjecucionService _entorno;

    // Registrador de eventos de seguridad sin exposición de contraseñas ni hashes.
    private readonly ILogger<SesionActualService> _logger;

    // Objeto de sincronización dedicado exclusivamente al estado interno de la sesión.
    private readonly object _bloqueoSesion = new();

    // Instantánea inmutable de la sesión actualmente activa en memoria.
    private SesionUsuario? _sesionActual;

    // Evento reactivo invocado fuera del bloqueo ante cualquier transición de sesión.
    public event Action? OnSesionCambiada;

    /// <summary>
    /// Inicializa el servicio de gestión de sesión con sus dependencias de persistencia, criptografía, RBAC y entorno.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public SesionActualService(
        IDbContextFactory<UmadDbContext> dbContextFactory,
        IPasswordHasher passwordHasher,
        IAutorizacionRolService autorizacionRolService,
        IEntornoEjecucionService entornoEjecucionService,
        ILogger<SesionActualService> logger)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _autorizacionRolService = autorizacionRolService ?? throw new ArgumentNullException(nameof(autorizacionRolService));
        _entorno = entornoEjecucionService ?? throw new ArgumentNullException(nameof(entornoEjecucionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // Devuelve la sesión vigente o invalida automáticamente la sesión expirada antes de retornar nulo.
    public SesionUsuario? SesionActual
    {
        get
        {
            bool sesionExpiro = false;
            int? personaIdExpirada = null;

            lock (_bloqueoSesion)
            {
                if (_sesionActual is null)
                {
                    return null;
                }

                if (!_sesionActual.EstaExpirada())
                {
                    return _sesionActual;
                }

                personaIdExpirada = _sesionActual.PersonaId;
                _sesionActual = null;
                sesionExpiro = true;
            }

            if (sesionExpiro)
            {
                _logger.LogInformation(
                    "Sesión expirada e invalidada automáticamente para PersonaId={PersonaId}.",
                    personaIdExpirada);
                NotificarCambioSesion();
            }

            return null;
        }
    }

    // Indica si existe una sesión autenticada y no expirada.
    public bool EstaAutenticado => SesionActual is not null;

    // Obtiene el rol operativo de la sesión vigente o nulo si no hay sesión activa.
    public RolUsuario? RolActual => SesionActual?.RolOperativo;

    /// <summary>
    /// Valida credenciales contra la tabla Personas en SQLite mediante PBKDF2 y crea una sesión vigente en memoria.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<ResultadoAutenticacion> IniciarSesionAsync(
        string correo,
        string password,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(correo) || string.IsNullOrWhiteSpace(password))
        {
            return ResultadoAutenticacion.Fallo(MensajeCredencialesInvalidas);
        }

        string correoNormalizado = correo.Trim().ToLowerInvariant();

        await using UmadDbContext db = await _dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        Persona? persona = await db.Personas
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Correo.ToLower() == correoNormalizado, ct)
            .ConfigureAwait(false);

        if (persona is null || !persona.Activo || string.IsNullOrWhiteSpace(persona.PasswordHash))
        {
            _logger.LogWarning("Intento de inicio de sesión rechazado por credenciales inválidas o cuenta inactiva.");
            return ResultadoAutenticacion.Fallo(MensajeCredencialesInvalidas);
        }

        bool contrasenaValida = _passwordHasher.VerifyPassword(password, persona.PasswordHash);
        if (!contrasenaValida)
        {
            _logger.LogWarning(
                "Intento de inicio de sesión rechazado por verificación criptográfica fallida para PersonaId={PersonaId}.",
                persona.Id);
            return ResultadoAutenticacion.Fallo(MensajeCredencialesInvalidas);
        }

        RolUsuario rolOperativo;
        try
        {
            rolOperativo = _autorizacionRolService.MapearRolUsuario(persona.Rol);
        }
        catch (ArgumentOutOfRangeException)
        {
            _logger.LogWarning(
                "Inicio de sesión denegado por rol institucional no reconocido para PersonaId={PersonaId}.",
                persona.Id);
            return ResultadoAutenticacion.Fallo(MensajeCredencialesInvalidas);
        }

        DateTime ahoraUtc = DateTime.UtcNow;
        SesionUsuario nuevaSesion = CrearSesionUsuario(persona, rolOperativo, ahoraUtc, esSesionModoPrueba: false);

        lock (_bloqueoSesion)
        {
            _sesionActual = nuevaSesion;
        }

        _logger.LogInformation(
            "Inicio de sesión exitoso para PersonaId={PersonaId} con RolOperativo={RolOperativo}.",
            nuevaSesion.PersonaId,
            nuevaSesion.RolOperativo);

        NotificarCambioSesion();
        return ResultadoAutenticacion.Exito(nuevaSesion, "Sesión iniciada correctamente.");
    }

    /// <summary>
    /// Limpia el estado de sesión activa, registra el cierre seguro y notifica a los suscriptores reactivos.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public Task CerrarSesionAsync()
    {
        int? personaIdAnterior;

        lock (_bloqueoSesion)
        {
            personaIdAnterior = _sesionActual?.PersonaId;
            _sesionActual = null;
        }

        if (personaIdAnterior.HasValue)
        {
            _logger.LogInformation(
                "Cierre de sesión completado de forma segura para PersonaId={PersonaId}.",
                personaIdAnterior.Value);
        }
        else
        {
            _logger.LogDebug("Solicitud de cierre de sesión ejecutada sin una sesión previamente activa.");
        }

        NotificarCambioSesion();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Comprueba si la sesión actual vigente posee el permiso solicitado.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public bool TienePermiso(PermisoSistema permiso)
    {
        SesionUsuario? sesion = SesionActual;
        return sesion is not null && _autorizacionRolService.TienePermiso(sesion.RolOperativo, permiso);
    }

    /// <summary>
    /// Exige que la sesión actual vigente cuente con el permiso solicitado o lanza una excepción de autorización.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public void ExigirPermiso(PermisoSistema permiso)
    {
        _autorizacionRolService.ExigirPermiso(SesionActual, permiso);
    }

    /// <summary>
    /// Determina si la sesión actual vigente puede acceder a los recursos de la persona objetivo.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public bool PuedeAccederRecursosDePersona(int personaObjetivoId)
    {
        return _autorizacionRolService.PuedeAccederRecursosDePersona(SesionActual, personaObjetivoId);
    }

    /// <summary>
    /// Permite cambiar a una cuenta demo según rol destino únicamente con doble blindaje: bandera activa y compilación DEBUG o UMAD_DEMO_MODE.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public Task<ResultadoAutenticacion> CambiarRolModoPruebaAsync(
        RolUsuario rolDestino,
        CancellationToken ct = default)
    {
        if (!_entorno.EsModoPruebaHabilitado)
        {
            _logger.LogWarning(
                "Intento bloqueado de cambio de rol en modo de prueba hacia {RolDestino} en entorno {Entorno}.",
                rolDestino,
                _entorno.NombreEntorno);
            return Task.FromResult(ResultadoAutenticacion.Fallo("El modo de prueba no está habilitado en el entorno actual."));
        }

#if DEBUG || UMAD_DEMO_MODE
        return CambiarRolModoPruebaDebugAsync(rolDestino, ct);
#else
        return Task.FromResult(ResultadoAutenticacion.Fallo("El modo de prueba está deshabilitado en compilaciones de producción."));
#endif
    }

#if DEBUG || UMAD_DEMO_MODE
    /// <summary>
    /// Ejecuta el cambio controlado de rol demo en compilaciones DEBUG o UMAD_DEMO_MODE cuando el modo de prueba fue habilitado explícitamente.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private async Task<ResultadoAutenticacion> CambiarRolModoPruebaDebugAsync(
        RolUsuario rolDestino,
        CancellationToken ct)
    {
        if (!Enum.IsDefined(rolDestino))
        {
            return ResultadoAutenticacion.Fallo("El rol de prueba solicitado no es válido.");
        }

        await using UmadDbContext db = await _dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        Persona? personaDemo = await BuscarCuentaDemoPorRolAsync(db, rolDestino, ct).ConfigureAwait(false);

        if (personaDemo is null)
        {
            _logger.LogWarning(
                "No se encontró una cuenta activa disponible para el rol de prueba {RolDestino}.",
                rolDestino);
            return ResultadoAutenticacion.Fallo("No existe una cuenta activa disponible para el rol de prueba seleccionado.");
        }

        RolUsuario rolMapeado = _autorizacionRolService.MapearRolUsuario(personaDemo.Rol);
        DateTime ahoraUtc = DateTime.UtcNow;
        SesionUsuario sesionPrueba = CrearSesionUsuario(personaDemo, rolMapeado, ahoraUtc, esSesionModoPrueba: true);

        lock (_bloqueoSesion)
        {
            _sesionActual = sesionPrueba;
        }

        _logger.LogInformation(
            "Sesión de modo de prueba activada en DEBUG para PersonaId={PersonaId} con RolOperativo={RolOperativo}.",
            sesionPrueba.PersonaId,
            sesionPrueba.RolOperativo);

        NotificarCambioSesion();
        return ResultadoAutenticacion.Exito(sesionPrueba, $"Sesión de prueba iniciada como {sesionPrueba.RolOperativo}.");
    }

    /// <summary>
    /// Localiza en SQLite una persona activa correspondiente al rol operativo de prueba, priorizando las cuentas demo institucionales.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private static async Task<Persona?> BuscarCuentaDemoPorRolAsync(
        UmadDbContext db,
        RolUsuario rolDestino,
        CancellationToken ct)
    {
        (string correoPrioritario, RolPersona[] rolesPermitidos) = rolDestino switch
        {
            RolUsuario.AdministradorAccesos => (
                "admin@umad.edu.mx",
                new[] { RolPersona.Administrador }),
            RolUsuario.GuardiaSeguridad => (
                "guardia@umad.edu.mx",
                new[] { RolPersona.Guardia }),
            RolUsuario.UsuarioComunidad => (
                "estudiante@umad.edu.mx",
                new[]
                {
                    RolPersona.Estudiante,
                    RolPersona.Docente,
                    RolPersona.Administrativo,
                    RolPersona.Visitante
                }),
            _ => (string.Empty, Array.Empty<RolPersona>())
        };

        if (rolesPermitidos.Length == 0)
        {
            return null;
        }

        Persona? personaPrioritaria = await db.Personas
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.Activo
                     && p.Correo.ToLower() == correoPrioritario
                     && rolesPermitidos.Contains(p.Rol),
                ct)
            .ConfigureAwait(false);

        if (personaPrioritaria is not null)
        {
            return personaPrioritaria;
        }

        return await db.Personas
            .AsNoTracking()
            .Where(p => p.Activo && rolesPermitidos.Contains(p.Rol))
            .OrderBy(p => p.Id)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }
#endif

    /// <summary>
    /// Construye la instantánea inmutable de sesión a partir de la entidad Persona sin copiar información sensible.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private SesionUsuario CrearSesionUsuario(
        Persona persona,
        RolUsuario rolOperativo,
        DateTime inicioSesionUtc,
        bool esSesionModoPrueba)
    {
        return new SesionUsuario
        {
            PersonaId = persona.Id,
            IdentificadorGlobal = persona.IdentificadorGlobal,
            NombreCompleto = persona.NombreCompleto,
            Correo = persona.Correo,
            Matricula = persona.Matricula,
            RolInstitucional = persona.Rol,
            RolOperativo = rolOperativo,
            InicioSesionUtc = inicioSesionUtc,
            ExpiraSesionUtc = inicioSesionUtc.Add(_entorno.DuracionSesion),
            EsSesionModoPrueba = esSesionModoPrueba
        };
    }

    /// <summary>
    /// Invoca de forma aislada y segura a cada suscriptor de OnSesionCambiada fuera de bloqueos para evitar deadlocks en Blazor.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private void NotificarCambioSesion()
    {
        Action? manejadores = OnSesionCambiada;
        if (manejadores is null)
        {
            return;
        }

        foreach (Delegate delegado in manejadores.GetInvocationList())
        {
            if (delegado is not Action accion)
            {
                continue;
            }

            try
            {
                accion();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error no controlado en un suscriptor del evento OnSesionCambiada.");
            }
        }
    }
}

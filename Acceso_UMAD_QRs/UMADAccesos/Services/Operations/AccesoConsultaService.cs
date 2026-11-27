using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using UMADAccesos.Data;
using UMADAccesos.Models;
using UMADAccesos.Services.Security;

namespace UMADAccesos.Services.Operations;

/// <summary>
/// Implementa consultas de solo lectura sobre SQLite, lectura segura de evidencias y registro seguro de cuentas nuevas.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public sealed class AccesoConsultaService : IAccesoConsultaService
{
    // Subcarpeta permitida dentro de AppDataDirectory para evidencias fotográficas.
    private const string CarpetaEvidencias = "evidencias";

    // Límite máximo de bytes permitido al leer evidencia para vista previa en WebView.
    private const long TamanoMaximoLecturaBytes = 10L * 1024L * 1024L;

    // Fábrica de contextos para aislamiento de consultas sobre SQLite.
    private readonly IDbContextFactory<UmadDbContext> _dbContextFactory;

    // Servicio criptográfico para derivar hashes PBKDF2-SHA256 en nuevos registros.
    private readonly IPasswordHasher _passwordHasher;

    // Servicio de emisión de tokens digitales firmados.
    private readonly ITokenAccesoService _tokenAccesoService;

    // Registrador de eventos operativos de consulta y alta segura.
    private readonly ILogger<AccesoConsultaService> _logger;

    /// <summary>
    /// Inicializa el servicio de consultas y operaciones complementarias sobre SQLite.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public AccesoConsultaService(
        IDbContextFactory<UmadDbContext> dbContextFactory,
        IPasswordHasher passwordHasher,
        ITokenAccesoService tokenAccesoService,
        ILogger<AccesoConsultaService> logger)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _tokenAccesoService = tokenAccesoService ?? throw new ArgumentNullException(nameof(tokenAccesoService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Consulta el historial de accesos en SQLite aplicando los filtros solicitados y evitando devolver hashes de credenciales.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<IReadOnlyList<RegistroAcceso>> ObtenerRegistrosAccesoAsync(
        int? personaIdFiltro = null,
        DateTime? desdeUtc = null,
        DateTime? hastaUtc = null,
        ResultadoAcceso? resultadoFiltro = null,
        bool? soloPendientesSync = null,
        string? textoBusqueda = null,
        int limite = 200,
        CancellationToken ct = default)
    {
        int limiteEfectivo = Math.Clamp(limite, 1, 500);

        await using UmadDbContext db = await _dbContextFactory
            .CreateDbContextAsync(ct)
            .ConfigureAwait(false);

        IQueryable<RegistroAcceso> consulta = db.RegistrosAcceso
            .AsNoTracking()
            .Include(r => r.Persona)
            .Include(r => r.TokenAcceso)
            .Include(r => r.Guardia)
            .Include(r => r.EntradaRelacionada);

        if (personaIdFiltro.HasValue && personaIdFiltro.Value > 0)
        {
            consulta = consulta.Where(r => r.PersonaId == personaIdFiltro.Value);
        }

        if (desdeUtc.HasValue)
        {
            DateTime inicio = NormalizarUtc(desdeUtc.Value);
            consulta = consulta.Where(r => r.FechaHora >= inicio);
        }

        if (hastaUtc.HasValue)
        {
            DateTime fin = NormalizarUtc(hastaUtc.Value);
            consulta = consulta.Where(r => r.FechaHora <= fin);
        }

        if (resultadoFiltro.HasValue)
        {
            consulta = consulta.Where(r => r.Resultado == resultadoFiltro.Value);
        }

        if (soloPendientesSync == true)
        {
            consulta = consulta.Where(r => !r.Sincronizado || r.EstadoSincronizacion != EstadoSincronizacion.Sincronizado);
        }

        if (!string.IsNullOrWhiteSpace(textoBusqueda))
        {
            string termino = textoBusqueda.Trim().ToLowerInvariant();
            consulta = consulta.Where(r =>
                r.PuntoAcceso.ToLower().Contains(termino) ||
                (r.MotivoResultado != null && r.MotivoResultado.ToLower().Contains(termino)) ||
                (r.Persona != null && (
                    r.Persona.NombreCompleto.ToLower().Contains(termino) ||
                    r.Persona.Correo.ToLower().Contains(termino) ||
                    (r.Persona.Matricula != null && r.Persona.Matricula.ToLower().Contains(termino)))));
        }

        return await consulta
            .OrderByDescending(r => r.FechaHora)
            .ThenByDescending(r => r.Id)
            .Take(limiteEfectivo)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Recupera un registro de acceso específico por su ID entero primario con sus relaciones cargadas.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<RegistroAcceso?> ObtenerRegistroAccesoPorIdAsync(
        int registroId,
        CancellationToken ct = default)
    {
        if (registroId <= 0)
        {
            return null;
        }

        await using UmadDbContext db = await _dbContextFactory
            .CreateDbContextAsync(ct)
            .ConfigureAwait(false);

        return await db.RegistrosAcceso
            .AsNoTracking()
            .Include(r => r.Persona)
            .Include(r => r.TokenAcceso)
            .Include(r => r.Guardia)
            .Include(r => r.EntradaRelacionada)
            .FirstOrDefaultAsync(r => r.Id == registroId, ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Obtiene todos los tokens activos y vigentes en la institución ordenados del más reciente al más antiguo.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<IReadOnlyList<TokenAcceso>> ObtenerTokensActivosGlobalAsync(
        CancellationToken ct = default)
    {
        DateTime ahoraUtc = DateTime.UtcNow;

        await using UmadDbContext db = await _dbContextFactory
            .CreateDbContextAsync(ct)
            .ConfigureAwait(false);

        return await db.TokensAcceso
            .AsNoTracking()
            .Include(t => t.Persona)
            .Where(t => !t.Revocado && t.FechaExpiracion >= ahoraUtc)
            .OrderByDescending(t => t.FechaEmision)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Consulta las solicitudes de renovación pertenecientes a la persona indicada.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<IReadOnlyList<SolicitudRenovacion>> ObtenerRenovacionesPorPersonaAsync(
        int personaId,
        CancellationToken ct = default)
    {
        if (personaId <= 0)
        {
            return Array.Empty<SolicitudRenovacion>();
        }

        await using UmadDbContext db = await _dbContextFactory
            .CreateDbContextAsync(ct)
            .ConfigureAwait(false);

        return await db.SolicitudesRenovacion
            .AsNoTracking()
            .Include(s => s.TokenAccesoOriginal)
            .Include(s => s.TokenAccesoNuevo)
            .Include(s => s.ResueltoPor)
            .Where(s => s.PersonaId == personaId)
            .OrderByDescending(s => s.FechaSolicitudUtc)
            .ThenByDescending(s => s.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Consulta los pases por evento registrados en SQLite, filtrando opcionalmente por titular.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<IReadOnlyList<PaseEvento>> ObtenerPasesEventoAsync(
        int? personaIdFiltro = null,
        CancellationToken ct = default)
    {
        await using UmadDbContext db = await _dbContextFactory
            .CreateDbContextAsync(ct)
            .ConfigureAwait(false);

        IQueryable<PaseEvento> consulta = db.PasesEvento
            .AsNoTracking()
            .Include(p => p.Persona)
            .Include(p => p.CreadoPor)
            .Include(p => p.TokenAcceso);

        if (personaIdFiltro.HasValue && personaIdFiltro.Value > 0)
        {
            consulta = consulta.Where(p => p.PersonaId == personaIdFiltro.Value);
        }

        return await consulta
            .OrderByDescending(p => p.FechaInicioEvento)
            .ThenByDescending(p => p.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Obtiene una lista segura (sin hashes) de personas activas en la institución.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<IReadOnlyList<PersonaResumenDto>> ObtenerPersonasActivasAsync(
        CancellationToken ct = default)
    {
        await using UmadDbContext db = await _dbContextFactory
            .CreateDbContextAsync(ct)
            .ConfigureAwait(false);

        return await db.Personas
            .AsNoTracking()
            .Where(p => p.Activo)
            .OrderBy(p => p.NombreCompleto)
            .Select(p => new PersonaResumenDto
            {
                Id = p.Id,
                IdentificadorGlobal = p.IdentificadorGlobal,
                Matricula = p.Matricula,
                NombreCompleto = p.NombreCompleto,
                Correo = p.Correo,
                Rol = p.Rol,
                Activo = p.Activo
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Devuelve la cantidad de personas registradas en la base SQLite local.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<int> ObtenerCantidadPersonasAsync(CancellationToken ct = default)
    {
        await using UmadDbContext db = await _dbContextFactory
            .CreateDbContextAsync(ct)
            .ConfigureAwait(false);

        return await db.Personas.AsNoTracking().CountAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Registra una cuenta nueva con hash PBKDF2-SHA256 evitando escalada de privilegios y emitiendo un token inicial.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<PersonaResumenDto> RegistrarPersonaAsync(
        string nombreCompleto,
        string correo,
        string password,
        string? matricula,
        RolPersona rolSolicitado,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nombreCompleto))
        {
            throw new ArgumentException("El nombre completo es obligatorio.", nameof(nombreCompleto));
        }

        if (string.IsNullOrWhiteSpace(correo))
        {
            throw new ArgumentException("El correo electrónico es obligatorio.", nameof(correo));
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            throw new ArgumentException("La contraseña debe tener al menos 8 caracteres.", nameof(password));
        }

        string correoNormalizado = correo.Trim().ToLowerInvariant();
        string nombreNormalizado = nombreCompleto.Trim();
        string? matriculaNormalizada = string.IsNullOrWhiteSpace(matricula) ? null : matricula.Trim().ToUpperInvariant();

        await using UmadDbContext db = await _dbContextFactory
            .CreateDbContextAsync(ct)
            .ConfigureAwait(false);

        bool existeCorreo = await db.Personas
            .AsNoTracking()
            .AnyAsync(p => p.Correo.ToLower() == correoNormalizado, ct)
            .ConfigureAwait(false);

        if (existeCorreo)
        {
            throw new InvalidOperationException("Ya existe una cuenta registrada con ese correo electrónico.");
        }

        if (matriculaNormalizada is not null)
        {
            bool existeMatricula = await db.Personas
                .AsNoTracking()
                .AnyAsync(p => p.Matricula != null && p.Matricula.ToUpper() == matriculaNormalizada, ct)
                .ConfigureAwait(false);

            if (existeMatricula)
            {
                throw new InvalidOperationException("La matrícula ingresada ya se encuentra registrada.");
            }
        }

        bool baseVacia = !await db.Personas.AsNoTracking().AnyAsync(ct).ConfigureAwait(false);
        RolPersona rolEfectivo = rolSolicitado switch
        {
            RolPersona.Administrador when baseVacia => RolPersona.Administrador,
            RolPersona.Estudiante => RolPersona.Estudiante,
            RolPersona.Docente => RolPersona.Docente,
            RolPersona.Administrativo => RolPersona.Administrativo,
            RolPersona.Visitante => RolPersona.Visitante,
            _ => throw new UnauthorizedAccessException("Solo se permite autoregistro para cuentas de Comunidad UMAD o administrador inicial en base vacía.")
        };

        Persona nuevaPersona = new()
        {
            IdentificadorGlobal = Guid.NewGuid(),
            Matricula = matriculaNormalizada,
            NombreCompleto = nombreNormalizado.Length <= 150 ? nombreNormalizado : nombreNormalizado[..150],
            Correo = correoNormalizado.Length <= 150 ? correoNormalizado : correoNormalizado[..150],
            PasswordHash = _passwordHasher.HashPassword(password),
            Rol = rolEfectivo,
            Activo = true,
            FechaCreacionUtc = DateTime.UtcNow
        };

        db.Personas.Add(nuevaPersona);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        TipoPase tipoInicial = rolEfectivo == RolPersona.Visitante ? TipoPase.Visitante : TipoPase.Temporal;
        await _tokenAccesoService
            .EmitirTokenAsync(nuevaPersona.Id, tipoInicial, revocarPreviosActivos: false, cancellationToken: ct)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Persona {PersonaId} registrada con rol {Rol} y token inicial {TipoPase}.",
            nuevaPersona.Id,
            rolEfectivo,
            tipoInicial);

        return new PersonaResumenDto
        {
            Id = nuevaPersona.Id,
            IdentificadorGlobal = nuevaPersona.IdentificadorGlobal,
            Matricula = nuevaPersona.Matricula,
            NombreCompleto = nuevaPersona.NombreCompleto,
            Correo = nuevaPersona.Correo,
            Rol = nuevaPersona.Rol,
            Activo = nuevaPersona.Activo
        };
    }

    /// <summary>
    /// Lee un archivo de evidencia dentro de la frontera segura de AppDataDirectory/evidencias y lo codifica como Data URI.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<string?> LeerEvidenciaFotoDataUriAsync(
        string? rutaRelativa,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rutaRelativa))
        {
            return null;
        }

        try
        {
            string directorioEvidencias = Path.GetFullPath(Path.Combine(FileSystem.AppDataDirectory, CarpetaEvidencias));
            string frontera = directorioEvidencias.EndsWith(Path.DirectorySeparatorChar)
                ? directorioEvidencias
                : directorioEvidencias + Path.DirectorySeparatorChar;

            string rutaNormalizada = rutaRelativa.Trim().Replace('/', Path.DirectorySeparatorChar);
            string rutaCandidata = Path.GetFullPath(Path.Combine(FileSystem.AppDataDirectory, rutaNormalizada));

            if (!rutaCandidata.StartsWith(frontera, StringComparison.Ordinal) || !File.Exists(rutaCandidata))
            {
                return null;
            }

            FileInfo info = new(rutaCandidata);
            if (info.Length <= 0 || info.Length > TamanoMaximoLecturaBytes)
            {
                return null;
            }

            byte[] bytes = await File.ReadAllBytesAsync(rutaCandidata, ct).ConfigureAwait(false);
            string mime = Path.GetExtension(rutaCandidata).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => "image/jpeg"
            };

            return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No fue posible cargar la vista previa de evidencia en {RutaRelativa}.", rutaRelativa);
            return null;
        }
    }

    // Garantiza interpretación UTC para comparaciones de fechas en consultas LINQ.
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

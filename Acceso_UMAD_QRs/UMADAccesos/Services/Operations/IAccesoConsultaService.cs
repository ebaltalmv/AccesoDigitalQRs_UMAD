using UMADAccesos.Models;

namespace UMADAccesos.Services.Operations;

/// <summary>
/// Define las consultas reales contra SQLite y operaciones de soporte requeridas por las vistas Blazor Hybrid.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public interface IAccesoConsultaService
{
    /// <summary>
    /// Consulta el historial de registros de acceso aplicando filtros opcionales de persona, fechas, resultado, sincronización y búsqueda.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<IReadOnlyList<RegistroAcceso>> ObtenerRegistrosAccesoAsync(
        int? personaIdFiltro = null,
        DateTime? desdeUtc = null,
        DateTime? hastaUtc = null,
        ResultadoAcceso? resultadoFiltro = null,
        bool? soloPendientesSync = null,
        string? textoBusqueda = null,
        int limite = 200,
        CancellationToken ct = default);

    /// <summary>
    /// Recupera el detalle completo de un registro de acceso por su identificador primario, incluyendo persona, token, guardia y entrada relacionada.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<RegistroAcceso?> ObtenerRegistroAccesoPorIdAsync(
        int registroId,
        CancellationToken ct = default);

    /// <summary>
    /// Obtiene todos los tokens activos y no revocados en la institución para gestión y revocación administrativa.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<IReadOnlyList<TokenAcceso>> ObtenerTokensActivosGlobalAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Obtiene el historial de solicitudes de renovación de una persona específica ordenado de la más reciente a la más antigua.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<IReadOnlyList<SolicitudRenovacion>> ObtenerRenovacionesPorPersonaAsync(
        int personaId,
        CancellationToken ct = default);

    /// <summary>
    /// Obtiene los pases por evento registrados en SQLite, filtrando opcionalmente por asistente.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<IReadOnlyList<PaseEvento>> ObtenerPasesEventoAsync(
        int? personaIdFiltro = null,
        CancellationToken ct = default);

    /// <summary>
    /// Obtiene la proyección segura (sin hashes) de las personas activas registradas en SQLite.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<IReadOnlyList<PersonaResumenDto>> ObtenerPersonasActivasAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Devuelve la cantidad total de personas registradas en la base de datos local.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<int> ObtenerCantidadPersonasAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Registra una nueva cuenta de forma segura con hash PBKDF2-SHA256 y emite su token inicial.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<PersonaResumenDto> RegistrarPersonaAsync(
        string nombreCompleto,
        string correo,
        string password,
        string? matricula,
        RolPersona rolSolicitado,
        CancellationToken ct = default);

    /// <summary>
    /// Lee de forma segura una fotografía de evidencia dentro del directorio de evidencias y la convierte a Data URI para previsualización.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<string?> LeerEvidenciaFotoDataUriAsync(
        string? rutaRelativa,
        CancellationToken ct = default);
}

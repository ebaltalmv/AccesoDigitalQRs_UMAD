using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UMADAccesos.Data;
using UMADAccesos.Models;

namespace UMADAccesos.Services.AI;

/// <summary>
/// Proveedor de IA local offline que ejecuta clasificación NLP heurística en español y detección de anomalías sobre SQLite.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public sealed class OfflineLocalIaProvider : IIaProvider
{
    // Nombre identificador de este proveedor heurístico offline.
    private const string NombreProveedorConstante = "OfflineLocalHeuristico";

    // Puntaje mínimo requerido para asignar una categoría específica distinta de Otro.
    private const double PuntajeMinimoClasificacion = 1.5;

    // Ventana en minutos para detectar cambio brusco de punto de acceso ("viaje imposible").
    private const int MinutosCambioBruscoPuntoAcceso = 5;

    // Límite superior de registros históricos consultados por persona para acotar latencia en SQLite.
    private const int MaximoHistorialConsultado = 500;

    // Nombre canónico del área de destino por defecto cuando no se identifica una oficina específica.
    private const string AreaRecepcionGeneral = "Recepción General";

    // Áreas institucionales sensibles que exigen aprobación o validación especial en caseta.
    private static readonly HashSet<string> AreasSensibles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Rectoría",
        "Caja y Finanzas",
        "Almacén / Recepción de Proveedores",
        "Servicios Generales y Mantenimiento"
    };

    // Léxico ponderado de frases y términos en español normalizado por tipo de visita.
    private static readonly IReadOnlyList<EntradaLexicoCategoria> LexicoCategorias = new EntradaLexicoCategoria[]
    {
        // ProveedorEntrega
        new(TipoVisitaSugerido.ProveedorEntrega, "entrega de", 3.5, true),
        new(TipoVisitaSugerido.ProveedorEntrega, "entregar", 3.2, false),
        new(TipoVisitaSugerido.ProveedorEntrega, "entrega", 3.0, false),
        new(TipoVisitaSugerido.ProveedorEntrega, "papeleria", 3.5, false),
        new(TipoVisitaSugerido.ProveedorEntrega, "proveedor", 4.0, false),
        new(TipoVisitaSugerido.ProveedorEntrega, "proveedores", 4.0, false),
        new(TipoVisitaSugerido.ProveedorEntrega, "paqueteria", 4.0, false),
        new(TipoVisitaSugerido.ProveedorEntrega, "paquete", 3.2, false),
        new(TipoVisitaSugerido.ProveedorEntrega, "mensajeria", 3.8, false),
        new(TipoVisitaSugerido.ProveedorEntrega, "pedido", 2.8, false),
        new(TipoVisitaSugerido.ProveedorEntrega, "insumos", 3.2, false),
        new(TipoVisitaSugerido.ProveedorEntrega, "suministros", 3.2, false),
        new(TipoVisitaSugerido.ProveedorEntrega, "mercancia", 3.5, false),
        new(TipoVisitaSugerido.ProveedorEntrega, "abastecimiento", 3.5, false),
        new(TipoVisitaSugerido.ProveedorEntrega, "reparto", 3.5, false),
        new(TipoVisitaSugerido.ProveedorEntrega, "repartidor", 3.8, false),
        new(TipoVisitaSugerido.ProveedorEntrega, "almacen", 2.8, false),
        new(TipoVisitaSugerido.ProveedorEntrega, "carga y descarga", 4.0, true),
        new(TipoVisitaSugerido.ProveedorEntrega, "recoger paquete", 3.8, true),
        new(TipoVisitaSugerido.ProveedorEntrega, "recoger pedido", 3.8, true),

        // Evento
        new(TipoVisitaSugerido.Evento, "congreso", 4.2, false),
        new(TipoVisitaSugerido.Evento, "conferencia", 4.0, false),
        new(TipoVisitaSugerido.Evento, "seminario", 3.8, false),
        new(TipoVisitaSugerido.Evento, "simposio", 4.0, false),
        new(TipoVisitaSugerido.Evento, "ceremonia", 4.0, false),
        new(TipoVisitaSugerido.Evento, "graduacion", 4.2, false),
        new(TipoVisitaSugerido.Evento, "evento", 3.8, false),
        new(TipoVisitaSugerido.Evento, "auditorio", 2.8, false),
        new(TipoVisitaSugerido.Evento, "ponencia", 3.8, false),
        new(TipoVisitaSugerido.Evento, "ponente", 3.8, false),
        new(TipoVisitaSugerido.Evento, "expositor", 3.5, false),
        new(TipoVisitaSugerido.Evento, "feria", 3.2, false),
        new(TipoVisitaSugerido.Evento, "torneo", 3.5, false),
        new(TipoVisitaSugerido.Evento, "concierto", 3.8, false),
        new(TipoVisitaSugerido.Evento, "foro", 3.0, false),
        new(TipoVisitaSugerido.Evento, "asisto", 1.2, false),
        new(TipoVisitaSugerido.Evento, "asistir", 1.2, false),
        new(TipoVisitaSugerido.Evento, "invitado", 2.0, false),

        // Administrativo
        new(TipoVisitaSugerido.Administrativo, "control escolar", 4.5, true),
        new(TipoVisitaSugerido.Administrativo, "servicios escolares", 4.5, true),
        new(TipoVisitaSugerido.Administrativo, "recoger certificado", 3.5, true),
        new(TipoVisitaSugerido.Administrativo, "certificado", 3.5, false),
        new(TipoVisitaSugerido.Administrativo, "constancia", 3.5, false),
        new(TipoVisitaSugerido.Administrativo, "kardex", 3.8, false),
        new(TipoVisitaSugerido.Administrativo, "tramite", 3.8, false),
        new(TipoVisitaSugerido.Administrativo, "tramites", 3.8, false),
        new(TipoVisitaSugerido.Administrativo, "inscripcion", 3.8, false),
        new(TipoVisitaSugerido.Administrativo, "reinscripcion", 3.8, false),
        new(TipoVisitaSugerido.Administrativo, "admisiones", 3.6, false),
        new(TipoVisitaSugerido.Administrativo, "colegiatura", 3.8, false),
        new(TipoVisitaSugerido.Administrativo, "factura", 3.4, false),
        new(TipoVisitaSugerido.Administrativo, "facturacion", 3.6, false),
        new(TipoVisitaSugerido.Administrativo, "beca", 3.2, false),
        new(TipoVisitaSugerido.Administrativo, "becas", 3.2, false),
        new(TipoVisitaSugerido.Administrativo, "caja", 3.0, false),
        new(TipoVisitaSugerido.Administrativo, "finanzas", 3.2, false),
        new(TipoVisitaSugerido.Administrativo, "tesoreria", 3.4, false),
        new(TipoVisitaSugerido.Administrativo, "titulacion", 3.5, false),
        new(TipoVisitaSugerido.Administrativo, "titulo", 2.8, false),
        new(TipoVisitaSugerido.Administrativo, "credencial", 3.0, false),
        new(TipoVisitaSugerido.Administrativo, "pago", 2.8, false),
        new(TipoVisitaSugerido.Administrativo, "pagos", 2.8, false),
        new(TipoVisitaSugerido.Administrativo, "cita", 1.5, false),

        // Mantenimiento
        new(TipoVisitaSugerido.Mantenimiento, "aires acondicionados", 4.8, true),
        new(TipoVisitaSugerido.Mantenimiento, "aire acondicionado", 4.8, true),
        new(TipoVisitaSugerido.Mantenimiento, "mantenimiento", 4.5, false),
        new(TipoVisitaSugerido.Mantenimiento, "reparacion", 4.2, false),
        new(TipoVisitaSugerido.Mantenimiento, "reparar", 4.0, false),
        new(TipoVisitaSugerido.Mantenimiento, "instalacion electrica", 4.5, true),
        new(TipoVisitaSugerido.Mantenimiento, "plomeria", 4.2, false),
        new(TipoVisitaSugerido.Mantenimiento, "electricista", 4.2, false),
        new(TipoVisitaSugerido.Mantenimiento, "tecnico", 3.0, false),
        new(TipoVisitaSugerido.Mantenimiento, "fumigacion", 4.2, false),
        new(TipoVisitaSugerido.Mantenimiento, "jardineria", 3.8, false),
        new(TipoVisitaSugerido.Mantenimiento, "limpieza", 3.2, false),
        new(TipoVisitaSugerido.Mantenimiento, "subestacion", 4.2, false),
        new(TipoVisitaSugerido.Mantenimiento, "cableado", 3.8, false),
        new(TipoVisitaSugerido.Mantenimiento, "elevador", 3.8, false),
        new(TipoVisitaSugerido.Mantenimiento, "clima", 2.6, false),
        new(TipoVisitaSugerido.Mantenimiento, "revision", 2.2, false),
        new(TipoVisitaSugerido.Mantenimiento, "soporte tecnico", 3.8, true),
        new(TipoVisitaSugerido.Mantenimiento, "servicios generales", 3.8, true),

        // Academico
        new(TipoVisitaSugerido.Academico, "asesoria de tesis", 4.5, true),
        new(TipoVisitaSugerido.Academico, "asesoria", 3.2, false),
        new(TipoVisitaSugerido.Academico, "tesis", 4.2, false),
        new(TipoVisitaSugerido.Academico, "tutoria", 3.8, false),
        new(TipoVisitaSugerido.Academico, "clase", 3.4, false),
        new(TipoVisitaSugerido.Academico, "clases", 3.4, false),
        new(TipoVisitaSugerido.Academico, "examen", 3.8, false),
        new(TipoVisitaSugerido.Academico, "profesor", 3.2, false),
        new(TipoVisitaSugerido.Academico, "docente", 3.4, false),
        new(TipoVisitaSugerido.Academico, "investigacion", 3.8, false),
        new(TipoVisitaSugerido.Academico, "practica", 3.0, false),
        new(TipoVisitaSugerido.Academico, "practicas", 3.0, false),
        new(TipoVisitaSugerido.Academico, "laboratorio de ingenieria", 4.2, true),
        new(TipoVisitaSugerido.Academico, "laboratorio", 2.8, false),
        new(TipoVisitaSugerido.Academico, "biblioteca", 3.2, false),
        new(TipoVisitaSugerido.Academico, "direccion academica", 3.8, true),
        new(TipoVisitaSugerido.Academico, "coordinacion academica", 3.8, true),
        new(TipoVisitaSugerido.Academico, "sinodal", 3.8, false),
        new(TipoVisitaSugerido.Academico, "seminario de titulacion", 4.0, true)
    };

    // Reglas ponderadas de extracción de área destino UMAD a partir de frases y palabras clave.
    private static readonly IReadOnlyList<ReglaAreaDestino> ReglasAreas = new ReglaAreaDestino[]
    {
        new("Rectoría", "rectoria", 6.0, false),
        new("Rectoría", "rector", 5.5, false),
        new("Rectoría", "vicerrectoria", 5.5, false),
        new("Rectoría", "consejo directivo", 5.5, true),
        new("Rectoría", "secretaria general", 5.0, true),

        new("Control Escolar", "control escolar", 6.5, true),
        new("Control Escolar", "servicios escolares", 6.5, true),
        new("Control Escolar", "certificado", 3.5, false),
        new("Control Escolar", "kardex", 4.0, false),
        new("Control Escolar", "constancia", 3.5, false),
        new("Control Escolar", "inscripcion", 3.5, false),
        new("Control Escolar", "reinscripcion", 3.5, false),
        new("Control Escolar", "titulacion", 3.5, false),

        new("Auditorio Principal", "auditorio principal", 6.5, true),
        new("Auditorio Principal", "auditorio", 5.5, false),
        new("Auditorio Principal", "salon de actos", 5.5, true),
        new("Auditorio Principal", "foro", 4.0, false),

        new("Dirección Académica", "direccion academica", 6.5, true),
        new("Dirección Académica", "coordinacion academica", 6.0, true),
        new("Dirección Académica", "coordinacion de carrera", 5.5, true),
        new("Dirección Académica", "decanato", 5.5, false),

        new("Caja y Finanzas", "caja y finanzas", 6.5, true),
        new("Caja y Finanzas", "caja", 4.5, false),
        new("Caja y Finanzas", "finanzas", 5.0, false),
        new("Caja y Finanzas", "tesoreria", 5.5, false),
        new("Caja y Finanzas", "colegiatura", 4.5, false),
        new("Caja y Finanzas", "facturacion", 4.5, false),
        new("Caja y Finanzas", "becas", 4.0, false),

        new("Almacén / Recepción de Proveedores", "recepcion de proveedores", 6.5, true),
        new("Almacén / Recepción de Proveedores", "almacen", 5.5, false),
        new("Almacén / Recepción de Proveedores", "bodega", 5.0, false),
        new("Almacén / Recepción de Proveedores", "anden", 5.0, false),
        new("Almacén / Recepción de Proveedores", "carga y descarga", 5.5, true),

        new("Servicios Generales y Mantenimiento", "servicios generales", 6.5, true),
        new("Servicios Generales y Mantenimiento", "mantenimiento", 5.5, false),
        new("Servicios Generales y Mantenimiento", "aires acondicionados", 5.0, true),
        new("Servicios Generales y Mantenimiento", "aire acondicionado", 5.0, true),
        new("Servicios Generales y Mantenimiento", "subestacion", 5.0, false),
        new("Servicios Generales y Mantenimiento", "cuarto de maquinas", 5.5, true),
        new("Servicios Generales y Mantenimiento", "plomeria", 4.5, false),
        new("Servicios Generales y Mantenimiento", "electricidad", 4.5, false),

        new("Laboratorios de Ingeniería", "laboratorios de ingenieria", 6.5, true),
        new("Laboratorios de Ingeniería", "laboratorio de ingenieria", 6.5, true),
        new("Laboratorios de Ingeniería", "taller de ingenieria", 6.0, true),
        new("Laboratorios de Ingeniería", "laboratorio", 4.5, false),
        new("Laboratorios de Ingeniería", "centro de computo", 5.0, true),
        new("Laboratorios de Ingeniería", "robotica", 4.5, false),

        new("Biblioteca", "biblioteca", 6.0, false),
        new("Biblioteca", "hemeroteca", 5.5, false),
        new("Biblioteca", "sala de lectura", 5.5, true),
        new("Biblioteca", "prestamo de libros", 5.0, true),

        new(AreaRecepcionGeneral, "recepcion general", 5.5, true),
        new(AreaRecepcionGeneral, "recepcion", 3.5, false),
        new(AreaRecepcionGeneral, "informes", 3.5, false)
    };

    // Fábrica de contextos SQLite para consultar historial de accesos de forma aislada.
    private readonly IDbContextFactory<UmadDbContext> _dbContextFactory;

    // Opciones operativas con umbrales y zona horaria institucional.
    private readonly IaOptions _options;

    // Registrador estructurado para diagnóstico local sin datos sensibles.
    private readonly ILogger<OfflineLocalIaProvider> _logger;

    // Modo operativo de este proveedor local offline.
    public ModoProveedorIa Modo => ModoProveedorIa.OfflineLocal;

    // Nombre lógico reportado en cada resultado emitido por este proveedor.
    public string NombreProveedor => NombreProveedorConstante;

    /// <summary>
    /// Inicializa una nueva instancia del proveedor de IA heurístico offline respaldado por SQLite.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public OfflineLocalIaProvider(
        IDbContextFactory<UmadDbContext> dbContextFactory,
        IaOptions options,
        ILogger<OfflineLocalIaProvider> logger)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Clasifica offline un motivo de visita en español usando normalización de diacríticos y léxicos ponderados.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public Task<ClasificacionVisitaResult> ClasificarMotivoVisitaAsync(string textoLibre, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(textoLibre))
        {
            return Task.FromResult(new ClasificacionVisitaResult
            {
                TipoVisitaSugerido = TipoVisitaSugerido.Otro,
                AreaDestinoSugerida = AreaRecepcionGeneral,
                NivelConfianza = 0.0,
                RequiereAprobacionEspecial = true,
                Justificacion = "No se especificó el motivo de la visita. Solicitar aclaración y autorización en caseta.",
                ProveedorUtilizado = NombreProveedorConstante,
                PalabrasClaveDetectadas = Array.Empty<string>()
            });
        }

        string textoNormalizado = NormalizarTextoEspanol(textoLibre);
        string textoAcolchado = $" {textoNormalizado} ";
        HashSet<string> tokens = textoNormalizado
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.Ordinal);

        Dictionary<TipoVisitaSugerido, double> puntajesCategoria = new();
        List<string> palabrasDetectadas = new();
        HashSet<string> palabrasUnicas = new(StringComparer.Ordinal);

        foreach (EntradaLexicoCategoria entrada in LexicoCategorias)
        {
            bool coincide = entrada.EsFrase
                ? textoAcolchado.Contains($" {entrada.Patron} ", StringComparison.Ordinal)
                : tokens.Contains(entrada.Patron);

            if (!coincide)
            {
                continue;
            }

            puntajesCategoria[entrada.Categoria] = puntajesCategoria.GetValueOrDefault(entrada.Categoria) + entrada.Peso;
            if (palabrasUnicas.Add(entrada.Patron))
            {
                palabrasDetectadas.Add(entrada.Patron);
            }
        }

        string? areaDetectada = ExtraerAreaDestino(textoAcolchado, tokens, palabrasDetectadas, palabrasUnicas);

        List<KeyValuePair<TipoVisitaSugerido, double>> ordenadas = puntajesCategoria
            .OrderByDescending(kv => kv.Value)
            .ToList();

        double mejorPuntaje = ordenadas.Count > 0 ? ordenadas[0].Value : 0.0;
        double segundoPuntaje = ordenadas.Count > 1 ? ordenadas[1].Value : 0.0;

        if (mejorPuntaje < PuntajeMinimoClasificacion)
        {
            double confianzaBaja = Math.Round(Math.Clamp(0.15 + (mejorPuntaje * 0.12), 0.15, 0.35), 2);
            string areaAmbigua = areaDetectada ?? AreaRecepcionGeneral;

            return Task.FromResult(new ClasificacionVisitaResult
            {
                TipoVisitaSugerido = TipoVisitaSugerido.Otro,
                AreaDestinoSugerida = areaAmbigua,
                NivelConfianza = confianzaBaja,
                RequiereAprobacionEspecial = true,
                Justificacion = "El motivo declarado es ambiguo o no coincide con una categoría institucional clara. Requiere validación manual del guardia.",
                ProveedorUtilizado = NombreProveedorConstante,
                PalabrasClaveDetectadas = palabrasDetectadas
            });
        }

        TipoVisitaSugerido categoriaGanadora = ordenadas[0].Key;
        string areaFinal = areaDetectada ?? ResolverAreaPorDefectoCategoria(categoriaGanadora);

        double fuerza = Math.Min(1.0, mejorPuntaje / 5.5);
        double margen = segundoPuntaje <= 0.0 ? 1.0 : mejorPuntaje / (mejorPuntaje + segundoPuntaje);
        double confianza = Math.Round(Math.Clamp(0.45 + (0.35 * fuerza) + (0.18 * margen), 0.50, 0.98), 2);

        bool esAreaSensible = AreasSensibles.Contains(areaFinal);
        bool requiereAprobacion = categoriaGanadora == TipoVisitaSugerido.ProveedorEntrega
            || categoriaGanadora == TipoVisitaSugerido.Mantenimiento
            || categoriaGanadora == TipoVisitaSugerido.Otro
            || esAreaSensible
            || confianza < 0.60;

        string justificacion = ConstruirJustificacionClasificacion(
            categoriaGanadora,
            areaFinal,
            palabrasDetectadas,
            requiereAprobacion,
            esAreaSensible);

        ClasificacionVisitaResult resultado = new()
        {
            TipoVisitaSugerido = categoriaGanadora,
            AreaDestinoSugerida = areaFinal,
            NivelConfianza = confianza,
            RequiereAprobacionEspecial = requiereAprobacion,
            Justificacion = justificacion,
            ProveedorUtilizado = NombreProveedorConstante,
            PalabrasClaveDetectadas = palabrasDetectadas
        };

        return Task.FromResult(resultado);
    }

    /// <summary>
    /// Evalúa las cuatro reglas institucionales de anomalías consultando el historial real en SQLite con AsNoTracking.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<EvaluacionAnomaliaResult> EvaluarAnomaliaAsync(ContextoEvaluacionAcceso contexto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ct.ThrowIfCancellationRequested();

        DateTime fechaEventoUtc = NormalizarUtc(
            contexto.FechaHoraEventoUtc == default ? DateTime.UtcNow : contexto.FechaHoraEventoUtc);

        TimeZoneInfo zonaHoraria = ResolverZonaHorariaInstitucional(_options.ZonaHorariaInstitucionalId);
        DateTime fechaEventoLocal = TimeZoneInfo.ConvertTimeFromUtc(fechaEventoUtc, zonaHoraria);

        List<RegistroHistoricoProyeccion> historialPrevio = new();

        if (contexto.PersonaId > 0)
        {
            await using UmadDbContext db = await _dbContextFactory
                .CreateDbContextAsync(ct)
                .ConfigureAwait(false);

            List<RegistroHistoricoProyeccion> registrosPersona = await db.RegistrosAcceso
                .AsNoTracking()
                .Where(r => r.PersonaId == contexto.PersonaId)
                .OrderByDescending(r => r.FechaHora)
                .ThenByDescending(r => r.Id)
                .Take(MaximoHistorialConsultado)
                .Select(r => new RegistroHistoricoProyeccion
                {
                    Id = r.Id,
                    PuntoAcceso = r.PuntoAcceso,
                    TipoMovimiento = r.TipoMovimiento,
                    Resultado = r.Resultado,
                    FechaHoraUtc = r.FechaHora,
                    EntradaRelacionadaId = r.EntradaRelacionadaId
                })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            foreach (RegistroHistoricoProyeccion registro in registrosPersona)
            {
                if (contexto.RegistroAccesoActualId.HasValue && registro.Id == contexto.RegistroAccesoActualId.Value)
                {
                    continue;
                }

                registro.FechaHoraUtc = NormalizarUtc(registro.FechaHoraUtc);
                historialPrevio.Add(registro);
            }
        }

        List<string> reglasActivadas = new();
        bool tieneIndicadorAlto = false;
        bool tieneMultiplesEntradas = false;
        bool tieneEntradaAbierta = false;
        bool tieneFueraHorario = false;
        bool tieneCambioPatron = false;
        bool tieneReintentosDenegados = false;

        // Regla 1: Múltiples entradas en el mismo día local institucional por encima del límite normal.
        int entradasPreviasHoy = historialPrevio.Count(r =>
            r.TipoMovimiento == TipoMovimientoAcceso.Entrada
            && r.Resultado == ResultadoAcceso.Autorizado
            && TimeZoneInfo.ConvertTimeFromUtc(r.FechaHoraUtc, zonaHoraria).Date == fechaEventoLocal.Date);

        int totalEntradasHoy = entradasPreviasHoy +
            (contexto.TipoMovimiento == TipoMovimientoAcceso.Entrada && contexto.TokenValido ? 1 : 0);

        int limiteEntradasDiarias = Math.Max(1, _options.MaximoEntradasDiariasNormales);
        if (totalEntradasHoy > limiteEntradasDiarias)
        {
            tieneMultiplesEntradas = true;
            reglasActivadas.Add(
                $"Exceso de entradas diarias ({totalEntradasHoy} entradas en el día local {fechaEventoLocal:yyyy-MM-dd}; límite normal: {limiteEntradasDiarias}).");

            if (totalEntradasHoy >= limiteEntradasDiarias * 2)
            {
                tieneIndicadorAlto = true;
            }
        }

        // Regla 2: Entrada registrada sin salida previa abierta (tailgating o registro incompleto).
        if (contexto.TipoMovimiento == TipoMovimientoAcceso.Entrada)
        {
            HashSet<int> entradasCerradasPorEnlace = historialPrevio
                .Where(r => r.TipoMovimiento == TipoMovimientoAcceso.Salida
                    && r.Resultado == ResultadoAcceso.Autorizado
                    && r.EntradaRelacionadaId.HasValue)
                .Select(r => r.EntradaRelacionadaId!.Value)
                .ToHashSet();

            List<DateTime> salidasSinEnlace = historialPrevio
                .Where(r => r.TipoMovimiento == TipoMovimientoAcceso.Salida
                    && r.Resultado == ResultadoAcceso.Autorizado
                    && !r.EntradaRelacionadaId.HasValue
                    && r.FechaHoraUtc <= fechaEventoUtc)
                .Select(r => r.FechaHoraUtc)
                .ToList();

            RegistroHistoricoProyeccion? entradaAbierta = historialPrevio
                .Where(r => r.TipoMovimiento == TipoMovimientoAcceso.Entrada
                    && r.Resultado == ResultadoAcceso.Autorizado
                    && r.FechaHoraUtc <= fechaEventoUtc
                    && !entradasCerradasPorEnlace.Contains(r.Id)
                    && !salidasSinEnlace.Any(fechaSalida => fechaSalida >= r.FechaHoraUtc))
                .OrderByDescending(r => r.FechaHoraUtc)
                .ThenByDescending(r => r.Id)
                .FirstOrDefault();

            if (entradaAbierta is not null)
            {
                tieneEntradaAbierta = true;
                DateTime horaEntradaLocal = TimeZoneInfo.ConvertTimeFromUtc(entradaAbierta.FechaHoraUtc, zonaHoraria);
                reglasActivadas.Add(
                    $"Entrada previa sin salida registrada (posible tailgating o registro incompleto; entrada #{entradaAbierta.Id} del {horaEntradaLocal:dd/MM HH:mm}).");
            }
        }

        // Regla 3: Acceso fuera del horario institucional permitido en zona horaria local.
        TimeSpan horaLocalEvento = fechaEventoLocal.TimeOfDay;
        bool fueraHorario = _options.HoraInicioPermitidaLocal <= _options.HoraFinPermitidaLocal
            ? (horaLocalEvento < _options.HoraInicioPermitidaLocal || horaLocalEvento > _options.HoraFinPermitidaLocal)
            : (horaLocalEvento < _options.HoraInicioPermitidaLocal && horaLocalEvento > _options.HoraFinPermitidaLocal);

        if (fueraHorario)
        {
            tieneFueraHorario = true;
            reglasActivadas.Add(
                $"Acceso fuera de horario institucional permitido ({fechaEventoLocal:HH:mm}; horario ordinario: {_options.HoraInicioPermitidaLocal:hh\\:mm}–{_options.HoraFinPermitidaLocal:hh\\:mm}).");
        }

        // Regla 4a: Cambio brusco de patrón horario respecto al promedio histórico.
        int minimoHistorial = Math.Max(1, _options.MinimoHistorialParaCambioPatron);
        int umbralDesviacionHoras = Math.Max(1, _options.UmbralDesviacionHorarioHoras);

        List<double> horasHistoricas = historialPrevio
            .Where(r => r.TipoMovimiento == TipoMovimientoAcceso.Entrada
                && r.Resultado == ResultadoAcceso.Autorizado
                && r.FechaHoraUtc < fechaEventoUtc)
            .Select(r => TimeZoneInfo.ConvertTimeFromUtc(r.FechaHoraUtc, zonaHoraria).TimeOfDay.TotalHours)
            .ToList();

        if (horasHistoricas.Count >= minimoHistorial)
        {
            double promedioHoras = CalcularPromedioCircularHoras(horasHistoricas);
            double desviacionHoras = Math.Abs(horaLocalEvento.TotalHours - promedioHoras);
            if (desviacionHoras > 12.0)
            {
                desviacionHoras = 24.0 - desviacionHoras;
            }

            if (desviacionHoras >= umbralDesviacionHoras)
            {
                tieneCambioPatron = true;
                TimeSpan horaPromedio = TimeSpan.FromHours(promedioHoras);
                reglasActivadas.Add(
                    $"Cambio brusco de patrón horario (desviación de {desviacionHoras:F1} h respecto al promedio habitual {horaPromedio:hh\\:mm}; umbral: {umbralDesviacionHoras} h).");
            }
        }

        // Regla 4b: Cambio brusco de punto de acceso en menos de 5 minutos.
        if (!string.IsNullOrWhiteSpace(contexto.PuntoAcceso))
        {
            string puntoActual = contexto.PuntoAcceso.Trim();
            DateTime ventanaPuntoUtc = fechaEventoUtc.AddMinutes(-MinutosCambioBruscoPuntoAcceso);

            RegistroHistoricoProyeccion? movimientoOtroPunto = historialPrevio
                .Where(r => r.Resultado == ResultadoAcceso.Autorizado
                    && r.FechaHoraUtc >= ventanaPuntoUtc
                    && r.FechaHoraUtc <= fechaEventoUtc
                    && !string.IsNullOrWhiteSpace(r.PuntoAcceso)
                    && !string.Equals(r.PuntoAcceso.Trim(), puntoActual, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(r => r.FechaHoraUtc)
                .FirstOrDefault();

            if (movimientoOtroPunto is not null)
            {
                tieneCambioPatron = true;
                tieneIndicadorAlto = true;
                double minutos = Math.Max(0.0, (fechaEventoUtc - movimientoOtroPunto.FechaHoraUtc).TotalMinutes);
                reglasActivadas.Add(
                    $"Cambio brusco de punto de acceso en {minutos:F1} min (de '{movimientoOtroPunto.PuntoAcceso}' a '{puntoActual}', menor a {MinutosCambioBruscoPuntoAcceso} min).");
            }
        }

        // Regla 4c: Intentos reiterados con token expirado, revocado o inválido en la ventana reciente.
        int ventanaReintentos = Math.Max(1, _options.VentanaReintentosMinutos);
        int umbralDenegados = Math.Max(1, _options.UmbralIntentosDenegados);
        DateTime inicioVentanaDenegadosUtc = fechaEventoUtc.AddMinutes(-ventanaReintentos);

        int denegadosPrevios = historialPrevio.Count(r =>
            r.Resultado == ResultadoAcceso.Denegado
            && r.FechaHoraUtc >= inicioVentanaDenegadosUtc
            && r.FechaHoraUtc <= fechaEventoUtc);

        int totalDenegados = denegadosPrevios + (contexto.TokenValido ? 0 : 1);
        if (totalDenegados >= umbralDenegados)
        {
            tieneReintentosDenegados = true;
            tieneIndicadorAlto = true;
            string detalleMotivo = string.IsNullOrWhiteSpace(contexto.MotivoValidacionToken)
                ? string.Empty
                : $" Motivo actual: {contexto.MotivoValidacionToken.Trim()}.";
            reglasActivadas.Add(
                $"Intentos reiterados con token expirado, revocado o inválido ({totalDenegados} intentos denegados en los últimos {ventanaReintentos} min).{detalleMotivo}");
        }

        if (reglasActivadas.Count == 0)
        {
            return EvaluacionAnomaliaResult.Normal(NombreProveedorConstante);
        }

        NivelRiesgoAnomalia nivelRiesgo = (tieneIndicadorAlto || reglasActivadas.Count >= 2)
            ? NivelRiesgoAnomalia.Alto
            : NivelRiesgoAnomalia.Medio;

        string recomendacion = ConstruirRecomendacionGuardia(
            nivelRiesgo,
            tieneMultiplesEntradas,
            tieneEntradaAbierta,
            tieneFueraHorario,
            tieneCambioPatron,
            tieneReintentosDenegados);

        _logger.LogInformation(
            "Anomalía detectada para PersonaId {PersonaId} con nivel {NivelRiesgo} ({CantidadReglas} reglas activadas).",
            contexto.PersonaId,
            nivelRiesgo,
            reglasActivadas.Count);

        return new EvaluacionAnomaliaResult
        {
            EsAnomalo = true,
            NivelRiesgo = nivelRiesgo,
            ReglasActivadas = reglasActivadas,
            RecomendacionGuardia = recomendacion,
            ProveedorUtilizado = NombreProveedorConstante,
            FechaEvaluacionUtc = DateTime.UtcNow,
            DegradadoPorFallo = false
        };
    }

    /// <summary>
    /// Normaliza texto en español a minúsculas sin acentos ni puntuación para coincidencia léxica robusta.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private static string NormalizarTextoEspanol(string texto)
    {
        string minusculas = texto.Trim().ToLowerInvariant();
        string descompuesto = minusculas.Normalize(NormalizationForm.FormD);
        StringBuilder sb = new(descompuesto.Length);
        bool ultimoFueEspacio = true;

        foreach (char c in descompuesto)
        {
            UnicodeCategory categoria = CharUnicodeInfo.GetUnicodeCategory(c);
            if (categoria == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
                ultimoFueEspacio = false;
            }
            else if (!ultimoFueEspacio)
            {
                sb.Append(' ');
                ultimoFueEspacio = true;
            }
        }

        return sb.ToString().Trim();
    }

    /// <summary>
    /// Extrae el área destino UMAD con mayor coincidencia ponderada dentro del texto normalizado.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private static string? ExtraerAreaDestino(
        string textoAcolchado,
        HashSet<string> tokens,
        List<string> palabrasDetectadas,
        HashSet<string> palabrasUnicas)
    {
        Dictionary<string, double> puntajesArea = new(StringComparer.OrdinalIgnoreCase);

        foreach (ReglaAreaDestino regla in ReglasAreas)
        {
            bool coincide = regla.EsFrase
                ? textoAcolchado.Contains($" {regla.Patron} ", StringComparison.Ordinal)
                : tokens.Contains(regla.Patron);

            if (!coincide)
            {
                continue;
            }

            puntajesArea[regla.Area] = puntajesArea.GetValueOrDefault(regla.Area) + regla.Peso;
            if (palabrasUnicas.Add(regla.Patron))
            {
                palabrasDetectadas.Add(regla.Patron);
            }
        }

        if (puntajesArea.Count == 0)
        {
            return null;
        }

        return puntajesArea
            .OrderByDescending(kv => kv.Value)
            .First()
            .Key;
    }

    /// <summary>
    /// Determina el área UMAD por defecto cuando el motivo no menciona explícitamente un departamento.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private static string ResolverAreaPorDefectoCategoria(TipoVisitaSugerido categoria)
    {
        return categoria switch
        {
            TipoVisitaSugerido.ProveedorEntrega => "Almacén / Recepción de Proveedores",
            TipoVisitaSugerido.Mantenimiento => "Servicios Generales y Mantenimiento",
            TipoVisitaSugerido.Evento => "Auditorio Principal",
            TipoVisitaSugerido.Academico => "Dirección Académica",
            TipoVisitaSugerido.Administrativo => "Control Escolar",
            _ => AreaRecepcionGeneral
        };
    }

    /// <summary>
    /// Redacta una justificación clara en español explicando la categoría, área sugerida y necesidad de aprobación.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private static string ConstruirJustificacionClasificacion(
        TipoVisitaSugerido categoria,
        string areaDestino,
        IReadOnlyList<string> palabrasClave,
        bool requiereAprobacion,
        bool esAreaSensible)
    {
        string listaPalabras = palabrasClave.Count > 0
            ? string.Join(", ", palabrasClave.Take(5))
            : "sin coincidencias directas";

        string sufijoAprobacion = requiereAprobacion
            ? (esAreaSensible
                ? " Requiere aprobación especial por tratarse de un área sensible o acceso controlado."
                : " Requiere aprobación especial en caseta antes de autorizar el ingreso.")
            : " Flujo ordinario permitido con registro estándar en caseta.";

        return $"Clasificado como '{categoria}' con destino sugerido '{areaDestino}' a partir de los indicadores: {listaPalabras}.{sufijoAprobacion}";
    }

    /// <summary>
    /// Construye una recomendación operativa accionable en español para el guardia según el nivel de riesgo y reglas activadas.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private static string ConstruirRecomendacionGuardia(
        NivelRiesgoAnomalia nivelRiesgo,
        bool tieneMultiplesEntradas,
        bool tieneEntradaAbierta,
        bool tieneFueraHorario,
        bool tieneCambioPatron,
        bool tieneReintentosDenegados)
    {
        List<string> acciones = new();

        if (tieneReintentosDenegados)
        {
            acciones.Add("verificar la vigencia del token y descartar uso de credenciales revocadas o expiradas");
        }

        if (tieneEntradaAbierta)
        {
            acciones.Add("confirmar salida previa pendiente y verificar posible tailgating");
        }

        if (tieneFueraHorario)
        {
            acciones.Add("solicitar autorización de acceso fuera de horario institucional");
        }

        if (tieneMultiplesEntradas)
        {
            acciones.Add("validar motivo de reingresos múltiples en la jornada");
        }

        if (tieneCambioPatron)
        {
            acciones.Add("corroborar identidad física con credencial institucional ante cambio inusual de patrón");
        }

        string detalleAcciones = acciones.Count > 0
            ? string.Join("; ", acciones)
            : "aplicar verificación reforzada de identidad";

        if (nivelRiesgo == NivelRiesgoAnomalia.Alto)
        {
            return $"Riesgo alto: retener momentáneamente el paso, {detalleAcciones} y notificar al supervisor de caseta.";
        }

        return $"Riesgo medio: solicitar identificación oficial vigente, {detalleAcciones} y anotar observación en bitácora.";
    }

    /// <summary>
    /// Resuelve la zona horaria institucional configurada con respaldo seguro a UTC-6 si el identificador no existe.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private TimeZoneInfo ResolverZonaHorariaInstitucional(string? zonaHorariaId)
    {
        if (!string.IsNullOrWhiteSpace(zonaHorariaId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(zonaHorariaId.Trim());
            }
            catch (TimeZoneNotFoundException)
            {
                // Se intenta equivalente de Windows para America/Mexico_City antes de aplicar fallback fijo UTC-6.
            }
            catch (InvalidTimeZoneException)
            {
                // Se recurre al respaldo determinista UTC-6.
            }

            if (string.Equals(zonaHorariaId.Trim(), "America/Mexico_City", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time (Mexico)");
                }
                catch (TimeZoneNotFoundException)
                {
                    // Se recurre al respaldo determinista UTC-6.
                }
                catch (InvalidTimeZoneException)
                {
                    // Se recurre al respaldo determinista UTC-6.
                }
            }
        }

        _logger.LogDebug(
            "No se encontró la zona horaria '{ZonaId}' en la plataforma; se utiliza respaldo fijo UTC-6.",
            zonaHorariaId);

        return TimeZoneInfo.CreateCustomTimeZone(
            "UMAD-UTC-6",
            TimeSpan.FromHours(-6),
            "Hora Institucional UMAD (UTC-6)",
            "Hora Institucional UMAD (UTC-6)");
    }

    /// <summary>
    /// Garantiza que un valor DateTime se interprete en formato UTC antes de conversiones horarias.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private static DateTime NormalizarUtc(DateTime fechaHora)
    {
        return fechaHora.Kind switch
        {
            DateTimeKind.Utc => fechaHora,
            DateTimeKind.Local => fechaHora.ToUniversalTime(),
            _ => DateTime.SpecifyKind(fechaHora, DateTimeKind.Utc)
        };
    }

    /// <summary>
    /// Calcula el promedio horario circular sobre 24 horas para comparar desviaciones incluso cerca de medianoche.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private static double CalcularPromedioCircularHoras(IReadOnlyList<double> horas)
    {
        double sumaSeno = 0.0;
        double sumaCoseno = 0.0;
        const double factorRadianes = (2.0 * Math.PI) / 24.0;

        foreach (double hora in horas)
        {
            double angulo = hora * factorRadianes;
            sumaSeno += Math.Sin(angulo);
            sumaCoseno += Math.Cos(angulo);
        }

        if (Math.Sqrt((sumaSeno * sumaSeno) + (sumaCoseno * sumaCoseno)) < 1e-6)
        {
            return horas.Average();
        }

        double anguloPromedio = Math.Atan2(sumaSeno, sumaCoseno);
        if (anguloPromedio < 0.0)
        {
            anguloPromedio += 2.0 * Math.PI;
        }

        return anguloPromedio / factorRadianes;
    }

    /// <summary>
    /// Representa una entrada del léxico ponderado para asociar términos o frases a un tipo de visita.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private sealed class EntradaLexicoCategoria
    {
        // Categoría de visita asociada al patrón léxico.
        public TipoVisitaSugerido Categoria { get; }

        // Palabra o frase normalizada en español sin acentos.
        public string Patron { get; }

        // Peso numérico que aporta esta coincidencia al puntaje de la categoría.
        public double Peso { get; }

        // Indica si el patrón corresponde a una frase multipalabra.
        public bool EsFrase { get; }

        /// <summary>
        /// Inicializa una nueva entrada ponderada del léxico de categorías.
        /// </summary>
        /// <author>Emmanuel Baltazar López</author>
        /// <date>25/11/2026</date>
        /// <version>1.0</version>
        /// <modification>25/11/2026</modification>
        public EntradaLexicoCategoria(TipoVisitaSugerido categoria, string patron, double peso, bool esFrase)
        {
            Categoria = categoria;
            Patron = patron;
            Peso = peso;
            EsFrase = esFrase;
        }
    }

    /// <summary>
    /// Representa una regla ponderada para detectar un área de destino institucional UMAD en texto normalizado.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private sealed class ReglaAreaDestino
    {
        // Nombre canónico del área institucional UMAD.
        public string Area { get; }

        // Palabra o frase normalizada que apunta al área.
        public string Patron { get; }

        // Peso de prioridad de esta regla en la selección del área destino.
        public double Peso { get; }

        // Indica si el patrón corresponde a una frase compuesta.
        public bool EsFrase { get; }

        /// <summary>
        /// Inicializa una nueva regla de extracción de área destino institucional.
        /// </summary>
        /// <author>Emmanuel Baltazar López</author>
        /// <date>25/11/2026</date>
        /// <version>1.0</version>
        /// <modification>25/11/2026</modification>
        public ReglaAreaDestino(string area, string patron, double peso, bool esFrase)
        {
            Area = area;
            Patron = patron;
            Peso = peso;
            EsFrase = esFrase;
        }
    }

    /// <summary>
    /// Proyección ligera de registros históricos de acceso leídos desde SQLite sin seguimiento de cambios.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private sealed class RegistroHistoricoProyeccion
    {
        // Identificador primario del registro histórico en SQLite.
        public int Id { get; init; }

        // Punto de acceso o caseta donde ocurrió el registro.
        public string PuntoAcceso { get; init; } = string.Empty;

        // Dirección del movimiento (entrada o salida).
        public TipoMovimientoAcceso TipoMovimiento { get; init; }

        // Decisión de acceso emitida para el registro histórico.
        public ResultadoAcceso Resultado { get; init; }

        // Marca de tiempo UTC normalizada del evento histórico.
        public DateTime FechaHoraUtc { get; set; }

        // Referencia opcional a la entrada cerrada cuando el registro es una salida.
        public int? EntradaRelacionadaId { get; init; }
    }
}

namespace UMADAccesos.Services.AI;

/// <summary>
/// Agrupa las opciones operativas y umbrales configurables del subsistema de IA y detección de anomalías.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public sealed class IaOptions
{
    // Proveedor activo seleccionado para clasificación de visitas y evaluación de anomalías.
    public ModoProveedorIa Modo { get; set; } = ModoProveedorIa.OfflineLocal;

    // Número máximo de entradas autorizadas por día local consideradas dentro del comportamiento normal.
    public int MaximoEntradasDiariasNormales { get; set; } = 4;

    // Hora local institucional de inicio del horario ordinario permitido de acceso al campus.
    public TimeSpan HoraInicioPermitidaLocal { get; set; } = new(6, 30, 0);

    // Hora local institucional de cierre del horario ordinario permitido de acceso al campus.
    public TimeSpan HoraFinPermitidaLocal { get; set; } = new(22, 0, 0);

    // Ventana temporal en minutos para contabilizar intentos reiterados con token inválido o denegado.
    public int VentanaReintentosMinutos { get; set; } = 30;

    // Cantidad mínima de intentos denegados dentro de la ventana temporal para disparar alerta.
    public int UmbralIntentosDenegados { get; set; } = 2;

    // Identificador IANA de la zona horaria institucional utilizada para evaluar horarios locales.
    public string ZonaHorariaInstitucionalId { get; set; } = "America/Mexico_City";

    // Diferencia mínima en horas respecto al horario promedio histórico para detectar cambio brusco de patrón.
    public int UmbralDesviacionHorarioHoras { get; set; } = 4;

    // Cantidad mínima de entradas históricas autorizadas requeridas antes de evaluar desviación horaria.
    public int MinimoHistorialParaCambioPatron { get; set; } = 3;

    // Opciones no secretas de conexión hacia el backend proxy de Azure AI en producción.
    public AzureAiEndpointOptions Azure { get; set; } = new();
}

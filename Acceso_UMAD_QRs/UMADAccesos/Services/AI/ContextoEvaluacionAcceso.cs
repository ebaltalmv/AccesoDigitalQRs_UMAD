using UMADAccesos.Models;

namespace UMADAccesos.Services.AI;

/// <summary>
/// Encapsula el contexto operativo de un evento de acceso para su evaluación de anomalías.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public sealed class ContextoEvaluacionAcceso
{
    // Identificador interno de la persona asociada al evento de acceso.
    public int PersonaId { get; set; }

    // Nombre o código del punto de acceso o caseta donde se registra el movimiento.
    public string? PuntoAcceso { get; set; }

    // Dirección del movimiento evaluado (entrada o salida).
    public TipoMovimientoAcceso TipoMovimiento { get; set; } = TipoMovimientoAcceso.Entrada;

    // Fecha y hora UTC exacta del evento sometido a evaluación.
    public DateTime FechaHoraEventoUtc { get; set; } = DateTime.UtcNow;

    // Identificador opcional del token digital presentado durante el intento.
    public int? TokenAccesoId { get; set; }

    // Indica si la validación criptográfica y de vigencia del token resultó exitosa.
    public bool TokenValido { get; set; } = true;

    // Descripción segura del resultado de validación del token, especialmente en rechazos.
    public string? MotivoValidacionToken { get; set; }

    // Identificador opcional del registro recién persistido para evitar doble conteo en SQLite.
    public int? RegistroAccesoActualId { get; set; }
}

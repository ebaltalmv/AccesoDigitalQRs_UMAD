namespace UMADAccesos.Models
{
    /// <summary>
    /// Registra un evento de entrada o salida en caseta con metadatos de auditoría y sincronización offline.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public class RegistroAcceso
    {
        // Identificador numérico primario del registro de acceso en SQLite.
        public int Id { get; set; }

        // Identificador único global para idempotencia durante la sincronización remota.
        public Guid IdentificadorGlobal { get; set; } = Guid.NewGuid();

        // Clave foránea hacia la persona asociada al evento de acceso.
        public int PersonaId { get; set; }

        // Clave foránea opcional hacia el token presentado en el intento de acceso.
        public int? TokenAccesoId { get; set; }

        // Nombre o código del punto de acceso o caseta donde ocurrió el evento.
        public string PuntoAcceso { get; set; } = string.Empty;

        // Dirección del movimiento registrado (entrada o salida).
        public TipoMovimientoAcceso TipoMovimiento { get; set; } = TipoMovimientoAcceso.Entrada;

        // Decisión de seguridad emitida para el intento de acceso.
        public ResultadoAcceso Resultado { get; set; } = ResultadoAcceso.Autorizado;

        // Detalle o justificación del resultado de validación, especialmente en rechazos.
        public string? MotivoResultado { get; set; }

        // Fecha y hora UTC exacta en la que ocurrió el evento de acceso.
        public DateTime FechaHora { get; set; } = DateTime.UtcNow;

        // Bandera rápida que indica si el evento ya fue confirmado por el servidor central.
        public bool Sincronizado { get; set; }

        // Estado detallado del flujo de sincronización offline del registro.
        public EstadoSincronizacion EstadoSincronizacion { get; set; } = EstadoSincronizacion.Pendiente;

        // Fecha y hora UTC del último intento de envío a la nube o servidor central.
        public DateTime? FechaUltimoIntento { get; set; }

        // Número acumulado de reintentos fallidos de sincronización.
        public int Reintentos { get; set; }

        // Descripción acotada del último error reportado durante la sincronización.
        public string? UltimoError { get; set; }

        // Referencia de navegación hacia la persona involucrada en el evento.
        public Persona? Persona { get; set; }

        // Referencia de navegación opcional hacia el token utilizado.
        public TokenAcceso? TokenAcceso { get; set; }
    }
}

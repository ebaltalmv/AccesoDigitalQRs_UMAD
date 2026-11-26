namespace UMADAccesos.Models
{
    /// <summary>
    /// Representa una solicitud de renovación o cambio de modalidad de pase digital con trazabilidad de resolución y sincronización offline.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public class SolicitudRenovacion
    {
        // Identificador numérico primario de la solicitud en SQLite.
        public int Id { get; set; }

        // Identificador único global para idempotencia durante la sincronización remota.
        public Guid IdentificadorGlobal { get; set; } = Guid.NewGuid();

        // Clave foránea hacia la persona que solicita la renovación del pase.
        public int PersonaId { get; set; }

        // Clave foránea opcional hacia el token de acceso previo o vigente asociado a la persona.
        public int? TokenAccesoOriginalId { get; set; }

        // Modalidad de pase solicitada por la persona.
        public TipoPase TipoPaseDeseado { get; set; } = TipoPase.Permanente;

        // Duración solicitada opcional para el nuevo pase en caso de aprobación.
        public TimeSpan? VigenciaSolicitada { get; set; }

        // Motivo o justificación proporcionada por el solicitante (máximo 500 caracteres).
        public string Justificacion { get; set; } = string.Empty;

        // Estado actual del trámite de renovación.
        public EstadoRenovacion Estado { get; set; } = EstadoRenovacion.Pendiente;

        // Observación o motivo de la decisión administrativa al aprobar o rechazar.
        public string? MotivoResolucion { get; set; }

        // Clave foránea opcional hacia el administrador o guardia que resolvió la solicitud.
        public int? ResueltoPorPersonaId { get; set; }

        // Clave foránea opcional hacia el nuevo token emitido tras aprobar la solicitud.
        public int? TokenAccesoNuevoId { get; set; }

        // Fecha y hora UTC en la que se registró la solicitud.
        public DateTime FechaSolicitudUtc { get; set; } = DateTime.UtcNow;

        // Fecha y hora UTC en la que se resolvió la solicitud.
        public DateTime? FechaResolucionUtc { get; set; }

        // Indica si el registro ya fue confirmado por el servidor central.
        public bool Sincronizado { get; set; }

        // Estado detallado del flujo de sincronización offline del registro.
        public EstadoSincronizacion EstadoSincronizacion { get; set; } = EstadoSincronizacion.Pendiente;

        // Fecha y hora UTC del último intento de envío al servidor central.
        public DateTime? FechaUltimoIntento { get; set; }

        // Número acumulado de reintentos fallidos de sincronización.
        public int Reintentos { get; set; }

        // Descripción acotada del último error reportado durante la sincronización.
        public string? UltimoError { get; set; }

        // Referencia de navegación hacia la persona solicitante.
        public Persona? Persona { get; set; }

        // Referencia de navegación opcional hacia la persona que resolvió la solicitud.
        public Persona? ResueltoPor { get; set; }

        // Referencia de navegación opcional hacia el token original.
        public TokenAcceso? TokenAccesoOriginal { get; set; }

        // Referencia de navegación opcional hacia el nuevo token emitido al aprobar.
        public TokenAcceso? TokenAccesoNuevo { get; set; }
    }
}

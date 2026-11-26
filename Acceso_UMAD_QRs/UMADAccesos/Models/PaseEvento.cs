namespace UMADAccesos.Models
{
    /// <summary>
    /// Representa un pase temporal vinculado a un evento institucional y respaldado por un token QR firmado.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public class PaseEvento
    {
        // Identificador numérico primario del pase de evento en SQLite.
        public int Id { get; set; }

        // Identificador único global para idempotencia durante la sincronización remota.
        public Guid IdentificadorGlobal { get; set; } = Guid.NewGuid();

        // Nombre oficial del evento institucional para el cual se autoriza el ingreso (máximo 200 caracteres).
        public string NombreEvento { get; set; } = string.Empty;

        // Descripción o notas operativas adicionales del evento (máximo 500 caracteres).
        public string? Descripcion { get; set; }

        // Fecha y hora UTC de inicio de vigencia del evento.
        public DateTime FechaInicioEvento { get; set; }

        // Fecha y hora UTC de finalización de vigencia del evento.
        public DateTime FechaFinEvento { get; set; }

        // Clave foránea hacia la persona asistente o visitante beneficiaria del pase.
        public int PersonaId { get; set; }

        // Clave foránea opcional hacia el token criptográfico de evento emitido automáticamente.
        public int? TokenAccesoId { get; set; }

        // Clave foránea hacia el guardia o administrador que generó el pase de evento.
        public int CreadoPorPersonaId { get; set; }

        // Fecha y hora UTC en la que se emitió el pase de evento.
        public DateTime FechaCreacionUtc { get; set; } = DateTime.UtcNow;

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

        // Referencia de navegación hacia el visitante o asistente titular del pase.
        public Persona? Persona { get; set; }

        // Referencia de navegación hacia el usuario responsable de la creación del pase.
        public Persona? CreadoPor { get; set; }

        // Referencia de navegación opcional hacia el token QR generado para el evento.
        public TokenAcceso? TokenAcceso { get; set; }
    }
}

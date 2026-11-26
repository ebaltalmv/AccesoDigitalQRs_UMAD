namespace UMADAccesos.Models
{
    /// <summary>
    /// Registra una incidencia operativa o de seguridad en caseta con evidencia fotográfica opcional y soporte de sincronización offline.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public class Incidencia
    {
        // Identificador numérico primario de la incidencia en SQLite.
        public int Id { get; set; }

        // Identificador único global para idempotencia durante la sincronización remota.
        public Guid IdentificadorGlobal { get; set; } = Guid.NewGuid();

        // Clave foránea hacia el guardia o usuario responsable de reportar la incidencia.
        public int ReportadoPorPersonaId { get; set; }

        // Clave foránea opcional hacia el registro de acceso vinculado con la incidencia.
        public int? RegistroAccesoId { get; set; }

        // Nivel de severidad asignado conforme a la clasificación de incidentes ITIL 4.
        public SeveridadIncidencia Severidad { get; set; } = SeveridadIncidencia.Baja;

        // Estado actual dentro del flujo de atención de la incidencia.
        public EstadoIncidencia Estado { get; set; } = EstadoIncidencia.Abierta;

        // Título breve y descriptivo de la incidencia reportada (máximo 200 caracteres).
        public string Titulo { get; set; } = string.Empty;

        // Descripción detallada de los hechos observados en caseta (máximo 2000 caracteres).
        public string Descripcion { get; set; } = string.Empty;

        // Ruta relativa al directorio de datos local donde se almacena la evidencia fotográfica.
        public string? RutaEvidenciaFoto { get; set; }

        // Huella criptográfica SHA-256 del archivo de evidencia fotográfica para verificar integridad.
        public string? HashEvidenciaFoto { get; set; }

        // Fecha y hora UTC en la que se levantó el reporte de incidencia.
        public DateTime FechaReporteUtc { get; set; } = DateTime.UtcNow;

        // Fecha y hora UTC en la que la incidencia alcanzó un estado resuelto o cerrado.
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

        // Referencia de navegación hacia la persona que reportó la incidencia.
        public Persona? ReportadoPor { get; set; }

        // Referencia de navegación opcional hacia el evento de acceso relacionado.
        public RegistroAcceso? RegistroAcceso { get; set; }
    }
}

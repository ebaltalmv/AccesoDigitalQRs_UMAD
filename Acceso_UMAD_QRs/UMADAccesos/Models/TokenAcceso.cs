namespace UMADAccesos.Models
{
    /// <summary>
    /// Representa un token digital firmado criptográficamente para acreditar el acceso de una persona.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public class TokenAcceso
    {
        // Identificador numérico primario del token en SQLite.
        public int Id { get; set; }

        // Identificador único global del token para trazabilidad y sincronización.
        public Guid IdentificadorGlobal { get; set; } = Guid.NewGuid();

        // Clave foránea hacia la persona propietaria del token.
        public int PersonaId { get; set; }

        // Cadena estructurada firmada con HMAC-SHA256 que se codifica en el QR.
        public string CodigoFirmado { get; set; } = string.Empty;

        // Modalidad del pase emitido (permanente, temporal, visitante o evento).
        public TipoPase TipoPase { get; set; } = TipoPase.Permanente;

        // Fecha y hora UTC en la que el token fue emitido.
        public DateTime FechaEmision { get; set; } = DateTime.UtcNow;

        // Fecha y hora UTC límite de validez del token.
        public DateTime FechaExpiracion { get; set; }

        // Indica si el token fue revocado de forma manual o por rotación.
        public bool Revocado { get; set; }

        // Fecha y hora UTC en la que se registró la revocación del token.
        public DateTime? FechaRevocacionUtc { get; set; }

        // Motivo de auditoría por el cual el token fue revocado.
        public string? MotivoRevocacion { get; set; }

        // Referencia de navegación hacia la persona titular del token.
        public Persona? Persona { get; set; }

        // Colección de registros de acceso validados mediante este token.
        public ICollection<RegistroAcceso> RegistrosAcceso { get; set; } = new List<RegistroAcceso>();
    }
}

namespace UMADAccesos.Models
{
    /// <summary>
    /// Representa una persona registrada en el sistema de accesos digitales UMAD.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public class Persona
    {
        // Identificador numérico primario en la base de datos local SQLite.
        public int Id { get; set; }

        // Identificador único global utilizado para reconciliación y sincronización entre nodos.
        public Guid IdentificadorGlobal { get; set; } = Guid.NewGuid();

        // Matrícula o clave institucional opcional asociada a la persona.
        public string? Matricula { get; set; }

        // Nombre completo de la persona.
        public string NombreCompleto { get; set; } = string.Empty;

        // Correo electrónico único utilizado para identificación e inicio de sesión.
        public string Correo { get; set; } = string.Empty;

        // Hash criptográfico PBKDF2-SHA256 de la contraseña; nunca texto plano.
        public string PasswordHash { get; set; } = string.Empty;

        // Rol institucional que determina permisos y alcances dentro del sistema.
        public RolPersona Rol { get; set; } = RolPersona.Estudiante;

        // Indica si la cuenta se encuentra habilitada para emitir y validar accesos.
        public bool Activo { get; set; } = true;

        // Fecha y hora UTC de creación del registro de la persona.
        public DateTime FechaCreacionUtc { get; set; } = DateTime.UtcNow;

        // Fecha y hora UTC de la última actualización realizada sobre la persona.
        public DateTime? FechaActualizacionUtc { get; set; }

        // Colección de tokens de acceso emitidos para esta persona.
        public ICollection<TokenAcceso> TokensAcceso { get; set; } = new List<TokenAcceso>();

        // Colección de registros históricos de entrada y salida asociados a esta persona.
        public ICollection<RegistroAcceso> RegistrosAcceso { get; set; } = new List<RegistroAcceso>();
    }
}

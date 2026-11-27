using UMADAccesos.Models;

namespace UMADAccesos.Services.Operations;

/// <summary>
/// Encapsula los datos de entrada requeridos para dar de alta un nuevo usuario desde la administración.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public sealed class CrearUsuarioRequest
{
    // Nombre completo del usuario que será registrado.
    public string NombreCompleto { get; set; } = string.Empty;

    // Correo electrónico institucional o personal único del usuario.
    public string Correo { get; set; } = string.Empty;

    // Matrícula o clave institucional opcional; si está en blanco se almacena como nula.
    public string? Matricula { get; set; }

    // Contraseña inicial en texto plano que será derivada mediante PBKDF2-SHA256.
    public string Password { get; set; } = string.Empty;

    // Rol institucional asignado al usuario creado.
    public RolPersona Rol { get; set; } = RolPersona.Estudiante;

    // Indica si se debe emitir automáticamente un token QR inicial al crear la cuenta.
    public bool EmitirTokenInicial { get; set; } = true;

    // Tipo de pase aplicado al token inicial cuando su emisión está habilitada.
    public TipoPase TipoPaseInicial { get; set; } = TipoPase.Permanente;
}

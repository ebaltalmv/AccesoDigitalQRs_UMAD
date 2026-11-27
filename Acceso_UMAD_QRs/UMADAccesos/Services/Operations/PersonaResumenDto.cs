using UMADAccesos.Models;

namespace UMADAccesos.Services.Operations;

/// <summary>
/// Representa una proyección segura de persona sin credenciales ni hashes para consultas de interfaz.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public sealed class PersonaResumenDto
{
    // Identificador numérico primario de la persona en SQLite.
    public int Id { get; init; }

    // Identificador único global de la persona.
    public Guid IdentificadorGlobal { get; init; }

    // Matrícula o clave institucional opcional.
    public string? Matricula { get; init; }

    // Nombre completo de la persona.
    public string NombreCompleto { get; init; } = string.Empty;

    // Correo electrónico institucional o registrado.
    public string Correo { get; init; } = string.Empty;

    // Rol institucional asignado en la entidad Persona.
    public RolPersona Rol { get; init; }

    // Indica si la persona se encuentra habilitada para operar.
    public bool Activo { get; init; }
}

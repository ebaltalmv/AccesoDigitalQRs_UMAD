namespace UMADAccesos.Models
{
    /// <summary>
    /// Define los roles institucionales permitidos dentro del sistema de control de accesos UMAD.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public enum RolPersona
    {
        // Estudiante inscrito en la institución.
        Estudiante = 1,

        // Personal docente activo.
        Docente = 2,

        // Personal administrativo y operativo.
        Administrativo = 3,

        // Persona externa con acceso temporal o registrado.
        Visitante = 4,

        // Personal de seguridad encargado de la validación en caseta.
        Guardia = 5,

        // Administrador del sistema con privilegios de gestión.
        Administrador = 6
    }
}

namespace UMADAccesos.Models
{
    /// <summary>
    /// Representa la decisión final emitida durante la validación de un intento de acceso.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public enum ResultadoAcceso
    {
        // El pase y la persona cumplieron todas las reglas de validación.
        Autorizado = 1,

        // El intento de acceso fue rechazado por seguridad o vigencia.
        Denegado = 2
    }
}

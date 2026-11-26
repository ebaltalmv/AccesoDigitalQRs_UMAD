namespace UMADAccesos.Models
{
    /// <summary>
    /// Define las etapas del flujo de atención de una incidencia operativa o de seguridad en caseta.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public enum EstadoIncidencia
    {
        // Incidencia recién reportada y pendiente de asignación o análisis.
        Abierta = 0,

        // Incidencia en curso de diagnóstico o recopilación de evidencias.
        EnInvestigacion = 1,

        // Incidencia cuya causa o contención ha sido aplicada y documentada.
        Resuelta = 2,

        // Incidencia verificada y finalizada administrativamente.
        Cerrada = 3
    }
}

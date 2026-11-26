namespace UMADAccesos.Models
{
    /// <summary>
    /// Clasifica el nivel de criticidad operativa de una incidencia conforme a criterios de gestión de incidentes ITIL 4.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public enum SeveridadIncidencia
    {
        // Incidente menor sin impacto directo en la seguridad ni interrupción del servicio en caseta.
        Baja = 1,

        // Incidente moderado que requiere seguimiento operativo sin detener el flujo de acceso.
        Media = 2,

        // Incidente relevante que compromete el control de acceso o requiere atención prioritaria.
        Alta = 3,

        // Incidente grave de seguridad o falla mayor que exige escalamiento inmediato.
        Critica = 4
    }
}

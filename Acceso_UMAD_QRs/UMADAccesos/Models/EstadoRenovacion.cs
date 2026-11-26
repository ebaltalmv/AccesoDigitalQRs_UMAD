namespace UMADAccesos.Models
{
    /// <summary>
    /// Define los estados del ciclo de vida de una solicitud de renovación de pase digital.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public enum EstadoRenovacion
    {
        // La solicitud ha sido registrada y espera revisión administrativa.
        Pendiente = 0,

        // La solicitud fue autorizada y se emitió un nuevo token de acceso.
        Aprobada = 1,

        // La solicitud fue denegada con un motivo de resolución documentado.
        Rechazada = 2
    }
}

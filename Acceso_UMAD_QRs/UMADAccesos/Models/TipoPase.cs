namespace UMADAccesos.Models
{
    /// <summary>
    /// Clasifica la modalidad y política de vigencia de un pase digital de acceso.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public enum TipoPase
    {
        // Pase de vigencia extendida para comunidad universitaria activa.
        Permanente = 1,

        // Pase provisional con duración limitada.
        Temporal = 2,

        // Pase expedido para visitantes externos.
        Visitante = 3,

        // Pase acotado a un evento institucional específico.
        Evento = 4
    }
}

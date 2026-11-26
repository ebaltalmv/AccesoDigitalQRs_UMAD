namespace UMADAccesos.Models
{
    /// <summary>
    /// Representa el estado de sincronización de un registro local respecto al servidor central.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public enum EstadoSincronizacion
    {
        // Registro persistido localmente en espera de envío al servidor.
        Pendiente = 0,

        // Registro transmitido y confirmado correctamente en el servidor.
        Sincronizado = 1,

        // Registro cuyo último intento de envío falló y requiere reintento.
        Error = 2
    }
}

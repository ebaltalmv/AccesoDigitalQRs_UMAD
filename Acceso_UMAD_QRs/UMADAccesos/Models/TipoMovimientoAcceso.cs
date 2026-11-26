namespace UMADAccesos.Models
{
    /// <summary>
    /// Indica la dirección del movimiento registrado en un punto de control de acceso.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public enum TipoMovimientoAcceso
    {
        // Ingreso de una persona al campus o área controlada.
        Entrada = 1,

        // Egreso de una persona del campus o área controlada.
        Salida = 2
    }
}

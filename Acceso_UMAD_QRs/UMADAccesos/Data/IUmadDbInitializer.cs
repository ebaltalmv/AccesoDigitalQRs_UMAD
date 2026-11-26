namespace UMADAccesos.Data
{
    /// <summary>
    /// Define el contrato para inicializar de forma segura e idempotente la base de datos local SQLite.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public interface IUmadDbInitializer
    {
        // Indica si el esquema local y los datos iniciales ya fueron verificados correctamente.
        bool EstaInicializada { get; }

        // Fecha y hora UTC en la que se completó la inicialización de la base de datos.
        DateTime? FechaInicializacionUtc { get; }

        /// <summary>
        /// Garantiza la creación del esquema local en SQLite y siembra los registros base si la base está vacía.
        /// </summary>
        /// <author>Emmanuel Baltazar López</author>
        /// <date>25/11/2026</date>
        /// <version>1.0</version>
        /// <modification>25/11/2026</modification>
        Task InitializeAsync(CancellationToken cancellationToken = default);
    }
}

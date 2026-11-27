namespace UMADAccesos.Services.AI;

/// <summary>
/// Define el modo de ejecución del proveedor de inteligencia artificial utilizado por el sistema de accesos.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public enum ModoProveedorIa
{
    // Proveedor local offline basado en heurísticas NLP y análisis sobre SQLite sin dependencia de red.
    OfflineLocal = 0,

    // Proveedor de producción que delega las inferencias al backend proxy seguro en Azure AI.
    AzureAiProduction = 1
}

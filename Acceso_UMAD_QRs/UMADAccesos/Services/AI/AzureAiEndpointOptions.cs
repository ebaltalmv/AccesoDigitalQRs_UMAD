namespace UMADAccesos.Services.AI;

/// <summary>
/// Contiene la configuración no secreta del endpoint backend proxy para el proveedor Azure AI en producción.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public sealed class AzureAiEndpointOptions
{
    // URL base HTTPS del backend proxy institucional (BFF); nunca contiene llaves ni credenciales.
    public string? BackendProxyBaseUrl { get; set; }

    // Nombre lógico del despliegue o perfil de modelo configurado en el backend de Azure AI.
    public string DeploymentName { get; set; } = "umad-accesos-ia-prod";

    // Tiempo máximo en segundos para esperar respuesta del backend proxy antes de cancelar la solicitud.
    public int TimeoutSegundos { get; set; } = 10;

    // Indica si el servicio debe degradar automáticamente al proveedor offline local cuando Azure falle.
    public bool DegradarAOfflineEnFallo { get; set; } = true;

    /// <summary>
    /// Verifica si la URL base del backend proxy está definida y cumple estrictamente con el esquema HTTPS.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public bool EstaConfiguradoHttps()
    {
        if (string.IsNullOrWhiteSpace(BackendProxyBaseUrl))
        {
            return false;
        }

        if (!Uri.TryCreate(BackendProxyBaseUrl.Trim(), UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        return uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(uri.Host);
    }
}

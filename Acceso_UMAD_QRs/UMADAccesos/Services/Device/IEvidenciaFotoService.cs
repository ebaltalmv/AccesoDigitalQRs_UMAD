namespace UMADAccesos.Services.Device;

/// <summary>
/// Define el contrato para el servicio de captura de evidencia fotográfica usando la cámara del dispositivo.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public interface IEvidenciaFotoService
{
    /// <summary>
    /// Captura una fotografía como evidencia, la almacena localmente y devuelve el resultado con la ruta y hash de integridad.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<EvidenciaFotoResult> CapturarEvidenciaAsync(string? nombreArchivo = null, CancellationToken ct = default);
}

namespace UMADAccesos.Services.Device;

/// <summary>
/// Define el contrato para el servicio de escaneo de códigos QR utilizando la cámara del dispositivo.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public interface IQrScannerService
{
    /// <summary>
    /// Abre el escáner de cámara, escanea un código QR y devuelve el resultado de la operación.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<QrScanResult> EscanearCodigoQrAsync(CancellationToken ct = default);
}

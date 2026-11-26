using Microsoft.Extensions.DependencyInjection;
using UMADAccesos.Services.Device;

namespace UMADAccesos.Extensions;

/// <summary>
/// Proporciona métodos de extensión para registrar los servicios nativos de dispositivo (escáner QR y evidencia fotográfica).
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public static class DeviceServiceCollectionExtensions
{
    /// <summary>
    /// Registra IQrScannerService e IEvidenciaFotoService como singletons en el contenedor de dependencias.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public static IServiceCollection AddUmadDeviceServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IQrScannerService, QrScannerService>();
        services.AddSingleton<IEvidenciaFotoService, EvidenciaFotoService>();

        return services;
    }
}

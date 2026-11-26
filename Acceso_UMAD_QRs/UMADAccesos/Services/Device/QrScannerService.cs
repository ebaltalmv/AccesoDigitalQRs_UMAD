using UMADAccesos.Pages;

namespace UMADAccesos.Services.Device;

/// <summary>
/// Implementación del servicio de escaneo QR que presenta una página nativa MAUI con cámara o devuelve un fallback en plataformas sin cámara.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public sealed class QrScannerService : IQrScannerService
{
    /// <summary>
    /// Abre la página de escaneo QR como modal nativa. En Windows desktop devuelve un resultado de error indicando que la cámara no está disponible.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<QrScanResult> EscanearCodigoQrAsync(CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
        {
            return QrScanResult.CanceladoPorUsuario();
        }

        // Fallback para Windows desktop donde no hay cámara disponible para escaneo ZXing.
#if WINDOWS
        return QrScanResult.Error(
            "La cámara no está disponible en esta plataforma. Utilice la entrada manual de código QR.");
#else
        try
        {
            PermissionStatus permisoEstado = await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                PermissionStatus actual = await Permissions.CheckStatusAsync<Permissions.Camera>();
                if (actual != PermissionStatus.Granted)
                {
                    actual = await Permissions.RequestAsync<Permissions.Camera>();
                }
                return actual;
            });

            if (permisoEstado != PermissionStatus.Granted)
            {
                return QrScanResult.Error(
                    "El permiso de cámara fue denegado. Habilítelo en la configuración del dispositivo para escanear códigos QR.");
            }

            TaskCompletionSource<QrScanResult> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            QrScannerPage? scannerPage = null;
            Page? paginaPrincipal = null;

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                IReadOnlyList<Window>? ventanas = Application.Current?.Windows;
                paginaPrincipal = ventanas is { Count: > 0 } ? ventanas[0].Page : null;

                if (paginaPrincipal is null)
                {
                    tcs.TrySetResult(QrScanResult.Error(
                        "No se pudo obtener la página principal de la aplicación."));
                    return;
                }

                scannerPage = new QrScannerPage(tcs);
                await paginaPrincipal.Navigation.PushModalAsync(scannerPage, animated: true);
            });

            if (tcs.Task.IsCompleted)
            {
                return await tcs.Task;
            }

            await using CancellationTokenRegistration registro = ct.Register(() =>
            {
                if (tcs.TrySetResult(QrScanResult.CanceladoPorUsuario()))
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        if (paginaPrincipal is not null &&
                            scannerPage is not null &&
                            paginaPrincipal.Navigation.ModalStack.Contains(scannerPage))
                        {
                            await paginaPrincipal.Navigation.PopModalAsync(animated: true);
                        }
                    });
                }
            });

            return await tcs.Task;
        }
        catch (Exception ex)
        {
            return QrScanResult.Error($"Error inesperado al escanear código QR: {ex.Message}");
        }
#endif
    }
}

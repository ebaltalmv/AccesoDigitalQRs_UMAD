using UMADAccesos.Services.Device;
using ZXing.Net.Maui;

namespace UMADAccesos.Pages;

/// <summary>
/// Página nativa MAUI que utiliza la cámara del dispositivo para escanear códigos QR mediante ZXing.Net.Maui.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public partial class QrScannerPage : ContentPage
{
    // TaskCompletionSource que permite devolver el resultado del escaneo de forma asíncrona.
    private readonly TaskCompletionSource<QrScanResult> _tcs;

    // Bandera atómica (0 = activo, 1 = finalizado) para garantizar que el resultado se resuelva una sola vez entre hilos.
    private int _finalizado;

    /// <summary>
    /// Inicializa la página de escaneo QR, configura el formato QR exclusivo y registra el TaskCompletionSource.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public QrScannerPage(TaskCompletionSource<QrScanResult> tcs)
    {
        InitializeComponent();
        _tcs = tcs;

        BarcodeReader.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormat.QrCode,
            AutoRotate = true,
            Multiple = false
        };
    }

    /// <summary>
    /// Solicita el permiso de cámara al abrir la página; activa la detección si es concedido o cierra con error si es denegado.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (Volatile.Read(ref _finalizado) != 0)
            return;

        try
        {
            PermissionStatus estado = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (estado != PermissionStatus.Granted)
            {
                estado = await Permissions.RequestAsync<Permissions.Camera>();
            }

            if (estado != PermissionStatus.Granted)
            {
                await CerrarConResultadoAsync(QrScanResult.Error(
                    "El permiso de cámara fue denegado. Habilítelo en la configuración del dispositivo para escanear códigos QR."));
                return;
            }

            if (Volatile.Read(ref _finalizado) == 0)
            {
                BarcodeReader.IsDetecting = true;
            }
        }
        catch (Exception ex)
        {
            await CerrarConResultadoAsync(QrScanResult.Error(
                $"No se pudo inicializar el escáner de cámara: {ex.Message}"));
        }
    }

    /// <summary>
    /// Maneja la desaparición de la página para asegurar la cancelación si el modal fue cerrado externamente.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        BarcodeReader.IsDetecting = false;

        if (Interlocked.Exchange(ref _finalizado, 1) == 0)
        {
            _tcs.TrySetResult(QrScanResult.CanceladoPorUsuario());
        }
    }

    /// <summary>
    /// Maneja el evento de detección de códigos de barras; captura el primer resultado QR y cierra la página modal.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        if (Volatile.Read(ref _finalizado) != 0)
            return;

        BarcodeResult[]? resultados = e.Results;
        if (resultados is null || resultados.Length == 0)
            return;

        string? codigo = resultados[0]?.Value;
        if (string.IsNullOrWhiteSpace(codigo))
            return;

        if (Interlocked.Exchange(ref _finalizado, 1) != 0)
            return;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            BarcodeReader.IsDetecting = false;
            await CerrarModalDeFormaSeguraAsync();
            _tcs.TrySetResult(QrScanResult.Exito(codigo));
        });
    }

    /// <summary>
    /// Maneja la pulsación del botón de cerrar; cancela la operación de escaneo y cierra la página modal.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private async void OnCerrarClicked(object? sender, EventArgs e)
    {
        await CerrarConResultadoAsync(QrScanResult.CanceladoPorUsuario());
    }

    /// <summary>
    /// Intercepta el botón físico/sistema de retroceso para devolver un resultado de cancelación.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    protected override bool OnBackButtonPressed()
    {
        if (Volatile.Read(ref _finalizado) != 0)
            return base.OnBackButtonPressed();

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await CerrarConResultadoAsync(QrScanResult.CanceladoPorUsuario());
        });

        return true;
    }

    /// <summary>
    /// Detiene la detección, cierra la página modal y completa el TaskCompletionSource con el resultado indicado.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private async Task CerrarConResultadoAsync(QrScanResult resultado)
    {
        if (Interlocked.Exchange(ref _finalizado, 1) != 0)
            return;

        BarcodeReader.IsDetecting = false;

        await CerrarModalDeFormaSeguraAsync();
        _tcs.TrySetResult(resultado);
    }

    /// <summary>
    /// Retira la página actual de la pila modal si aún se encuentra presentada.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private async Task CerrarModalDeFormaSeguraAsync()
    {
        if (Navigation.ModalStack.Contains(this))
        {
            await Navigation.PopModalAsync(animated: true);
        }
    }
}

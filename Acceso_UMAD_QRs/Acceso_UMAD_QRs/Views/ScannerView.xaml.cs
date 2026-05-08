using Acceso_UMAD_QRs.ViewModels;
using ZXing.Net.Maui;

namespace Acceso_UMAD_QRs.Views;

public partial class ScannerView : ContentPage
{
    public ScannerView(AccessLogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        barcodeReader.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormats.OneDimensional | BarcodeFormats.TwoDimensional,
            AutoRotate = true,
            Multiple = false
        };
        barcodeReader.IsDetecting = true;
    }

    private void CameraBarcodeReaderView_BarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        if (e.Results != null && e.Results.Any())
        {
            var first = e.Results.FirstOrDefault();
            if (first != null)
            {
                Dispatcher.DispatchAsync(async () =>
                {
                    barcodeReader.IsDetecting = false;
                    if (BindingContext is AccessLogViewModel vm)
                    {
                        await vm.ProcessQRScan(first.Value);
                    }
                    await Task.Delay(3000);
                    barcodeReader.IsDetecting = true;
                });
            }
        }
    }
}

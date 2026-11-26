using System.Security.Cryptography;

namespace UMADAccesos.Services.Device;

/// <summary>
/// Implementación del servicio de captura de evidencia fotográfica que utiliza MediaPicker, almacena la imagen y calcula su hash SHA-256.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public sealed class EvidenciaFotoService : IEvidenciaFotoService
{
    // Nombre de la subcarpeta dentro de AppDataDirectory donde se almacenan las evidencias fotográficas.
    private const string CarpetaEvidencias = "evidencias";

    /// <summary>
    /// Captura una fotografía usando la cámara del dispositivo, la guarda en almacenamiento local y retorna la ruta relativa con su hash SHA-256.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<EvidenciaFotoResult> CapturarEvidenciaAsync(string? nombreArchivo = null, CancellationToken ct = default)
    {
        try
        {
            if (ct.IsCancellationRequested)
            {
                return EvidenciaFotoResult.CanceladoPorUsuario();
            }

            // Verificar que MediaPicker esté soportado en la plataforma.
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                return EvidenciaFotoResult.Error(
                    "La captura de fotografía no está soportada en esta plataforma.");
            }

            // Solicitar permiso de cámara en el hilo principal.
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
                return EvidenciaFotoResult.Error(
                    "El permiso de cámara fue denegado. Habilítelo en la configuración del dispositivo para capturar evidencias.");
            }

            // Capturar la fotografía en el hilo principal de UI.
            FileResult? foto = await MainThread.InvokeOnMainThreadAsync(
                () => MediaPicker.Default.CapturePhotoAsync());

            if (foto is null)
            {
                return EvidenciaFotoResult.CanceladoPorUsuario();
            }

            // Preparar directorio de destino.
            string directorioBase = FileSystem.AppDataDirectory;
            string directorioEvidencias = Path.Combine(directorioBase, CarpetaEvidencias);
            Directory.CreateDirectory(directorioEvidencias);

            // Generar nombre de archivo único con marca de tiempo.
            string extension = Path.GetExtension(foto.FileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".jpg";
            }

            string? baseSolicitada = string.IsNullOrWhiteSpace(nombreArchivo)
                ? null
                : Path.GetFileNameWithoutExtension(nombreArchivo.Trim());

            string prefijo = string.IsNullOrWhiteSpace(baseSolicitada) ? "evidencia" : baseSolicitada;
            string nombreFinal = $"{prefijo}_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}{extension}";

            string rutaCompleta = Path.Combine(directorioEvidencias, nombreFinal);
            string rutaRelativa = Path.Combine(CarpetaEvidencias, nombreFinal);

            // Copiar la imagen al almacenamiento local.
            await using (Stream streamOrigen = await foto.OpenReadAsync())
            await using (FileStream streamDestino = File.Create(rutaCompleta))
            {
                await streamOrigen.CopyToAsync(streamDestino, ct);
            }

            // Calcular el hash SHA-256 del archivo guardado.
            string hashHex = await CalcularHashSha256Async(rutaCompleta, ct);

            return EvidenciaFotoResult.Exito(rutaRelativa, hashHex);
        }
        catch (FeatureNotSupportedException)
        {
            return EvidenciaFotoResult.Error(
                "La cámara no está disponible o no es compatible en este dispositivo.");
        }
        catch (PermissionException)
        {
            return EvidenciaFotoResult.Error(
                "No se cuenta con permisos de cámara para capturar la evidencia.");
        }
        catch (OperationCanceledException)
        {
            return EvidenciaFotoResult.CanceladoPorUsuario();
        }
        catch (Exception ex)
        {
            return EvidenciaFotoResult.Error($"Error inesperado al capturar evidencia: {ex.Message}");
        }
    }

    /// <summary>
    /// Calcula el hash SHA-256 de un archivo y lo devuelve como cadena hexadecimal en minúsculas.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private static async Task<string> CalcularHashSha256Async(string rutaArchivo, CancellationToken ct)
    {
        await using FileStream stream = File.OpenRead(rutaArchivo);
        byte[] hashBytes = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}

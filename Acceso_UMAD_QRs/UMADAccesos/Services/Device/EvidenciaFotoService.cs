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

    // Tamaño máximo permitido por archivo de evidencia fotográfica (10 MB).
    private const long TamanoMaximoBytes = 10L * 1024L * 1024L;

    // Extensiones de imagen permitidas por lista blanca para evitar almacenamiento de archivos arbitrarios.
    private static readonly HashSet<string> ExtensionesPermitidas = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

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

            // Preparar directorio de destino validando frontera estricta de ruta.
            string directorioBase = FileSystem.AppDataDirectory;
            string directorioEvidencias = Path.GetFullPath(Path.Combine(directorioBase, CarpetaEvidencias));
            Directory.CreateDirectory(directorioEvidencias);

            // Validar extensión contra lista blanca de imágenes.
            string extensionCandidata = Path.GetExtension(foto.FileName)?.Trim() ?? string.Empty;
            string extension = ExtensionesPermitidas.Contains(extensionCandidata)
                ? extensionCandidata.ToLowerInvariant()
                : ".jpg";

            // Sanitizar prefijo para evitar traversal de directorios o caracteres inválidos.
            string prefijo = SanitizarPrefijo(nombreArchivo);
            string nombreFinal = $"{prefijo}_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}"[..Math.Min(80, prefijo.Length + 29)] + extension;

            string rutaCompleta = Path.GetFullPath(Path.Combine(directorioEvidencias, nombreFinal));
            string fronteraDirectorio = directorioEvidencias.EndsWith(Path.DirectorySeparatorChar)
                ? directorioEvidencias
                : directorioEvidencias + Path.DirectorySeparatorChar;

            if (!rutaCompleta.StartsWith(fronteraDirectorio, StringComparison.Ordinal))
            {
                return EvidenciaFotoResult.Error("La ruta generada para la evidencia fotográfica no es válida.");
            }

            string rutaRelativa = Path.Combine(CarpetaEvidencias, nombreFinal);

            // Copiar la imagen imponiendo límite de tamaño y limpiando residuos ante cancelación o fallo.
            try
            {
                await using Stream streamOrigen = await foto.OpenReadAsync();
                await using FileStream streamDestino = File.Create(rutaCompleta);

                byte[] buffer = new byte[81920];
                long totalLeido = 0;
                int leidos;
                while ((leidos = await streamOrigen.ReadAsync(buffer, ct)) > 0)
                {
                    totalLeido += leidos;
                    if (totalLeido > TamanoMaximoBytes)
                    {
                        throw new InvalidOperationException("La evidencia fotográfica excede el tamaño máximo permitido de 10 MB.");
                    }

                    await streamDestino.WriteAsync(buffer.AsMemory(0, leidos), ct);
                }
            }
            catch
            {
                if (File.Exists(rutaCompleta))
                {
                    try { File.Delete(rutaCompleta); } catch { /* Ignorar fallo al limpiar archivo parcial. */ }
                }

                throw;
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
    /// Sanitiza el prefijo de archivo permitiendo únicamente caracteres alfanuméricos, guiones y guiones bajos.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private static string SanitizarPrefijo(string? nombreArchivo)
    {
        if (string.IsNullOrWhiteSpace(nombreArchivo))
        {
            return "evidencia";
        }

        string baseNombre = Path.GetFileNameWithoutExtension(nombreArchivo.Trim());
        char[] filtrados = baseNombre
            .Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-')
            .Take(32)
            .ToArray();

        return filtrados.Length == 0 ? "evidencia" : new string(filtrados);
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
